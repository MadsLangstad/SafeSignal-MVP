# SafeSignal MVP - Production Hardening Checklist

## Security Improvements

### 1. MinIO SSL/TLS Configuration
**Status**: ⚠️ Currently using HTTP (UseSSL: false)
**Priority**: HIGH
**Implementation**:
```yaml
# docker-compose.yml - Add MinIO SSL
environment:
  - MINIO_ROOT_USER=safesignal-admin
  - MINIO_ROOT_PASSWORD=${MINIO_ROOT_PASSWORD}  # Use env variable
  - MINIO_SERVER_URL=https://minio:9443
volumes:
  - ./certs/minio:/root/.minio/certs
```

```json
// appsettings.Production.json
{
  "Minio": {
    "Endpoint": "minio:9443",
    "UseSSL": true,
    "AccessKey": "${MINIO_ACCESS_KEY}",
    "SecretKey": "${MINIO_SECRET_KEY}"
  }
}
```

### 2. API Authentication
**Status**: ⚠️ No authentication on /api/clips POST endpoint
**Priority**: HIGH
**Recommendation**: Add API key or mTLS authentication

```csharp
// Add to Program.cs
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

app.MapPost("/api/clips/{clipRef}", async (string clipRef, IFormFile file) => {
    // ... implementation
}).RequireAuthorization();
```

### 3. Environment Variables for Secrets
**Status**: ⚠️ Hardcoded credentials in appsettings.json
**Priority**: HIGH
**Action Items**:
- Create `.env` file for local development (gitignored)
- Use Docker secrets or Azure Key Vault in production
- Remove hardcoded credentials from config files

```bash
# .env (DO NOT COMMIT)
MINIO_ROOT_USER=safesignal-admin
MINIO_ROOT_PASSWORD=<strong-random-password>
MINIO_ACCESS_KEY=<access-key>
MINIO_SECRET_KEY=<secret-key>
POSTGRES_PASSWORD=<database-password>
```

## Reliability Improvements

### 1. Circuit Breaker for MinIO
**Status**: ❌ Not implemented
**Priority**: MEDIUM
**Implementation**:

```bash
# Add Polly package
dotnet add package Polly
dotnet add package Microsoft.Extensions.Http.Polly
```

```csharp
// Add to Program.cs
builder.Services.AddHttpClient("MinIO")
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30)
        )
    )
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        )
    );
```

### 2. Graceful Degradation
**Status**: ⚠️ Partial - errors are logged but not handled gracefully
**Priority**: MEDIUM
**Recommendations**:
- Add fallback audio clips for critical alert types
- Implement local audio file cache
- Queue failed playback attempts for retry

### 3. Health Checks Enhancement
**Status**: ⚠️ Basic health check exists but doesn't verify dependencies
**Priority**: MEDIUM

```csharp
// Enhanced health checks
builder.Services.AddHealthChecks()
    .AddCheck("minio", () => {
        // Check MinIO connectivity
        return HealthCheckResult.Healthy();
    })
    .AddCheck("mqtt", () => {
        // Check MQTT broker connectivity
        return HealthCheckResult.Healthy();
    })
    .AddCheck("audio-clips", () => {
        // Verify critical audio clips exist
        return HealthCheckResult.Healthy();
    });
```

## Error Handling Improvements

### 1. Missing Audio Clip Handling
**Status**: ⚠️ Returns null but doesn't implement fallback
**Priority**: MEDIUM
**Recommendation**:

```csharp
// Add fallback audio logic in AudioPlaybackService
public async Task<byte[]?> GetAudioClipAsync(string clipRef)
{
    var audio = await _audioStorage.GetAudioClipAsync(clipRef);

    if (audio == null)
    {
        _logger.LogWarning("Audio clip {ClipRef} not found, using fallback", clipRef);

        // Try fallback clips
        var fallbacks = new[] { "EMERGENCY_ALERT", "GENERIC_ALARM" };
        foreach (var fallback in fallbacks)
        {
            audio = await _audioStorage.GetAudioClipAsync(fallback);
            if (audio != null) break;
        }
    }

    return audio;
}
```

### 2. Retry Logic for Transient Failures
**Status**: ❌ Not implemented
**Priority**: MEDIUM
**Use Polly policies** (see Circuit Breaker section above)

## Monitoring & Observability

### 1. Prometheus Metrics
**Status**: ✅ Enabled at /metrics endpoint
**Enhancements**:

```csharp
// Add custom metrics
private static readonly Counter AudioPlaybackCounter = Metrics
    .CreateCounter("pa_audio_playback_total", "Total audio playbacks",
        new CounterConfiguration
        {
            LabelNames = new[] { "clip_ref", "status" }
        });

private static readonly Histogram AudioPlaybackDuration = Metrics
    .CreateHistogram("pa_audio_playback_duration_seconds", "Audio playback duration");
```

### 2. Grafana Dashboards
**Status**: ✅ Dashboard exists
**Action**: Verify metrics are populating correctly

### 3. Alerting Rules
**Status**: ❌ Not configured
**Priority**: MEDIUM
**Recommendations**:

```yaml
# prometheus/alerts.yml
groups:
  - name: safesignal_alerts
    interval: 30s
    rules:
      - alert: MinIOConnectionFailure
        expr: up{job="minio"} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "MinIO is down"

      - alert: HighAudioPlaybackFailureRate
        expr: rate(pa_audio_playback_total{status="failed"}[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High audio playback failure rate"
```

