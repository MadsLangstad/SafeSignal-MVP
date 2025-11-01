# SafeSignal Edge - Local E2E Runbook

**Version**: 1.0.0-MVP
**Last Updated**: 2025-10-31
**Target Audience**: Developers, QA Engineers

---

## Overview

This runbook guides you through starting the SafeSignal Edge stack locally, sending test triggers, and validating the complete alert flow with P95 latency measurement.

**Expected Time**: ~15 minutes (first run), ~5 minutes (subsequent runs)

---

## Prerequisites

### Required Software

- **Docker** (≥20.10) and Docker Compose (≥2.0)
- **OpenSSL** (for certificate generation)
- **mosquitto-clients** (for MQTT testing)
  - macOS: `brew install mosquitto`
  - Ubuntu: `sudo apt-get install mosquitto-clients`
- **curl** and **jq** (for metrics queries)

### System Requirements

- **OS**: macOS, Linux, or Windows (WSL2)
- **RAM**: 4GB minimum, 8GB recommended
- **Disk**: 2GB free space
- **Ports**: 3000, 5000, 5001, 8883, 9090, 18083 (must be available)

---

## Step 1: Generate TLS Certificates

Navigate to the scripts directory and run the certificate generation script:

```bash
cd mvp/scripts
./seed-certs.sh
```

**Expected Output**:
```
[INFO] Generating Certificate Authority (CA)...
[INFO] CA certificate generated: ...
[INFO] Generating certificate for: emqx
[INFO] ✓ Certificate generated and verified: ...
[INFO] ✓ Certificate generation complete!
```

**Verification**:
```bash
ls -la ../edge/certs/
```

You should see directories: `ca/`, `emqx/`, `policy-service/`, `pa-service/`, `devices/`

**Troubleshooting**:
- If OpenSSL errors occur, verify OpenSSL version: `openssl version` (need ≥1.1.1)
- Check permissions: Ensure script has execute permission (`chmod +x seed-certs.sh`)

---

## Step 2: Start the Edge Stack

Navigate to the edge directory and start all services:

```bash
cd ../edge
docker-compose up -d
```

**Expected Output**:
```
Creating network "safesignal-edge"...
Creating volume "safesignal-emqx-data"...
Creating safesignal-emqx...
Creating safesignal-policy-service...
Creating safesignal-pa-service...
Creating safesignal-prometheus...
Creating safesignal-grafana...
```

**Wait for Services to be Healthy** (~30-60 seconds):

```bash
docker-compose ps
```

All services should show `healthy` or `running`:

```
NAME                         STATUS
safesignal-emqx              Up (healthy)
safesignal-policy-service    Up (healthy)
safesignal-pa-service        Up (healthy)
safesignal-prometheus        Up
safesignal-grafana           Up
```

**Troubleshooting**:
- **Port conflicts**: Check if ports are in use: `lsof -i :8883` (macOS/Linux)
- **Health check failures**: View logs: `docker-compose logs policy-service`
- **Certificate errors**: Re-run `seed-certs.sh` and restart services

---

## Step 3: Verify Service Health

Check health endpoints for both services:

```bash
# Policy Service
curl -s http://localhost:5000/health | jq '.'

# PA Service
curl -s http://localhost:5001/health | jq '.'
```

**Expected Output**: `{"status":"Healthy"}`

Check Prometheus targets:

```bash
open http://localhost:9090/targets
# OR: curl http://localhost:9090/targets
```

All targets should be **UP**:
- `policy-service:5000` - UP
- `pa-service:5001` - UP
- `emqx:18083` - UP

**Troubleshooting**:
- If targets are DOWN, check service logs: `docker-compose logs [service-name]`
- Verify network connectivity: `docker network inspect safesignal-edge`

---

## Step 4: Send Test Trigger

Send a single test alert trigger:

```bash
cd ../scripts
./send-test-trigger.sh tenant-a building-a room-1
```

