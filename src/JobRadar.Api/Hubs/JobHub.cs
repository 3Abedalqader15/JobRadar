using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JobRadar.Application.Common;
using JobRadar.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Api.Hubs;

public class JobHub : Hub
{
    public const string HubUrl = "/hubs/jobs";

    public const int MaxGroupNameLength = 100;
    public const int MaxGroupsPerCall = 30;
    public const int MaxGroupsPerConnection = 50;

    private static readonly Regex ValidGroupRegex = new(
        @"^grp:(all|remote|loc:[a-z0-9_\-\s]{1,90}|emp:[a-z0-9_\-]{1,50}|exp:[a-z0-9_\-]{1,50}|skill:[a-z0-9_\-\.\+#\s]{1,90})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Thread-safe tracking of group membership per connection
    private static readonly ConcurrentDictionary<string, HashSet<string>> ConnectionGroups = new();

    private readonly ILogger<JobHub> _logger;

    public JobHub(ILogger<JobHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates that a group name adheres to strict naming and namespace policies.
    /// </summary>
    public static bool IsValidGroupName(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return false;
        }

        var trimmed = groupName.Trim();
        if (trimmed.Length > MaxGroupNameLength || !trimmed.StartsWith("grp:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ValidGroupRegex.IsMatch(trimmed);
    }

    /// <summary>
    /// Joins the connection to a single validated criteria group.
    /// </summary>
    public async Task JoinCriteriaGroup(string groupName)
    {
        if (!IsValidGroupName(groupName))
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join invalid group name '{GroupName}'", Context.ConnectionId, groupName);
            return;
        }

        var normalized = groupName.Trim().ToLowerInvariant();
        var userGroups = ConnectionGroups.GetOrAdd(Context.ConnectionId, _ => new HashSet<string>());

        lock (userGroups)
        {
            if (userGroups.Count >= MaxGroupsPerConnection && !userGroups.Contains(normalized))
            {
                _logger.LogWarning("Connection {ConnectionId} reached max group subscription cap of {Cap}. Rejecting {Group}",
                    Context.ConnectionId, MaxGroupsPerConnection, normalized);
                return;
            }

            userGroups.Add(normalized);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, normalized);
    }

    /// <summary>
    /// Leaves a single criteria group.
    /// </summary>
    public async Task LeaveCriteriaGroup(string groupName)
    {
        if (!IsValidGroupName(groupName))
        {
            return;
        }

        var normalized = groupName.Trim().ToLowerInvariant();
        if (ConnectionGroups.TryGetValue(Context.ConnectionId, out var userGroups))
        {
            lock (userGroups)
            {
                userGroups.Remove(normalized);
            }
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalized);
    }

    /// <summary>
    /// Atomically removes old groups and joins new criteria groups with per-call and per-connection bounds.
    /// </summary>
    public async Task UpdateCriteriaSubscription(List<string>? oldGroups, List<string>? newGroups)
    {
        // 1. Process group leaves
        if (oldGroups is { Count: > 0 })
        {
            var cappedOld = oldGroups.Take(MaxGroupsPerCall).Distinct();
            foreach (var group in cappedOld)
            {
                if (IsValidGroupName(group))
                {
                    var normalized = group.Trim().ToLowerInvariant();
                    if (ConnectionGroups.TryGetValue(Context.ConnectionId, out var groups))
                    {
                        lock (groups)
                        {
                            groups.Remove(normalized);
                        }
                    }
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalized);
                }
            }
        }

        // 2. Process group joins
        if (newGroups is { Count: > 0 })
        {
            if (newGroups.Count > MaxGroupsPerCall)
            {
                _logger.LogWarning("Connection {ConnectionId} passed {Count} groups in single call; capping at {Cap}",
                    Context.ConnectionId, newGroups.Count, MaxGroupsPerCall);
            }

            var cappedNew = newGroups.Take(MaxGroupsPerCall).Distinct();
            var userGroups = ConnectionGroups.GetOrAdd(Context.ConnectionId, _ => new HashSet<string>());

            foreach (var group in cappedNew)
            {
                if (!IsValidGroupName(group))
                {
                    _logger.LogWarning("Connection {ConnectionId} attempted to join invalid group name '{GroupName}'", Context.ConnectionId, group);
                    continue;
                }

                var normalized = group.Trim().ToLowerInvariant();

                lock (userGroups)
                {
                    if (userGroups.Count >= MaxGroupsPerConnection && !userGroups.Contains(normalized))
                    {
                        _logger.LogWarning("Connection {ConnectionId} reached max group subscription cap of {Cap}. Skipping remaining joins.",
                            Context.ConnectionId, MaxGroupsPerConnection);
                        break;
                    }

                    userGroups.Add(normalized);
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, normalized);
            }
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectionGroups.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Helper method preserving backward compatibility for static calls.
    /// </summary>
    public static List<string> ComputeMatchingGroups(
        bool isRemote,
        string? location,
        EmploymentType employmentType,
        ExperienceLevel experienceLevel,
        IEnumerable<string>? skills = null,
        string? title = null)
        => JobRelevanceGroups.ComputeMatchingGroups(isRemote, location, employmentType, experienceLevel, skills, title);
}
