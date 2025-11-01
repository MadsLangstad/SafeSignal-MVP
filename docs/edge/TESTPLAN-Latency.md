# SafeSignal Edge - Latency Test Plan

**Version**: 1.0.0-MVP
**Date**: 2025-10-31
**Owner**: QA Team / Engineering

---

## Objective

Measure and validate end-to-end latency for the SafeSignal Edge alert trigger flow to ensure compliance with the P95 ≤2000ms SLO for local delivery (ESP32/App → Edge → PA).

---

## Scope

**In Scope**:
- Trigger reception via MQTT
- Policy evaluation (validation, dedup, FSM)
- PA command fan-out
- Simulated audio playback
- Metrics collection and reporting

**Out of Scope** (for MVP):
- Real ESP32 hardware (using test MQTT client)
- Real PA hardware (using TTS stub)
- Cloud escalation path (gRPC to cloud)
- Multi-tenant concurrent load testing

---

## Test Environment

### Configuration

| Component | Version/Config |
|-----------|----------------|
| EMQX | 5.4.0, mTLS enabled |
| Policy Service | .NET 9, QoS1 subscription |
| PA Service | .NET 9, TTS stub |
| Prometheus | v2.48.0, 5s scrape interval |
| Docker | ≥20.10 |
| OS | macOS/Linux (local development) |

### Test Data

- **Tenants**: `tenant-a`
- **Buildings**: `building-a` (4 rooms), `building-b` (3 rooms)
- **Alert Mode**: `AUDIBLE`
- **Sample Size**: 50 triggers per test run
- **Source Rooms**: Varied (`room-1` through `room-50`)

---

## Measurement Methodology

### Latency Definition

**End-to-end latency** = `t_playback_complete - t_trigger`

Where:
- `t_trigger`: Timestamp when MQTT PUBLISH is sent by test client
- `t_playback_complete`: Timestamp when PA service completes audio playback

### Measurement Points

1. **Test Client** (`send-test-trigger.sh`):
   - Records `t_trigger` before sending MQTT message
   - Records `t_complete` after receiving confirmation

2. **Policy Service** (internal timestamps):
   - `t_received`: Alert trigger received from EMQX
   - `t_validated`: After anti-replay and dedup checks
   - `t_policy_eval`: After room topology evaluation
   - `t_fanout_complete`: All PA commands sent

3. **PA Service** (internal timestamps):
   - `t_command_received`: PA command received
   - `t_tts_complete`: Audio generation complete
   - `t_playback_complete`: Audio playback complete

4. **Prometheus Histograms**:
   - `alert_trigger_latency_seconds`: Policy service internal metric
   - `pa_command_to_playback_latency_seconds`: PA service internal metric

### Calculation Method

**Primary Method** (Application-level):
- Use Prometheus histogram `alert_trigger_latency_seconds`
- Calculate P50, P95, P99 from histogram buckets
- Query: `histogram_quantile(0.95, sum(rate(alert_trigger_latency_seconds_bucket[5m])) by (le))`

**Secondary Method** (Test Script):
- Measure wall-clock time in `measure-latency.sh`
- Sort results and calculate percentiles directly
- Compare with Prometheus metrics for validation

---

## Test Cases

### TC1: Baseline Latency Measurement

**Objective**: Measure P95 latency under no-load conditions

**Preconditions**:
- All services healthy
- No concurrent alerts
- System idle for 30 seconds

**Test Steps**:
1. Start all services: `docker-compose up -d`
2. Wait for health checks to pass
3. Run: `./measure-latency.sh 50`
4. Collect results

**Expected Results**:
- P95 ≤2000ms
- P50 ≤1000ms
- Success rate ≥99%

**Acceptance Criteria**:
- ✅ P95 latency ≤2000ms
- ✅ All 50 triggers processed successfully
- ✅ No errors in service logs

---

### TC2: Source Room Exclusion Validation

**Objective**: Verify source room is never included in PA commands

**Test Steps**:
1. Send trigger from `room-test`
2. Monitor PA service logs
3. Verify `room-test` does NOT appear in playback logs

