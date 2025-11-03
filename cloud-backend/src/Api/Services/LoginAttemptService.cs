using Microsoft.Extensions.Caching.Memory;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Services;

/// <summary>
/// Implements brute force protection by tracking failed login attempts and locking accounts.
/// Configuration:
/// - MaxFailedAttempts: 5 (configurable)
/// - LockoutDurationMinutes: 15 (configurable)
/// - AttemptWindowMinutes: 15 (rolling window for counting attempts)
/// </summary>
public class LoginAttemptService : ILoginAttemptService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LoginAttemptService> _logger;
    private readonly IConfiguration _configuration;

    // Configuration with secure defaults
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutDurationMinutes;
    private readonly int _attemptWindowMinutes;

    public LoginAttemptService(
        IMemoryCache cache,
        ILogger<LoginAttemptService> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        _configuration = configuration;

        // Load configuration with secure defaults
        var section = _configuration.GetSection("BruteForceProtection");
        _maxFailedAttempts = section.GetValue("MaxFailedAttempts", 5);
        _lockoutDurationMinutes = section.GetValue("LockoutDurationMinutes", 15);
        _attemptWindowMinutes = section.GetValue("AttemptWindowMinutes", 15);

        _logger.LogInformation(
            "LoginAttemptService initialized: MaxAttempts={MaxAttempts}, LockoutMinutes={LockoutMinutes}, WindowMinutes={WindowMinutes}",
            _maxFailedAttempts, _lockoutDurationMinutes, _attemptWindowMinutes);
    }

    public async Task RecordFailedAttemptAsync(string email, string ipAddress)
    {
        var normalizedEmail = NormalizeEmail(email);
        var attemptsKey = GetAttemptsKey(normalizedEmail);
        var lockoutKey = GetLockoutKey(normalizedEmail);

        // Get current attempts
        if (!_cache.TryGetValue<List<LoginAttempt>>(attemptsKey, out var attempts) || attempts == null)
        {
            attempts = new List<LoginAttempt>();
        }

        // Add new attempt
        var attempt = new LoginAttempt
        {
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress
        };
        attempts.Add(attempt);

        // Remove attempts outside the window
        var windowStart = DateTime.UtcNow.AddMinutes(-_attemptWindowMinutes);
        attempts = attempts.Where(a => a.Timestamp > windowStart).ToList();

        // Store updated attempts
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_attemptWindowMinutes)
        };
        _cache.Set(attemptsKey, attempts, cacheOptions);

        // Check if we need to lock the account
        if (attempts.Count >= _maxFailedAttempts)
        {
            var lockoutExpiry = DateTime.UtcNow.AddMinutes(_lockoutDurationMinutes);
            var lockoutCacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = lockoutExpiry
            };
            _cache.Set(lockoutKey, lockoutExpiry, lockoutCacheOptions);

            _logger.LogWarning(
                "Account locked due to {AttemptCount} failed attempts: Email={Email}, IP={IpAddress}, LockoutUntil={LockoutExpiry}",
                attempts.Count, normalizedEmail, ipAddress, lockoutExpiry);
        }
        else
        {
            _logger.LogWarning(
                "Failed login attempt {AttemptCount}/{MaxAttempts}: Email={Email}, IP={IpAddress}",
                attempts.Count, _maxFailedAttempts, normalizedEmail, ipAddress);
        }

        await Task.CompletedTask;
    }

    public async Task ResetFailedAttemptsAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var attemptsKey = GetAttemptsKey(normalizedEmail);
        var lockoutKey = GetLockoutKey(normalizedEmail);

        _cache.Remove(attemptsKey);
        _cache.Remove(lockoutKey);

        _logger.LogInformation("Reset failed attempts for email: {Email}", normalizedEmail);

        await Task.CompletedTask;
    }

    public async Task<bool> IsAccountLockedAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var lockoutKey = GetLockoutKey(normalizedEmail);

        var isLocked = _cache.TryGetValue<DateTime>(lockoutKey, out var lockoutExpiry) && lockoutExpiry > DateTime.UtcNow;

        await Task.CompletedTask;
        return isLocked;
    }

    public async Task<int> GetLockoutRemainingSecondsAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var lockoutKey = GetLockoutKey(normalizedEmail);

        if (_cache.TryGetValue<DateTime>(lockoutKey, out var lockoutExpiry) && lockoutExpiry > DateTime.UtcNow)
        {
            var remainingSeconds = (int)(lockoutExpiry - DateTime.UtcNow).TotalSeconds;
            await Task.CompletedTask;
            return remainingSeconds;
        }

        await Task.CompletedTask;
        return 0;
    }

    public async Task<int> GetFailedAttemptsCountAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var attemptsKey = GetAttemptsKey(normalizedEmail);

        if (_cache.TryGetValue<List<LoginAttempt>>(attemptsKey, out var attempts) && attempts != null)
        {
            // Count only attempts within the window
            var windowStart = DateTime.UtcNow.AddMinutes(-_attemptWindowMinutes);
            var count = attempts.Count(a => a.Timestamp > windowStart);
            await Task.CompletedTask;
            return count;
        }

        await Task.CompletedTask;
        return 0;
    }

    private static string NormalizeEmail(string email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string GetAttemptsKey(string email)
    {
        return $"login_attempts:{email}";
    }

    private static string GetLockoutKey(string email)
    {
        return $"account_lockout:{email}";
    }

    private class LoginAttempt
    {
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
    }
}
