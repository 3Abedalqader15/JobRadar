using JobRadar.Application.Features.JobApplications.Queries.GetAllApplications;
using JobRadar.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Features.JobApplications;

public class GetAllApplicationsQueryHandler : IRequestHandler<GetAllApplicationsQuery, GetAllApplicationsResponse>
{
    private readonly AppDbContext _db;

    public GetAllApplicationsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GetAllApplicationsResponse> Handle(GetAllApplicationsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.UserJobApplications
            .Include(a => a.Job)
            .Include(a => a.User)
            .AsQueryable();

        if (request.JobId.HasValue)
            query = query.Where(a => a.JobId == request.JobId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.AppliedAt)
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
                a.AppliedAt
            ))
            .ToListAsync(cancellationToken);

        return new GetAllApplicationsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
