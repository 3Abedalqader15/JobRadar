using JobRadar.Domain.Enums;

namespace JobRadar.Application.Common;

public static class JobRelevanceGroups
{
    private static readonly char[] TokenSeparators = { ' ', '-', '/', ',', '(', ')', '.', '|' };

    /// <summary>
    /// Minimum token length for keyword/title indexing and search query criteria groups.
    /// Short tokens (1-2 chars like "it", "to", "on") are noisy stopwords.
    /// Using a shared constant prevents query tokenization from creating groups that title tokenization ignores.
    /// </summary>
    public const int MinTokenLength = 3;

    /// <summary>
    /// Computes the list of criteria-based SignalR group keys that match a given job posting.
    /// </summary>
    public static List<string> ComputeMatchingGroups(
        bool isRemote,
        string? location,
        EmploymentType employmentType,
        ExperienceLevel experienceLevel,
        IEnumerable<string>? skills = null,
        string? title = null)
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

        if (!string.IsNullOrWhiteSpace(title))
        {
            var tokens = title.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var clean = token.Trim().ToLowerInvariant();
                if (clean.Length >= MinTokenLength)
                {
                    groups.Add($"grp:skill:{clean}");
                }
            }
        }

        return groups.Distinct().ToList();
    }

    /// <summary>
    /// Computes the active SignalR groups that a search client should join based on their current filter state.
    /// If no filters are active, returns ["grp:all"]. Otherwise, returns only targeted criteria groups without "grp:all".
    /// </summary>
    public static List<string> ComputeCriteriaGroups(
        string? query,
        string? location,
        bool? isRemote,
        IEnumerable<EmploymentType>? employmentTypes,
        IEnumerable<ExperienceLevel>? experienceLevels,
        IEnumerable<string>? skills)
    {
        var groups = new List<string>();

        bool hasFilters = !string.IsNullOrWhiteSpace(query)
            || !string.IsNullOrWhiteSpace(location)
            || (isRemote == true)
            || (employmentTypes != null && employmentTypes.Any())
            || (experienceLevels != null && experienceLevels.Any())
            || (skills != null && skills.Any());

        if (!hasFilters)
        {
            return new List<string> { "grp:all" };
        }

        if (isRemote == true)
        {
            groups.Add("grp:remote");
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            groups.Add($"grp:loc:{location.Trim().ToLowerInvariant()}");
        }

        if (employmentTypes != null)
        {
            foreach (var emp in employmentTypes)
            {
                groups.Add($"grp:emp:{emp}");
            }
        }

        if (experienceLevels != null)
        {
            foreach (var exp in experienceLevels)
            {
                groups.Add($"grp:exp:{exp}");
            }
        }

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

        if (!string.IsNullOrWhiteSpace(query))
        {
            var words = query.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var clean = word.Trim().ToLowerInvariant();
                if (clean.Length >= MinTokenLength)
                {
                    groups.Add($"grp:skill:{clean}");
                }
            }
        }

        return groups.Distinct().ToList();
    }
}
