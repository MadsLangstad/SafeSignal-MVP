using System.Collections.Concurrent;
using Prometheus;

namespace SafeSignal.Edge.PolicyService.Services;

/// <summary>
/// Deduplication service prevents duplicate alert processing within a time window (300-800ms)
/// Implements in-memory cache with TTL for MVP (production will need Redis)
/// </summary>
public class DeduplicationService
{
    private readonly ConcurrentDictionary<string, DedupEntry> _cache = new();
    private readonly TimeSpan _dedupWindow = TimeSpan.FromMilliseconds(500); // 300-800ms window
    private readonly Timer _cleanupTimer;

    // Prometheus metrics
    private static readonly Counter DedupHitsTotal = Metrics.CreateCounter(
        "dedup_hits_total",
        "Total number of deduplicated alerts");

    private static readonly Gauge DedupCacheSize = Metrics.CreateGauge(
        "dedup_cache_size",
        "Current number of entries in deduplication cache");

    private readonly ILogger<DeduplicationService> _logger;

    public DeduplicationService(ILogger<DeduplicationService> logger)
    {
        _logger = logger;

        // Cleanup expired entries every 1 second
        _cleanupTimer = new Timer(CleanupExpiredEntries, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Check if alert is a duplicate and should be rejected
    /// </summary>
    public bool IsDuplicate(string tenantId, string buildingId, string sourceRoomId, string mode)
    {
        var key = GenerateKey(tenantId, buildingId, sourceRoomId, mode);
        var now = DateTimeOffset.UtcNow;

        // Try to get existing entry
        if (_cache.TryGetValue(key, out var existingEntry))
        {
            if (now - existingEntry.Timestamp <= _dedupWindow)
            {
                // Duplicate detected within time window
                DedupHitsTotal.Inc();

                _logger.LogWarning(
                    "Duplicate alert detected: Tenant={TenantId}, Building={BuildingId}, Room={SourceRoomId}, " +
                    "Mode={Mode}, OriginalTs={OriginalTs}, DeltaMs={DeltaMs}",
                    tenantId, buildingId, sourceRoomId, mode,
                    existingEntry.Timestamp,
                    (now - existingEntry.Timestamp).TotalMilliseconds);

                return true;
            }
        }

        // Not a duplicate, store this entry
        _cache[key] = new DedupEntry
        {
            Timestamp = now,
            TenantId = tenantId,
            BuildingId = buildingId,
            SourceRoomId = sourceRoomId,
            Mode = mode
        };

        DedupCacheSize.Set(_cache.Count);

        return false;
    }

    /// <summary>
    /// Generate cache key for deduplication
    /// </summary>
    private static string GenerateKey(string tenantId, string buildingId, string sourceRoomId, string mode)
    {
        return $"{tenantId}:{buildingId}:{sourceRoomId}:{mode}";
    }

    /// <summary>
    /// Cleanup expired cache entries
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var keysToRemove = _cache
                .Where(kvp => now - kvp.Value.Timestamp > _dedupWindow * 2) // Keep for 2x window for safety
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                DedupCacheSize.Set(_cache.Count);
                _logger.LogDebug("Cleaned up {Count} expired dedup entries", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dedup cache cleanup");
        }
    }

    private class DedupEntry
    {
        public required DateTimeOffset Timestamp { get; init; }
        public required string TenantId { get; init; }
        public required string BuildingId { get; init; }
        public required string SourceRoomId { get; init; }
        public required string Mode { get; init; }
    }
}
