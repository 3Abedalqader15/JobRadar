using JobRadar.Domain.Enums;

namespace JobRadar.Application.Models;

public sealed record DiscoveredJobDto(
    string Title,
    string CompanyName,
    string Description,
    string? Location,
    bool IsRemote,
    EmploymentType EmploymentType,
    ExperienceLevel ExperienceLevel,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string ExternalApplyUrl,
    DateTime? PostedAt,
    IReadOnlyList<string> Skills,
    string ProviderName
);
