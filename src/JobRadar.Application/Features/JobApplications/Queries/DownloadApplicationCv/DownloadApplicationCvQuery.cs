using MediatR;

namespace JobRadar.Application.Features.JobApplications.Queries.DownloadApplicationCv;

public sealed record CvDownloadResult(
    Stream FileStream,
    string ContentType,
    string FileName
);

public sealed record DownloadApplicationCvQuery(Guid ApplicationId) : IRequest<CvDownloadResult>;
