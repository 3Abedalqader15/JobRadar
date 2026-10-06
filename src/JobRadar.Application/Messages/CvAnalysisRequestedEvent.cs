namespace JobRadar.Application.Messages;

public sealed record CvAnalysisRequestedEvent(
    Guid ApplicationId,
    Guid JobId,
    string CvFilePath,
    string CvOriginalFileName,
    DateTime RequestedAt
);
