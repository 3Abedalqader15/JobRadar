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

    private readonly string _localUploadDirectory = Path.Combine(AppContext.BaseDirectory, "local_uploads");

    private bool HasValidR2Credentials =>
        !string.IsNullOrWhiteSpace(_settings.AccessKey) &&
        _settings.AccessKey.Length == 32 &&
        !_settings.AccessKey.Contains('<') &&
        !string.IsNullOrWhiteSpace(_settings.SecretKey) &&
        !_settings.SecretKey.Contains('<');

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

        if (HasValidR2Credentials)
        {
            try
            {
                using var s3Client = CreateS3Client();
                var putRequest = new PutObjectRequest
                {
                    BucketName = _settings.BucketName,
                    Key = fileKey,
                    InputStream = content,
                    ContentType = contentType,
                    DisablePayloadSigning = true
                };

                await s3Client.PutObjectAsync(putRequest, cancellationToken);
                _logger.LogInformation("Successfully uploaded CV to R2 bucket '{Bucket}' with key '{FileKey}'", _settings.BucketName, fileKey);
                return fileKey;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload to R2 bucket. Falling back to local disk storage for key '{FileKey}'", fileKey);
            }
        }
        else
        {
            _logger.LogWarning("R2 storage credentials are invalid or placeholder (AccessKey length: {Length}, expected 32). Storing locally.", _settings.AccessKey?.Length ?? 0);
        }

        // Local storage fallback
        var localFilePath = Path.Combine(_localUploadDirectory, fileKey.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Stored file locally at '{Path}' with key '{FileKey}'", localFilePath, fileKey);
        return fileKey;
    }

    public async Task<Stream?> DownloadAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        var localFilePath = Path.Combine(_localUploadDirectory, fileKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localFilePath))
        {
            var memory = new MemoryStream();
            using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await fileStream.CopyToAsync(memory, cancellationToken);
            }
            memory.Position = 0;
            return memory;
        }

        if (!HasValidR2Credentials)
        {
            return null;
        }

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
        var localFilePath = Path.Combine(_localUploadDirectory, fileKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localFilePath))
        {
            File.Delete(localFilePath);
            _logger.LogInformation("Deleted local file '{Path}'", localFilePath);
        }

        if (!HasValidR2Credentials)
        {
            return;
        }

        using var s3Client = CreateS3Client();
        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            await s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("Successfully deleted CV key '{FileKey}' from R2 bucket '{Bucket}'", fileKey, _settings.BucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting file '{FileKey}' from R2", fileKey);
        }
    }

    public Task<string> GeneratePreSignedUrlAsync(string fileKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        if (!HasValidR2Credentials)
        {
            return Task.FromResult($"/api/jobs/applications/cv/{fileKey}");
        }

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