## Configuration Management

### 1. Environment-Specific Configuration
**Status**: ⚠️ Single appsettings.json for all environments
**Priority**: MEDIUM
**Recommendation**:

```
appsettings.json               # Base settings
appsettings.Development.json   # Dev overrides
appsettings.Production.json    # Production settings
appsettings.Staging.json       # Staging settings
```

### 2. Configuration Validation
**Status**: ❌ Not implemented
**Priority**: LOW

```csharp
// Validate configuration on startup
builder.Services.AddOptions<MinioSettings>()
    .BindConfiguration("Minio")
    .ValidateDataAnnotations()
    .ValidateOnStart();

public class MinioSettings
{
    [Required]
    public string Endpoint { get; set; }

    [Required]
    public string BucketName { get; set; }

    // ... other properties with validation attributes
}
```

## Performance Optimizations

### 1. Audio Clip Caching
**Status**: ❌ Not implemented
**Priority**: LOW
**Benefit**: Reduce MinIO calls for frequently played clips

```csharp
// Add in-memory cache
builder.Services.AddMemoryCache();

// Implement in AudioStorageService
private readonly IMemoryCache _cache;

public async Task<byte[]?> GetAudioClipAsync(string clipRef)
{
    var cacheKey = $"audio:{clipRef}";

    if (_cache.TryGetValue(cacheKey, out byte[]? cachedAudio))
    {
        _logger.LogDebug("Cache hit for {ClipRef}", clipRef);
        return cachedAudio;
    }

    var audio = await DownloadFromMinIOAsync(clipRef);

    if (audio != null)
    {
        _cache.Set(cacheKey, audio, TimeSpan.FromHours(1));
    }

    return audio;
}
```

### 2. Connection Pooling
**Status**: ⚠️ Default MinIO client pooling
**Priority**: LOW
**Current**: Singleton MinioClient should handle this adequately for MVP

## Documentation

### 1. API Documentation
**Status**: ❌ Not documented
**Priority**: LOW
**Recommendation**: Add Swagger/OpenAPI

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

app.UseSwagger();
app.UseSwaggerUI();
```

### 2. Deployment Guide
**Status**: ⚠️ Basic README exists
**Priority**: MEDIUM
**Enhancement**: Add production deployment checklist, backup procedures, disaster recovery

## Testing

### 1. Integration Tests
**Status**: ❌ Not implemented
**Priority**: MEDIUM
**Recommendations**:
- Test MinIO connection and failover
- Test MQTT message handling
- Test audio playback pipeline end-to-end

### 2. Load Testing
**Status**: ❌ Not performed
**Priority**: LOW (for MVP)
**Tools**: k6, Apache JMeter

## Implementation Priority

### Phase 1 (Immediate - Before Production)
1. ✅ Move secrets to environment variables
2. ✅ Enable MinIO SSL/TLS
3. ✅ Add API authentication
4. ✅ Enhanced health checks

### Phase 2 (Short Term - Within 2 weeks)
1. Circuit breaker implementation
2. Fallback audio clip handling
3. Prometheus alerting rules
4. Environment-specific configuration

### Phase 3 (Medium Term - Within 1 month)
1. Audio clip caching
2. Integration tests
3. API documentation
4. Comprehensive deployment guide

### Phase 4 (Long Term - Future enhancements)
1. Load testing
2. Advanced monitoring dashboards
3. Automated backup/restore procedures
4. Multi-region deployment support

---

## Quick Win Implementations

The following can be implemented immediately with minimal effort:

### 1. Environment Variables (.env file)
```bash
# Create .env file
cat > edge/.env << 'EOF'
MINIO_ROOT_USER=safesignal-admin
MINIO_ROOT_PASSWORD=$(openssl rand -base64 32)
MINIO_ACCESS_KEY=safesignal-admin
MINIO_SECRET_KEY=$(openssl rand -base64 32)
POSTGRES_PASSWORD=$(openssl rand -base64 32)
EOF

# Update docker-compose.yml
env_file:
  - .env
```

### 2. API Key Authentication Middleware
```csharp
// Simple API key middleware (5 minutes)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/clips") &&
        context.Request.Method == "POST")
    {
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        var expectedKey = Environment.GetEnvironmentVariable("API_KEY");

        if (apiKey != expectedKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }
    }

    await next();
});
```

### 3. Basic Circuit Breaker (15 minutes)
```bash
cd edge/pa-service
dotnet add package Polly
```

```csharp
// Add to AudioStorageService
private int _consecutiveFailures = 0;
private DateTime _circuitOpenedAt = DateTime.MinValue;
private const int FAILURE_THRESHOLD = 3;
private static readonly TimeSpan CIRCUIT_TIMEOUT = TimeSpan.FromSeconds(30);

private bool IsCircuitOpen()
{
    if (_consecutiveFailures >= FAILURE_THRESHOLD)
    {
        if (DateTime.UtcNow - _circuitOpenedAt < CIRCUIT_TIMEOUT)
        {
            _logger.LogWarning("Circuit breaker is OPEN - MinIO calls blocked");
            return true;
        }

        // Reset after timeout
        _consecutiveFailures = 0;
    }

    return false;
}
```
