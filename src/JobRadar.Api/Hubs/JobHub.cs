using JobRadar.Application.Common;
using JobRadar.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace JobRadar.Api.Hubs;

public class JobHub : Hub
{
    public const string HubUrl = "/hubs/jobs";

    /// <summary>
    /// Joins the connection to specified criteria groups and leaves obsolete ones.
    /// </summary>
    public async Task UpdateCriteriaSubscription(List<string>? oldGroups, List<string>? newGroups)
    {
        if (oldGroups is { Count: > 0 })
        {
            foreach (var group in oldGroups.Distinct())
            {
                if (!string.IsNullOrWhiteSpace(group))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
                }
            }
        }

        if (newGroups is { Count: > 0 })
        {
            foreach (var group in newGroups.Distinct())
            {
                if (!string.IsNullOrWhiteSpace(group))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, group);
                }
            }
        }
    }

    /// <summary>
    /// Computes the list of criteria-based SignalR group keys that match a given job posting.
    /// </summary>
    public static List<string> ComputeMatchingGroups(
        bool isRemote,
        string? location,
        EmploymentType employmentType,
        ExperienceLevel experienceLevel,
        IEnumerable<string>? skills = null)
        => JobRelevanceGroups.ComputeMatchingGroups(isRemote, location, employmentType, experienceLevel, skills);
}
