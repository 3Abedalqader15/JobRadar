namespace JobRadar.Application.Abstractions;

public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file stream to storage and returns the generated unique storage file key.
    /// </summary>
    Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the file stream directly from storage for server-side processing (e.g. AI parsing).
    /// </summary>
    Task<Stream?> DownloadAsync(string fileKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage by its key.
    /// </summary>
    Task DeleteAsync(string fileKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited pre-signed URL for direct, secure download by authorized clients.
    /// </summary>
    Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan expiration, CancellationToken cancellationToken = default);
}
