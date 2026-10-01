using JobRadar.Domain.Enums;

namespace JobRadar.Application.Common;

public static class JobRelevanceGroups
{
    /// <summary>
    /// Computes the list of criteria-based SignalR group keys that match a given job posting.
    /// </summary>
    public static List<string> ComputeMatchingGroups(
        bool isRemote,
        string? location,
        EmploymentType employmentType,
        ExperienceLevel experienceLevel,
        IEnumerable<string>? skills = null)
    {
        var groups = new List<string> { "grp:all" };

        if (isRemote)
        {
            groups.Add("grp:remote");
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim().ToLowerInvariant();
            groups.Add($"grp:loc:{loc}");
        }

        groups.Add($"grp:emp:{employmentType}");
        groups.Add($"grp:exp:{experienceLevel}");

        if (skills != null)
        {
            foreach (var skill in skills)
            {
                if (!string.IsNullOrWhiteSpace(skill))
                {
                    groups.Add($"grp:skill:{skill.Trim().ToLowerInvariant()}");
                }
            }
        }

        return groups.Distinct().ToList();
    }
}