**Expected Output**:
```
╔═══════════════════════════════════════════════════════════╗
║   SafeSignal - Send Test Alert Trigger                   ║
╚═══════════════════════════════════════════════════════════╝

Configuration:
  Broker: localhost:8883
  Topic: tenant/tenant-a/building/building-a/room/room-1/alert
  Alert ID: alert-1698765432-a3f4e9b2
  Source Room: room-1 (should NOT be audible)

✓ Alert trigger sent successfully!
```

**Verification**:

1. **Check Policy Service Logs**:
```bash
docker logs safesignal-policy-service --tail 20
```

Look for:
- `Alert trigger received: AlertId=...`
- `SOURCE ROOM EXCLUDED (Safety Invariant): Room=room-1` ✓ **CRITICAL**
- `Policy evaluation: ...TargetRooms=3` (excludes room-1)
- `PA command sent: ...`

2. **Check PA Service Logs**:
```bash
docker logs safesignal-pa-service --tail 20
```

Look for:
- `PA command received: AlertId=...`
- `Playing audio: Room=room-2, ...` (not room-1!)
- `PA playback completed: ...`

3. **Check Metrics**:
```bash
curl -s http://localhost:5000/metrics | grep alert_trigger_latency
```

---

## Step 5: Validate Source Room Exclusion

**CRITICAL TEST**: Verify source room is NEVER included in PA commands.

```bash
# Send trigger from room-test
./send-test-trigger.sh tenant-a building-a room-test

# Check PA service logs - should NOT see room-test
docker logs safesignal-pa-service --tail 30 | grep "room-test"
```

**Expected**: No output (room-test excluded from playback)

**If you see room-test in PA logs** → ⚠️ **CRITICAL BUG** - source room exclusion failed!

---

## Step 6: Run Latency Measurement

Measure P95 latency over 50 synthetic triggers:

```bash
./measure-latency.sh 50
```

**Expected Output**:
```
════════════════════════════════════════════════════════════
Latency Statistics (milliseconds)
════════════════════════════════════════════════════════════
  Min:      120 ms
  Average:  450 ms
  P50:      420 ms
  P95:     1850 ms  ✓ SLO MET
  P99:     1990 ms
  Max:     2100 ms
════════════════════════════════════════════════════════════

Test Summary
════════════════════════════════════════════════════════════
  Triggers sent: 50
  Triggers processed: 50
  P95 latency: 1850 ms
  SLO target: ≤2000 ms
  Result: ✓ PASS
════════════════════════════════════════════════════════════
```

**SLO Compliance**: P95 must be ≤2000ms for local E2E test to pass.

**Troubleshooting**:
- **P95 >2s**: Check system load, Docker resource allocation
- **Triggers not processed**: Check deduplication window, MQTT connection
- **High variance**: Network instability, increase test sample size

---

## Step 7: View Grafana Dashboard

Open Grafana dashboard:

```bash
open http://localhost:3000
# OR visit: http://localhost:3000
```

**Login**:
- Username: `admin`
- Password: `admin` (change on first login)

**Navigate to**: Dashboards → SafeSignal Edge Metrics

**Key Panels**:
1. **Alert Trigger Latency (P95)**: Should trend below 2s threshold line
2. **PA Playback Success Ratio**: Should be ≥0.99
3. **Alerts Processed Rate**: Shows throughput
4. **Deduplication Cache**: Monitor cache size and hit rate

---

## Step 8: Test Deduplication

Verify deduplication works within 300-800ms window:

```bash
# Send duplicate triggers rapidly
for i in {1..5}; do
  ./send-test-trigger.sh tenant-a building-a room-dup-test
  sleep 0.1  # 100ms between triggers
done
```

**Check Policy Service Logs**:
```bash
docker logs safesignal-policy-service --tail 50 | grep -i "duplicate"
```

**Expected**: At least 3-4 duplicates detected and rejected.

