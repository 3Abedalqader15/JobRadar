using JobRadar.Application.Features.JobApplications.Queries.GetAllApplications;
using JobRadar.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Features.JobApplications;

public class GetAllApplicationsQueryHandler : IRequestHandler<GetAllApplicationsQuery, GetAllApplicationsResponse>
{
    private readonly AppDbContext _db;
    private readonly JobRadar.Application.Abstractions.ICurrentUserService _currentUserService;

    public GetAllApplicationsQueryHandler(
        AppDbContext db,
        JobRadar.Application.Abstractions.ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<GetAllApplicationsResponse> Handle(GetAllApplicationsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.IsHr && !_currentUserService.CompanyId.HasValue)
        {
            return new GetAllApplicationsResponse(new List<ApplicationSummaryDto>(), 0, request.Page, request.PageSize);
        }

        var query = _db.UserJobApplications
            .Include(a => a.Job)
            .Include(a => a.User)
            .AsQueryable();

        if (_currentUserService.IsHr)
        {
            var companyId = _currentUserService.CompanyId!.Value;
            query = query.Where(a => a.Job != null && a.Job.CompanyId == companyId);
        }

        if (request.JobId.HasValue)
            query = query.Where(a => a.JobId == request.JobId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting by request.SortBy
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "score_desc" => query.OrderByDescending(a => a.AiMatchScore.HasValue)
                                 .ThenByDescending(a => a.AiMatchScore)
                                 .ThenByDescending(a => a.AppliedAt),
            "score_asc" => query.OrderByDescending(a => a.AiMatchScore.HasValue)
                                .ThenBy(a => a.AiMatchScore)
                                .ThenByDescending(a => a.AppliedAt),
            "date_asc" => query.OrderBy(a => a.AppliedAt),
            "status" => query.OrderBy(a => a.Status).ThenByDescending(a => a.AppliedAt),
            _ => query.OrderByDescending(a => a.AppliedAt) // default: date_desc
        };

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ApplicationSummaryDto(
                a.Id,
                a.JobId,
                a.Job!.Title,
                a.Job!.CompanyName,
                a.UserId,
                a.User!.Email!,
                a.User!.FullName,
                a.Status.ToString(),
                a.AppliedAt,
                string.IsNullOrEmpty(a.ApplicantFullName) ? a.User!.FullName : a.ApplicantFullName,
                string.IsNullOrEmpty(a.ApplicantEmail) ? a.User!.Email! : a.ApplicantEmail,
                a.ApplicantPhone,
                a.CvOriginalFileName,
                a.CvFilePath,
                a.AiMatchScore,
                a.AiAnalysisStatus.ToString(),
                a.AiMissingKeywords,
                a.AiAnalysisSummary,
                a.AiMissingKeywordEvidence,
                a.AiScoreBreakdown,
                a.SuspiciousInstructionsDetected,
                a.AnalysisPromptVersion
            ))
            .ToListAsync(cancellationToken);

        return new GetAllApplicationsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