**Expected Results**:
- PA service receives commands for all rooms EXCEPT `room-test`
- Policy service logs show: `SOURCE ROOM EXCLUDED (Safety Invariant): Room=room-test`

**Acceptance Criteria**:
- ✅ Source room explicitly excluded (logged)
- ✅ PA service never plays audio in source room
- ✅ Alert still delivered to other rooms

**Failure Condition**: ❌ If `room-test` appears in PA playback logs → **CRITICAL BUG**

---

### TC3: Deduplication Effectiveness

**Objective**: Measure deduplication within 300-800ms window

**Test Steps**:
1. Send 5 identical triggers with 100ms delay between each
2. Monitor policy service dedup metrics
3. Verify only first trigger is processed

**Expected Results**:
- First trigger processed
- Triggers 2-5 rejected as duplicates
- `dedup_hits_total` counter = 4

**Acceptance Criteria**:
- ✅ Dedup detected within 500ms window (avg)
- ✅ Duplicate triggers logged with delta timestamp
- ✅ No duplicate PA commands sent

---

### TC4: Concurrent Alert Handling

**Objective**: Measure latency with concurrent alerts from different rooms

**Test Steps**:
1. Send 10 alerts simultaneously from different rooms (parallel script execution)
2. Measure P95 latency
3. Verify all alerts processed

**Expected Results**:
- P95 latency ≤2500ms (acceptable degradation under concurrent load)
- All 10 alerts processed successfully
- No dropped messages

**Acceptance Criteria**:
- ✅ P95 ≤2500ms (MVP allows slight degradation)
- ✅ 100% success rate
- ✅ No MQTT queue overflow

---

### TC5: Latency Breakdown Analysis

**Objective**: Identify latency distribution across pipeline stages

**Test Steps**:
1. Run 50-trigger test
2. Query Prometheus for stage-specific metrics
3. Calculate average time per stage

**Expected Breakdown** (example):
```
Trigger → Policy Receipt:     ~100ms  (MQTT + network)
Policy Validation:             ~50ms   (anti-replay, dedup)
Policy Evaluation:             ~150ms  (FSM, topology)
PA Fan-Out:                    ~200ms  (MQTT publish to all rooms)
PA Playback:                   ~1500ms (TTS + audio playback)
Total:                         ~2000ms
```

**Acceptance Criteria**:
- ✅ No single stage exceeds 50% of total latency
- ✅ Identify bottlenecks for optimization
- ✅ Validate against sequence diagram estimates

---

### TC6: Metrics Accuracy Validation

**Objective**: Verify Prometheus metrics match actual latency measurements

**Test Steps**:
1. Run `measure-latency.sh` (script-based measurement)
2. Query Prometheus for same time period
3. Compare P95 values

**Expected Results**:
- Script P95 and Prometheus P95 within ±10% margin

**Acceptance Criteria**:
- ✅ Metrics correlation coefficient >0.9
- ✅ No missing data points in Prometheus
- ✅ Histogram buckets adequately cover latency range

---

## Test Execution Procedure

### Pre-Test Checklist

- [ ] All services started and healthy
- [ ] Certificates generated and valid
- [ ] Prometheus scraping both services
- [ ] Grafana dashboard accessible
- [ ] Test scripts executable (`chmod +x`)
- [ ] System resources adequate (CPU <50%, Memory <70%)

### Test Execution

```bash
# Navigate to scripts directory
cd mvp/scripts

# Run latency measurement
./measure-latency.sh 50

# Save results
cp /tmp/safesignal-latency-*.txt ./results/
```

### Post-Test Analysis

1. **Review Script Output**:
   - Check P50, P95, P99 values
   - Review histogram distribution
   - Note any anomalies

2. **Query Prometheus**:
   ```bash
   # P95 latency over test period
   curl -s 'http://localhost:9090/api/v1/query?query=histogram_quantile(0.95,sum(rate(alert_trigger_latency_seconds_bucket[5m]))by(le))' | jq .

   # Success ratio
   curl -s 'http://localhost:9090/api/v1/query?query=pa_playback_success_ratio' | jq .
   ```

