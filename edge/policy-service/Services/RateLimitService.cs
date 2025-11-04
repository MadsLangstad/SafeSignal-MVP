using System.Collections.Concurrent;
using Prometheus;

namespace SafeSignal.Edge.PolicyService.Services;

/// <summary>
/// Token Bucket Rate Limiting Service
///
/// Implements per-device and per-tenant rate limiting using token bucket algorithm.
/// Prevents DoS attacks and excessive alert spamming.
/// </summary>
public class RateLimitService
{
    private readonly ILogger<RateLimitService> _logger;
    private readonly IConfiguration _configuration;

    // Token buckets: Key = deviceId or tenantId, Value = bucket state
    private readonly ConcurrentDictionary<string, TokenBucket> _deviceBuckets = new();
    private readonly ConcurrentDictionary<string, TokenBucket> _tenantBuckets = new();

    // Configuration (loaded from appsettings.json)
    private readonly int _deviceCapacity;
    private readonly double _deviceRefillRate;
    private readonly int _tenantCapacity;
    private readonly double _tenantRefillRate;
    private readonly int _cooldownSeconds;

    // Prometheus metrics
    private static readonly Counter RateLimitChecksTotal = Metrics.CreateCounter(
        "rate_limit_checks_total",
        "Total rate limit checks performed",
        new CounterConfiguration
        {
            LabelNames = new[] { "scope", "result" }
        });

    private static readonly Gauge RateLimitedDevicesActive = Metrics.CreateGauge(
        "rate_limited_devices_active",
        "Number of devices currently rate limited");

    private static readonly Gauge RateLimitedTenantsActive = Metrics.CreateGauge(
        "rate_limited_tenants_active",
        "Number of tenants currently rate limited");

    public RateLimitService(
        ILogger<RateLimitService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Load configuration with defaults
        _deviceCapacity = int.Parse(configuration["RateLimit:DeviceCapacity"] ?? "10");
        _deviceRefillRate = double.Parse(configuration["RateLimit:DeviceRefillRate"] ?? "0.0167"); // ~1 per minute
        _tenantCapacity = int.Parse(configuration["RateLimit:TenantCapacity"] ?? "100");
        _tenantRefillRate = double.Parse(configuration["RateLimit:TenantRefillRate"] ?? "0.167"); // ~10 per minute
        _cooldownSeconds = int.Parse(configuration["RateLimit:CooldownSeconds"] ?? "300");

        _logger.LogInformation("Rate limiting initialized:");
        _logger.LogInformation("  Device: capacity={Capacity}, refill={Refill}/s",
            _deviceCapacity, _deviceRefillRate);
        _logger.LogInformation("  Tenant: capacity={Capacity}, refill={Refill}/s",
            _tenantCapacity, _tenantRefillRate);
        _logger.LogInformation("  Cooldown: {Cooldown}s", _cooldownSeconds);
    }

    /// <summary>
    /// Check if alert is allowed for device and tenant
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="tenantId">Tenant identifier</param>
    /// <returns>True if allowed, false if rate limited</returns>
    public bool CheckAlert(string deviceId, string tenantId)
    {
        // Check device-level rate limit
        var deviceBucket = _deviceBuckets.GetOrAdd(deviceId,
            _ => new TokenBucket(_deviceCapacity, _deviceRefillRate, _cooldownSeconds));

        if (!deviceBucket.TryConsume())
        {
            _logger.LogWarning(
                "⚠️  Device rate limit exceeded: Device={DeviceId}, Tokens={Tokens}/{Capacity}",
                deviceId, deviceBucket.Tokens, _deviceCapacity);

            RateLimitChecksTotal.WithLabels("device", "blocked").Inc();
            UpdateActiveRateLimits();
            return false;
        }

        // Check tenant-level rate limit
        var tenantBucket = _tenantBuckets.GetOrAdd(tenantId,
            _ => new TokenBucket(_tenantCapacity, _tenantRefillRate, _cooldownSeconds));

        if (!tenantBucket.TryConsume())
        {
            // Refund device token since tenant limit was hit
            deviceBucket.Refund();

            _logger.LogWarning(
                "⚠️  Tenant rate limit exceeded: Tenant={TenantId}, Tokens={Tokens}/{Capacity}",
                tenantId, tenantBucket.Tokens, _tenantCapacity);

            RateLimitChecksTotal.WithLabels("tenant", "blocked").Inc();
            UpdateActiveRateLimits();
            return false;
        }

        RateLimitChecksTotal.WithLabels("combined", "allowed").Inc();
        return true;
    }