**Check Metrics**:
```bash
curl -s http://localhost:5000/metrics | grep dedup_hits_total
```

Should show `dedup_hits_total` counter increasing.

---

## Common Troubleshooting

### EMQX Connection Refused

**Symptoms**: `mosquitto_pub: Connection refused` or TLS handshake errors

**Solutions**:
1. Verify EMQX is running: `docker ps | grep emqx`
2. Check EMQX logs: `docker logs safesignal-emqx --tail 50`
3. Verify certificates: `openssl s_client -connect localhost:8883 -CAfile ../edge/certs/ca/ca.crt`
4. Check ACL rules: `docker exec safesignal-emqx cat /opt/emqx/etc/acl.conf`

### Policy Service Not Processing Alerts

**Symptoms**: Alerts sent but not appearing in logs

**Solutions**:
1. Verify MQTT connection: `docker logs safesignal-policy-service | grep "Connected to MQTT"`
2. Check subscription status: Look for `Subscribed to alert topic` in logs
3. Verify topic format: Must match `tenant/{tid}/building/{bid}/room/{rid}/alert`
4. Check ACL permissions: Policy service cert must allow subscription

### PA Service Not Playing Audio

**Symptoms**: Policy service sends commands but PA service doesn't respond

**Solutions**:
1. Check PA service MQTT connection: `docker logs safesignal-pa-service | grep "Connected"`
2. Verify PA commands are sent: `docker logs safesignal-policy-service | grep "PA command sent"`
3. Check topic subscription: PA must subscribe to `pa/+/play`
4. Monitor errors: `docker logs safesignal-pa-service | grep -i error`

### High Latency (P95 >2s)

**Symptoms**: Latency test fails SLO

**Solutions**:
1. Check system resources: `docker stats`
2. Reduce Docker CPU/memory constraints
3. Check disk I/O: Prometheus/EMQX data volumes
4. Network issues: `docker network inspect safesignal-edge`
5. Increase test sample size to reduce variance

---

## Cleanup

### Stop Services (Keep Data)

```bash
cd ../edge
docker-compose down
```

### Full Cleanup (Remove Volumes)

```bash
docker-compose down -v
rm -rf certs
```

**Warning**: This deletes all metrics data, logs, and certificates. You'll need to re-run `seed-certs.sh`.

---

## Quick Reference Commands

```bash
# Start stack
cd mvp/edge && docker-compose up -d

# View all logs
docker-compose logs -f

# View specific service
docker logs safesignal-policy-service -f

# Check health
curl localhost:5000/health
curl localhost:5001/health

# Send test trigger
cd ../scripts && ./send-test-trigger.sh tenant-a building-a room-1

# Measure latency
./measure-latency.sh 50

# View metrics
curl localhost:5000/metrics | grep alert
curl localhost:5001/metrics | grep pa

# Restart service
docker-compose restart policy-service

# Stop everything
docker-compose down
```

---

## Next Steps

After successful local E2E test:

1. **Review Metrics**: Analyze Prometheus/Grafana for bottlenecks
2. **Adjust Configuration**: Tune dedup window, timeouts if needed
3. **Add Test Cases**: Custom building topologies, failure scenarios
4. **Cloud Integration**: Add gRPC stub for cloud escalation
5. **ESP32 Integration**: Replace test script with real hardware trigger

---

## Success Criteria

✅ **All acceptance criteria met**:
- [x] EMQX rejects connections without valid mTLS certificate
- [x] Test trigger flows through to PA service
- [x] Source room is excluded from PA commands (verified in logs)
- [x] Policy service logs show deduplication
- [x] P95 latency ≤2s measured over 50 triggers
- [x] Prometheus scrapes metrics from both services
- [x] Grafana dashboard displays all key metrics

---

**Questions or Issues?**
Check service logs first, then refer to troubleshooting section.
For bugs, create issue with logs attached.
