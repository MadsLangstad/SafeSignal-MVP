using Minio;
using Minio.DataModel.Args;
using SafeSignal.Edge.PaService.Configuration;
using System.Reactive.Linq;

namespace SafeSignal.Edge.PaService.Services;

/// <summary>
/// Service for managing audio clip storage in MinIO
/// </summary>
public class AudioStorageService : IDisposable
{
    private readonly IMinioClient _minioClient;
    private readonly MinioSettings _settings;
    private readonly ILogger<AudioStorageService> _logger;
    private bool _bucketInitialized;

    public AudioStorageService(
        MinioSettings settings,
        ILogger<AudioStorageService> logger)
    {
        _settings = settings;
        _logger = logger;

        _logger.LogInformation("Initializing MinIO client: Endpoint={Endpoint}, Bucket={Bucket}, UseSSL={UseSSL}",
            settings.Endpoint, settings.BucketName, settings.UseSSL);

        _minioClient = new MinioClient()
            .WithEndpoint(settings.Endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey)
            .WithSSL(settings.UseSSL)
            .Build();

        _logger.LogInformation("MinIO client initialized successfully");
    }

    /// <summary>
    /// Ensure the bucket exists, create if it doesn't
    /// </summary>
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_bucketInitialized)
            return;

        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_settings.BucketName);

            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);

            if (!found)
            {
                _logger.LogInformation("Bucket {Bucket} does not exist, creating...", _settings.BucketName);

                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_settings.BucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);

                _logger.LogInformation("Bucket {Bucket} created successfully", _settings.BucketName);
            }
            else
            {
                _logger.LogInformation("Bucket {Bucket} already exists", _settings.BucketName);
            }

            _bucketInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket {Bucket} exists", _settings.BucketName);
            throw;
        }
    }

    /// <summary>
    /// Upload an audio clip to MinIO
    /// </summary>
    public async Task<bool> UploadAudioClipAsync(
        string clipName,
        Stream audioStream,
        string contentType = "audio/wav",
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var objectName = $"clips/{clipName}";

            _logger.LogInformation("Uploading audio clip: {ObjectName}, Size={Size} bytes",
                objectName, audioStream.Length);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithStreamData(audioStream)
                .WithObjectSize(audioStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            _logger.LogInformation("Audio clip uploaded successfully: {ObjectName}", objectName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload audio clip {ClipName}", clipName);
            return false;
        }
    }

    /// <summary>
    /// Download an audio clip from MinIO
    /// </summary>
    public async Task<byte[]?> GetAudioClipAsync(
        string clipRef,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            // Support both "EMERGENCY_ALERT" and "clips/EMERGENCY_ALERT.wav" formats
            var objectName = clipRef.StartsWith("clips/")
                ? clipRef
                : $"clips/{clipRef}.wav";

            _logger.LogInformation("Downloading audio clip: {ObjectName}", objectName);

            using var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithCallbackStream((stream) =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);

            var audioData = memoryStream.ToArray();

            _logger.LogInformation("Audio clip downloaded: {ObjectName}, Size={Size} bytes",
                objectName, audioData.Length);

            return audioData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download audio clip {ClipRef}", clipRef);
            return null;
        }
    }

    /// <summary>
    /// List all available audio clips
    /// </summary>
    public async Task<List<string>> ListAvailableClipsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var clips = new List<string>();

            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(_settings.BucketName)
                .WithPrefix("clips/")
                .WithRecursive(true);

            // Use ListObjectsEnumAsync which returns IAsyncEnumerable
            await foreach (var item in _minioClient.ListObjectsEnumAsync(listObjectsArgs, cancellationToken))
            {
                var clipName = item.Key.Replace("clips/", "").Replace(".wav", "");
                clips.Add(clipName);
            }

            _logger.LogInformation("Found {Count} audio clips in bucket", clips.Count);
            return clips;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list audio clips");
            return new List<string>();
        }
    }

    /// <summary>
    /// Check if a clip exists
    /// </summary>
    public async Task<bool> ClipExistsAsync(
        string clipRef,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var objectName = clipRef.StartsWith("clips/")
                ? clipRef
                : $"clips/{clipRef}.wav";

            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _minioClient?.Dispose();
    }
}
