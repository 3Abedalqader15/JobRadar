using JobRadar.Domain.Enums;

namespace JobRadar.Application.Models;

public class JobSearchResultDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsRemote { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public DateTime PostedAt { get; set; }
    public IReadOnlyList<string> Skills { get; set; } = Array.Empty<string>();
    public double RelevanceScore { get; set; }
    public string? ExternalApplyUrl { get; set; }
    public long SearchDurationMs { get; set; }
    public string? SourceName { get; set; }
    public bool IsVerified { get; set; }
    public int ApplicantsClickCount { get; set; }
}