3. **Review Service Logs**:
   - Check for errors or warnings
   - Verify source room exclusion logged
   - Count dedup hits

4. **Grafana Dashboard**:
   - Visual inspection of latency trends
   - Identify spikes or outliers

---

## Pass/Fail Criteria

### PASS Criteria

All of the following must be true:

- ✅ P95 latency ≤2000ms (measured over ≥50 samples)
- ✅ Success rate ≥99%
- ✅ Source room exclusion enforced (100% of time)
- ✅ Deduplication working (within 500ms window)
- ✅ No critical errors in logs
- ✅ Prometheus metrics accurate (±10% of script measurements)

### FAIL Criteria

Any of the following:

- ❌ P95 latency >2000ms
- ❌ Success rate <99%
- ❌ Source room NOT excluded (even once) → **CRITICAL**
- ❌ Deduplication not working
- ❌ Service crashes or health check failures
- ❌ Metrics missing or incorrect

---

## Test Results Template

```markdown
## Test Run Report

**Date**: YYYY-MM-DD
**Tester**: [Name]
**Environment**: Local Docker
**Sample Size**: 50

### Results

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| P50 Latency | ≤1000ms | XXX ms | ✅/❌ |
| P95 Latency | ≤2000ms | XXX ms | ✅/❌ |
| P99 Latency | ≤3000ms | XXX ms | ✅/❌ |
| Success Rate | ≥99% | XX% | ✅/❌ |
| Source Exclusion | 100% | 100% | ✅/❌ |
| Dedup Hits | >0 | XX | ✅/❌ |

### Latency Distribution

```
  0-500ms:   ############### (20)
  500-1000ms: ################## (30)
  1000-1500ms: ##### (8)
  1500-2000ms: ## (2)
  2000-2500ms: (0)
```

### Observations

- [Any anomalies, patterns, or notable findings]

### Issues Found

- [List any bugs or concerns]

### Conclusion

**Overall Status**: ✅ PASS / ❌ FAIL

**Reason**: [Brief summary]
```

---

## Known Limitations (MVP)

1. **Test Client Overhead**: Script-based latency includes script execution time (~50-100ms)
2. **TTS Stub**: Simulated playback (2-4s) may not match real hardware
3. **Single Node**: No HA/cluster testing in MVP
4. **No Real Hardware**: ESP32 and PA system are simulated
5. **Limited Concurrency**: Test assumes sequential processing

These will be addressed in future iterations with hardware-in-the-loop and load testing.

---

## Continuous Testing

### Regression Testing

Run latency test after any code changes:

```bash
# In CI/CD pipeline
./measure-latency.sh 50 || exit 1
```

### Performance Monitoring

Set up alerts in Prometheus:

```yaml
- alert: HighLatencyP95
  expr: histogram_quantile(0.95, sum(rate(alert_trigger_latency_seconds_bucket[5m])) by (le)) > 2
  for: 5m
  annotations:
    summary: "P95 latency exceeds 2s SLO"
```

---

## Appendix: Useful Queries

### Prometheus Queries

```promql
# P95 latency (last 5 minutes)
histogram_quantile(0.95, sum(rate(alert_trigger_latency_seconds_bucket[5m])) by (le))

# Alerts processed rate
rate(alerts_processed_total{state="policy_evaluated"}[1m])

# PA success ratio
pa_playback_success_ratio

# Dedup cache size
dedup_cache_size

# MQTT message rate
rate(mqtt_messages_total[1m])
```

### Log Grep Patterns

```bash
# Source room exclusions
docker logs safesignal-policy-service | grep "SOURCE ROOM EXCLUDED"

# Latency measurements
docker logs safesignal-policy-service | grep "LatencyMs"

# Dedup hits
docker logs safesignal-policy-service | grep "Duplicate alert detected"

# PA playback completions
docker logs safesignal-pa-service | grep "playback completed"
```

---

**Document Version**: 1.0.0-MVP
**Next Review**: After first pilot deployment
