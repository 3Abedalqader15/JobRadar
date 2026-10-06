namespace JobRadar.Infrastructure.Services.Storage;

public class R2StorageSettings
{
    public const string SectionName = "Storage:R2";

    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}
