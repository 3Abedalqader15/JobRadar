using Amazon.S3;
using Amazon.S3.Model;
using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Infrastructure.Services.Storage;

public class R2FileStorageService : IFileStorageService
{
    private readonly R2StorageSettings _settings;
    private readonly ILogger<R2FileStorageService> _logger;

    public R2FileStorageService(IOptions<R2StorageSettings> options, ILogger<R2FileStorageService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    private IAmazonS3 CreateS3Client()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true
        };
        return new AmazonS3Client(_settings.AccessKey, _settings.SecretKey, config);
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var fileKey = $"cvs/{Guid.NewGuid():N}{extension}";

        using var s3Client = CreateS3Client();
        var putRequest = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = fileKey,
            InputStream = content,
            ContentType = contentType
        };

        await s3Client.PutObjectAsync(putRequest, cancellationToken);
        _logger.LogInformation("Successfully uploaded CV to R2 bucket '{Bucket}' with key '{FileKey}'", _settings.BucketName, fileKey);

        return fileKey;
    }

    public async Task<Stream?> DownloadAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        using var s3Client = CreateS3Client();
        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            var response = await s3Client.GetObjectAsync(getRequest, cancellationToken);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File key '{FileKey}' not found in R2 bucket '{Bucket}'", fileKey, _settings.BucketName);
            return null;
        }
    }

    public async Task DeleteAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        using var s3Client = CreateS3Client();
        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = fileKey
        };

        await s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
        _logger.LogInformation("Successfully deleted CV key '{FileKey}' from R2 bucket '{Bucket}'", fileKey, _settings.BucketName);
    }

    public Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        using var s3Client = CreateS3Client();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = fileKey,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        string url = s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }
}
