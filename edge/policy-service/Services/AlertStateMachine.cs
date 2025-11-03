using SafeSignal.Edge.PolicyService.Models;
using SafeSignal.Edge.PolicyService.Data;
using Prometheus;

namespace SafeSignal.Edge.PolicyService.Services;

/// <summary>
/// Alert Finite State Machine (FSM) - Core policy engine
/// Enforces safety invariants: never audible in source room, no loops, deterministic routing
/// </summary>
public class AlertStateMachine
{
    private readonly DeduplicationService _dedupService;
    private readonly TopologyRepository _topologyRepository;
    private readonly AlertRepository _alertRepository;
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

    // Fallback building topology if database unavailable
    private static readonly Dictionary<string, List<string>> FallbackBuildingRooms = new()
    {
        { "building-a", new List<string> { "room-1", "room-2", "room-3", "room-4" } },
        { "building-b", new List<string> { "room-101", "room-102", "room-103" } },
    };

    public AlertStateMachine(
        DeduplicationService dedupService,
        TopologyRepository topologyRepository,
        AlertRepository alertRepository,
        ILogger<AlertStateMachine> logger)
    {
        _dedupService = dedupService;
        _topologyRepository = topologyRepository;
        _alertRepository = alertRepository;
        _logger = logger;
    }

    /// <summary>
    /// Process alert trigger through FSM pipeline
    /// Returns null if alert should be rejected
    /// </summary>
    public async Task<AlertEvent?> ProcessTrigger(AlertTrigger trigger, DateTimeOffset receivedAt)
    {
        using var _ = AlertLatency.NewTimer();

        // Persist alert to database (initial state)
        var alertRecord = new AlertRecord
        {
            AlertId = trigger.AlertId,
            TenantId = trigger.TenantId,
            BuildingId = trigger.BuildingId,
            SourceRoomId = trigger.SourceRoomId,
            SourceDeviceId = trigger.SourceDeviceId,
            Mode = trigger.Mode,
            Origin = trigger.Origin,
            CausalChainId = trigger.CausalChainId,
            CreatedAt = receivedAt.UtcDateTime,
            Status = "PENDING"
        };
        var insertSuccess = await _alertRepository.InsertAlertAsync(alertRecord);
        if (!insertSuccess)
        {
            _logger.LogError("Failed to persist alert to database: AlertId={AlertId}", trigger.AlertId);
            throw new InvalidOperationException($"Failed to persist alert {trigger.AlertId} to database");
        }

        // State 1: Validation
        var validated = await ValidateTrigger(trigger);
        if (!validated)
        {
            AlertsProcessedTotal.WithLabels("rejected").Inc();
            await _alertRepository.UpdateAlertStatusAsync(trigger.AlertId, "FAILED", errorMessage: "Validation failed");
            return null;
        }
        AlertsProcessedTotal.WithLabels("validated").Inc();

        // State 2: Anti-Replay Check
        var replayCheck = CheckAntiReplay(trigger);
        if (!replayCheck)
        {
            AlertsRejectedTotal.WithLabels("replay").Inc();
            _logger.LogWarning("Alert rejected: Anti-replay check failed. AlertId={AlertId}", trigger.AlertId);
            await _alertRepository.UpdateAlertStatusAsync(trigger.AlertId, "FAILED", errorMessage: "Anti-replay check failed");
            return null;
        }

        // State 3: Deduplication
        if (_dedupService.IsDuplicate(trigger.TenantId, trigger.BuildingId, trigger.SourceRoomId, trigger.Mode))
        {
            AlertsRejectedTotal.WithLabels("duplicate").Inc();
            _logger.LogInformation("Alert rejected: Duplicate detected. AlertId={AlertId}", trigger.AlertId);
            await _alertRepository.UpdateAlertStatusAsync(trigger.AlertId, "FAILED", errorMessage: "Duplicate detected");
            return null;
        }

        // State 4: Policy Evaluation - Determine target rooms
        var targetRooms = await EvaluatePolicy(trigger);
        if (targetRooms.Count == 0)
        {
            AlertsRejectedTotal.WithLabels("no_targets").Inc();
            _logger.LogWarning("Alert rejected: No target rooms after policy evaluation. AlertId={AlertId}",
                trigger.AlertId);
            await _alertRepository.UpdateAlertStatusAsync(trigger.AlertId, "FAILED", errorMessage: "No target rooms");
            return null;
        }

        // Update alert status to completed with target room count
        var updateSuccess = await _alertRepository.UpdateAlertStatusAsync(trigger.AlertId, "COMPLETED", processedAt: DateTime.UtcNow, targetRoomCount: targetRooms.Count);
        if (!updateSuccess)
        {
            _logger.LogError("Failed to update alert status to COMPLETED: AlertId={AlertId}", trigger.AlertId);
            throw new InvalidOperationException($"Failed to update status for alert {trigger.AlertId}");
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
    private async Task<List<string>> EvaluatePolicy(AlertTrigger trigger)
    {
        List<string> allRoomIds;

        // Try to get rooms from database
        try
        {
            var rooms = await _topologyRepository.GetRoomsByBuildingAsync(trigger.BuildingId);
            if (rooms.Any())
            {
                allRoomIds = rooms.Select(r => r.RoomId).ToList();
                _logger.LogInformation("Loaded {Count} rooms from database for building {BuildingId}: {RoomIds}",
                    allRoomIds.Count, trigger.BuildingId, string.Join(", ", allRoomIds));
            }
            else
            {
                _logger.LogWarning(
                    "No rooms found in database for building {BuildingId}, using fallback topology",
                    trigger.BuildingId);

                // Fallback to hardcoded topology
                if (!FallbackBuildingRooms.TryGetValue(trigger.BuildingId, out allRoomIds!))
                {
                    _logger.LogWarning(
                        "Building not found in fallback topology: BuildingId={BuildingId}, AlertId={AlertId}",
                        trigger.BuildingId, trigger.AlertId);
                    return new List<string>();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error loading topology, using fallback for building {BuildingId}",
                trigger.BuildingId);

            // Fallback to hardcoded topology on database error
            if (!FallbackBuildingRooms.TryGetValue(trigger.BuildingId, out allRoomIds!))
            {
                _logger.LogWarning(
                    "Building not found in fallback topology: BuildingId={BuildingId}, AlertId={AlertId}",
                    trigger.BuildingId, trigger.AlertId);
                return new List<string>();
            }
        }

        // CRITICAL SAFETY INVARIANT: Exclude source room (never audible in source room)
        var targetRooms = allRoomIds
            .Where(room => room != trigger.SourceRoomId)
            .ToList();

        _logger.LogInformation(
            "Policy evaluation: Building={Building}, TotalRooms={Total}, SourceRoom={Source}, TargetRooms={Targets}, Target List=[{TargetList}]",
            trigger.BuildingId, allRoomIds.Count, trigger.SourceRoomId, targetRooms.Count, string.Join(", ", targetRooms));

        // Log explicit exclusion for audit trail
        if (targetRooms.Count < allRoomIds.Count)
        {
            _logger.LogInformation(
                "SOURCE ROOM EXCLUDED (Safety Invariant): Room={SourceRoom}, AlertId={AlertId}",
                trigger.SourceRoomId, trigger.AlertId);
        }
        else
        {
            _logger.LogError(
                "CRITICAL BUG: Source room NOT excluded! TotalRooms={Total}, TargetRooms={Targets}, SourceRoom={SourceRoom}",
                allRoomIds.Count, targetRooms.Count, trigger.SourceRoomId);
        }

        return targetRooms;
    }
}
