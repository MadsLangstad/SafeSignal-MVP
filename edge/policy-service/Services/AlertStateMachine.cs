using SafeSignal.Edge.PolicyService.Models;
using Prometheus;

namespace SafeSignal.Edge.PolicyService.Services;

/// <summary>
/// Alert Finite State Machine (FSM) - Core policy engine
/// Enforces safety invariants: never audible in source room, no loops, deterministic routing
/// </summary>
public class AlertStateMachine
{
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<AlertStateMachine> _logger;

    // Prometheus metrics
    private static readonly Histogram AlertLatency = Metrics.CreateHistogram(
        "alert_trigger_latency_seconds",
        "Time from trigger received to PA command sent",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 10) // 100ms to ~51s
        });

    private static readonly Counter AlertsProcessedTotal = Metrics.CreateCounter(
        "alerts_processed_total",
        "Total number of alerts processed",
        new CounterConfiguration
        {
            LabelNames = new[] { "state" }
        });

    private static readonly Counter AlertsRejectedTotal = Metrics.CreateCounter(
        "alerts_rejected_total",
        "Total number of alerts rejected",
        new CounterConfiguration
        {
            LabelNames = new[] { "reason" }
        });

    // Mock building topology (MVP - will load from SQLite in production)
    private static readonly Dictionary<string, List<string>> BuildingRooms = new()
    {
        { "building-a", new List<string> { "room-1", "room-2", "room-3", "room-4" } },
        { "building-b", new List<string> { "room-101", "room-102", "room-103" } },
    };

    public AlertStateMachine(DeduplicationService dedupService, ILogger<AlertStateMachine> logger)
    {
        _dedupService = dedupService;
        _logger = logger;
    }

    /// <summary>
    /// Process alert trigger through FSM pipeline
    /// Returns null if alert should be rejected
    /// </summary>
    public async Task<AlertEvent?> ProcessTrigger(AlertTrigger trigger, DateTimeOffset receivedAt)
    {
        using var _ = AlertLatency.NewTimer();

        // State 1: Validation
        var validated = await ValidateTrigger(trigger);
        if (!validated)
        {
            AlertsProcessedTotal.WithLabels("rejected").Inc();
            return null;
        }
        AlertsProcessedTotal.WithLabels("validated").Inc();

        // State 2: Anti-Replay Check
        var replayCheck = CheckAntiReplay(trigger);
        if (!replayCheck)
        {
            AlertsRejectedTotal.WithLabels("replay").Inc();
            _logger.LogWarning("Alert rejected: Anti-replay check failed. AlertId={AlertId}", trigger.AlertId);
            return null;
        }

        // State 3: Deduplication
        if (_dedupService.IsDuplicate(trigger.TenantId, trigger.BuildingId, trigger.SourceRoomId, trigger.Mode))
        {
            AlertsRejectedTotal.WithLabels("duplicate").Inc();
            _logger.LogInformation("Alert rejected: Duplicate detected. AlertId={AlertId}", trigger.AlertId);
            return null;
        }

        // State 4: Policy Evaluation - Determine target rooms
        var targetRooms = EvaluatePolicy(trigger);
        if (targetRooms.Count == 0)
        {
            AlertsRejectedTotal.WithLabels("no_targets").Inc();
            _logger.LogWarning("Alert rejected: No target rooms after policy evaluation. AlertId={AlertId}",
                trigger.AlertId);
            return null;
        }

        // State 5: Create Alert Event
        var alertEvent = new AlertEvent
        {
            AlertId = trigger.AlertId,
            TenantId = trigger.TenantId,
            BuildingId = trigger.BuildingId,
            SourceRoomId = trigger.SourceRoomId,
            CausalChainId = trigger.CausalChainId,
            Mode = trigger.Mode,
            State = AlertState.PolicyEvaluated,
            ReceivedAt = receivedAt,
            ProcessedAt = DateTimeOffset.UtcNow,
            TargetRooms = targetRooms,
            Metadata = new Dictionary<string, string>
            {
                { "origin", trigger.Origin },
                { "sourceDeviceId", trigger.SourceDeviceId }
            }
        };

        AlertsProcessedTotal.WithLabels("policy_evaluated").Inc();

        _logger.LogInformation(
            "Alert processed: AlertId={AlertId}, SourceRoom={SourceRoom}, TargetRooms={TargetCount}, " +
            "LatencyMs={LatencyMs}",
            alertEvent.AlertId,
            alertEvent.SourceRoomId,
            alertEvent.TargetRooms.Count,
            (alertEvent.ProcessedAt - alertEvent.ReceivedAt).TotalMilliseconds);

        return alertEvent;
    }

    /// <summary>
    /// Validate alert trigger basic structure and required fields
    /// </summary>
    private Task<bool> ValidateTrigger(AlertTrigger trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger.AlertId))
        {
            _logger.LogError("Validation failed: AlertId is null or empty");
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(trigger.TenantId) ||
            string.IsNullOrWhiteSpace(trigger.BuildingId) ||
            string.IsNullOrWhiteSpace(trigger.SourceRoomId))
        {
            _logger.LogError("Validation failed: Missing required fields. AlertId={AlertId}", trigger.AlertId);
            return Task.FromResult(false);
        }

        if (!DateTimeOffset.TryParse(trigger.Timestamp, out _))
        {
            _logger.LogError("Validation failed: Invalid timestamp format. AlertId={AlertId}", trigger.AlertId);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Anti-replay check: ensure timestamp is within ±30s window
    /// In production, also check nonce against cache
    /// </summary>
    private bool CheckAntiReplay(AlertTrigger trigger)
    {
        if (!DateTimeOffset.TryParse(trigger.Timestamp, out var timestamp))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var delta = Math.Abs((now - timestamp).TotalSeconds);

        if (delta > 30)
        {
            _logger.LogWarning(
                "Anti-replay: Timestamp outside ±30s window. AlertId={AlertId}, DeltaSeconds={Delta}",
                trigger.AlertId, delta);
            return false;
        }

        // TODO: Check nonce against cache in production
        // For MVP, timestamp check is sufficient

        return true;
    }

    /// <summary>
    /// Evaluate policy and determine target rooms
    /// CRITICAL INVARIANT: Never include source room in target list
    /// </summary>
    private List<string> EvaluatePolicy(AlertTrigger trigger)
    {
        // Get all rooms in building
        if (!BuildingRooms.TryGetValue(trigger.BuildingId, out var allRooms))
        {
            _logger.LogWarning(
                "Building not found in topology: BuildingId={BuildingId}, AlertId={AlertId}",
                trigger.BuildingId, trigger.AlertId);

            // Fail-safe: return empty list
            return new List<string>();
        }

        // CRITICAL SAFETY INVARIANT: Exclude source room (never audible in source room)
        var targetRooms = allRooms
            .Where(room => room != trigger.SourceRoomId)
            .ToList();

        _logger.LogInformation(
            "Policy evaluation: Building={Building}, TotalRooms={Total}, SourceRoom={Source}, TargetRooms={Targets}",
            trigger.BuildingId, allRooms.Count, trigger.SourceRoomId, targetRooms.Count);

        // Log explicit exclusion for audit trail
        if (targetRooms.Count < allRooms.Count)
        {
            _logger.LogInformation(
                "SOURCE ROOM EXCLUDED (Safety Invariant): Room={SourceRoom}, AlertId={AlertId}",
                trigger.SourceRoomId, trigger.AlertId);
        }

        return targetRooms;
    }
}
