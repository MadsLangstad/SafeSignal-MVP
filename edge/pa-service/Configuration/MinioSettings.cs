namespace SafeSignal.Edge.PaService.Configuration;

/// <summary>
/// MinIO configuration settings
/// </summary>
public class MinioSettings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "safesignal-audio";
    public bool UseSSL { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
}
