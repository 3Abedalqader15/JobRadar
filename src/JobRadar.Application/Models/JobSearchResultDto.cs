using JobRadar.Domain.Enums;

namespace JobRadar.Application.Models;

public class JobSearchResultDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? Location { get; init; }
    public bool IsRemote { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public ExperienceLevel ExperienceLevel { get; init; }
    public double RelevanceScore { get; init; }
}