    /// <summary>
    /// Get rate limit status for device
    /// </summary>
    public RateLimitStatus GetDeviceStatus(string deviceId)
    {
        if (!_deviceBuckets.TryGetValue(deviceId, out var bucket))
        {
            return new RateLimitStatus
            {
                Scope = "device",
                Identifier = deviceId,
                TokensRemaining = _deviceCapacity,
                Capacity = _deviceCapacity,
                IsLimited = false
            };
        }

        bucket.Refill(); // Update tokens before checking status

        return new RateLimitStatus
        {
            Scope = "device",
            Identifier = deviceId,
            TokensRemaining = (int)bucket.Tokens,
            Capacity = _deviceCapacity,
            IsLimited = bucket.InCooldown,
            CooldownUntil = bucket.CooldownUntil
        };
    }

    /// <summary>
    /// Get rate limit status for tenant
    /// </summary>
    public RateLimitStatus GetTenantStatus(string tenantId)
    {
        if (!_tenantBuckets.TryGetValue(tenantId, out var bucket))
        {
            return new RateLimitStatus
            {
                Scope = "tenant",
                Identifier = tenantId,
                TokensRemaining = _tenantCapacity,
                Capacity = _tenantCapacity,
                IsLimited = false
            };
        }

        bucket.Refill(); // Update tokens before checking status

        return new RateLimitStatus
        {
            Scope = "tenant",
            Identifier = tenantId,
            TokensRemaining = (int)bucket.Tokens,
            Capacity = _tenantCapacity,
            IsLimited = bucket.InCooldown,
            CooldownUntil = bucket.CooldownUntil
        };
    }

    /// <summary>
    /// Reset rate limits for device (for testing/admin)
    /// </summary>
    public void ResetDevice(string deviceId)
    {
        _deviceBuckets.TryRemove(deviceId, out _);
        _logger.LogInformation("Rate limit reset for device: {DeviceId}", deviceId);
        UpdateActiveRateLimits();
    }

    /// <summary>
    /// Reset rate limits for tenant (for testing/admin)
    /// </summary>
    public void ResetTenant(string tenantId)
    {
        _tenantBuckets.TryRemove(tenantId, out _);
        _logger.LogInformation("Rate limit reset for tenant: {TenantId}", tenantId);
        UpdateActiveRateLimits();
    }

    /// <summary>
    /// Update Prometheus metrics for active rate limits
    /// </summary>
    private void UpdateActiveRateLimits()
    {
        int devicesLimited = _deviceBuckets.Count(kvp => kvp.Value.InCooldown);
        int tenantsLimited = _tenantBuckets.Count(kvp => kvp.Value.InCooldown);

        RateLimitedDevicesActive.Set(devicesLimited);
        RateLimitedTenantsActive.Set(tenantsLimited);
    }
}

/// <summary>
/// Token Bucket Implementation
/// Classic algorithm for rate limiting with burst capacity and sustained rate
/// </summary>
internal class TokenBucket
{
    private readonly int _capacity;
    private readonly double _refillRate; // tokens per second
    private readonly int _cooldownSeconds;
    private double _tokens;
    private DateTimeOffset _lastRefill;
    private DateTimeOffset? _cooldownUntil;

    public double Tokens => _tokens;
    public bool InCooldown => _cooldownUntil.HasValue && DateTimeOffset.UtcNow < _cooldownUntil.Value;
    public DateTimeOffset? CooldownUntil => _cooldownUntil;

    public TokenBucket(int capacity, double refillRate, int cooldownSeconds)
    {
        _capacity = capacity;
        _refillRate = refillRate;
        _cooldownSeconds = cooldownSeconds;
        _tokens = capacity; // Start full
        _lastRefill = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Try to consume a token
    /// </summary>
    public bool TryConsume()
    {
        Refill();

        // Check cooldown
        if (InCooldown)
        {
            return false;
        }

        // Check tokens
        if (_tokens >= 1.0)
        {
            _tokens -= 1.0;
            return true;
        }

        // No tokens, enter cooldown
        _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(_cooldownSeconds);
        return false;
    }

    /// <summary>
    /// Refund a token (used when tenant limit hit after device limit passed)
    /// </summary>
    public void Refund()
    {
        _tokens = Math.Min(_tokens + 1.0, _capacity);
    }

    /// <summary>
    /// Refill tokens based on elapsed time
    /// </summary>
    public void Refill()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;

        // Add tokens based on refill rate
        _tokens = Math.Min(_tokens + (elapsed * _refillRate), _capacity);
        _lastRefill = now;

        // Clear cooldown if expired
        if (_cooldownUntil.HasValue && now >= _cooldownUntil.Value)
        {
            _cooldownUntil = null;
            // Refill to full capacity after cooldown
            _tokens = _capacity;
        }
    }
}

/// <summary>
/// Rate limit status information
/// </summary>
public class RateLimitStatus
{
    public string Scope { get; set; } = ""; // "device" or "tenant"
    public string Identifier { get; set; } = "";
    public int TokensRemaining { get; set; }
    public int Capacity { get; set; }
    public bool IsLimited { get; set; }
    public DateTimeOffset? CooldownUntil { get; set; }
}
