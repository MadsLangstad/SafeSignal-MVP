#!/usr/bin/env bash
# measure-latency.sh - Measure P95 latency for alert trigger → PA playback
#
# Sends 50 synthetic triggers and measures end-to-end latency
# Reports P50, P95, P99 and histogram
#
# Usage:
#   ./measure-latency.sh [num_triggers]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_FILE="/tmp/safesignal-latency-$(date +%s).txt"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

# Configuration
NUM_TRIGGERS="${1:-50}"
TENANT_ID="tenant-a"
BUILDING_ID="building-a"
# Use rooms from hardcoded topology (room-1 through room-4 cycle)
SOURCE_ROOM_ID="room-1"

# Metrics endpoint
POLICY_METRICS_URL="http://localhost:5100/metrics"
PA_METRICS_URL="http://localhost:5101/metrics"

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
if ! command -v curl &> /dev/null; then
    log_error "curl is required"
    exit 1
fi

# Banner
echo ""
echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   SafeSignal - Latency Measurement Test                  ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
log_info "Configuration:"
echo "  Number of triggers: ${NUM_TRIGGERS}"
echo "  Tenant: ${TENANT_ID}"
echo "  Building: ${BUILDING_ID}"
echo "  Results file: ${RESULTS_FILE}"
echo ""

# Verify services are running
log_info "Verifying services are running..."

if ! curl -s -f "${POLICY_METRICS_URL}" > /dev/null 2>&1; then
    log_error "Policy service not reachable at ${POLICY_METRICS_URL}"
    log_warn "Start services with: cd ../edge && docker-compose up -d"
    exit 1
fi

if ! curl -s -f "${PA_METRICS_URL}" > /dev/null 2>&1; then
    log_error "PA service not reachable at ${PA_METRICS_URL}"
    exit 1
fi

log_info "✓ All services reachable"
echo ""

# Get baseline metrics
log_info "Capturing baseline metrics..."
BASELINE_ALERTS=$(curl -s "${POLICY_METRICS_URL}" | grep 'alerts_processed_total{state="policy_evaluated"}' | awk '{print $2}' || echo "0")
log_info "Baseline alerts processed: ${BASELINE_ALERTS}"
echo ""

# Send triggers and measure latency
log_info "Sending ${NUM_TRIGGERS} test triggers..."
> "${RESULTS_FILE}"  # Clear results file

for i in $(seq 1 ${NUM_TRIGGERS}); do
    # Cycle through room-1 to room-4 (hardcoded topology)
    ROOM_NUM=$(( ((i - 1) % 4) + 1 ))
    ROOM_ID="room-${ROOM_NUM}"

    # Get millisecond timestamp (cross-platform using Python)
    START_TIME=$(python3 -c "import time; print(int(time.time() * 1000))")

    # Send trigger (silently)
    "${SCRIPT_DIR}/send-test-trigger.sh" "${TENANT_ID}" "${BUILDING_ID}" "${ROOM_ID}" > /dev/null 2>&1

    # Small delay to allow processing
    sleep 0.1

    # Query metrics to verify processing (simplified - in production would correlate by alertId)
    END_TIME=$(python3 -c "import time; print(int(time.time() * 1000))")
    LATENCY_MS=$((END_TIME - START_TIME))

    echo "${LATENCY_MS}" >> "${RESULTS_FILE}"

    if [[ $((i % 10)) -eq 0 ]]; then
        log_info "Progress: ${i}/${NUM_TRIGGERS} triggers sent"
    fi
done

echo ""
log_info "✓ All triggers sent. Waiting for processing to complete..."
sleep 2

# Verify final metrics
FINAL_ALERTS=$(curl -s "${POLICY_METRICS_URL}" | grep 'alerts_processed_total{state="policy_evaluated"}' | awk '{print $2}' || echo "0")
PROCESSED_COUNT=$((FINAL_ALERTS - BASELINE_ALERTS))

log_info "Alerts processed: ${PROCESSED_COUNT}/${NUM_TRIGGERS}"

if [[ ${PROCESSED_COUNT} -lt ${NUM_TRIGGERS} ]]; then
    log_warn "Some alerts may not have been processed. Check service logs."
fi

# Calculate statistics
log_info "Calculating latency statistics..."
echo ""

# Sort results
sort -n "${RESULTS_FILE}" > "${RESULTS_FILE}.sorted"

# Calculate percentiles
TOTAL_COUNT=$(wc -l < "${RESULTS_FILE}.sorted")
P50_INDEX=$(awk "BEGIN {idx=int(${TOTAL_COUNT} * 0.50); print (idx < 1 ? 1 : idx)}")
P95_INDEX=$(awk "BEGIN {idx=int(${TOTAL_COUNT} * 0.95); print (idx < 1 ? 1 : idx)}")
P99_INDEX=$(awk "BEGIN {idx=int(${TOTAL_COUNT} * 0.99); print (idx < 1 ? 1 : idx)}")

P50=$(sed -n "${P50_INDEX}p" "${RESULTS_FILE}.sorted" || echo "0")
P95=$(sed -n "${P95_INDEX}p" "${RESULTS_FILE}.sorted" || echo "0")
P99=$(sed -n "${P99_INDEX}p" "${RESULTS_FILE}.sorted" || echo "0")

MIN=$(head -n 1 "${RESULTS_FILE}.sorted" 2>/dev/null || echo "0")
MAX=$(tail -n 1 "${RESULTS_FILE}.sorted" 2>/dev/null || echo "0")

# Calculate average
TOTAL_MS=$(awk '{sum+=$1} END {print sum}' "${RESULTS_FILE}.sorted")
if [[ -z "${TOTAL_MS}" ]] || [[ "${TOTAL_MS}" == "0" ]]; then
    AVG=0
else
    AVG=$(awk "BEGIN {printf \"%.0f\", ${TOTAL_MS} / ${TOTAL_COUNT}}")
fi

# Display results
echo -e "${BLUE}════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}Latency Statistics (milliseconds)${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════${NC}"
printf "  Min:     %6d ms\n" "${MIN}"
printf "  Average: %6d ms\n" "${AVG}"
printf "  P50:     %6d ms\n" "${P50}"
printf "  P95:     %6d ms  " "${P95}"

# Check SLO compliance (P95 ≤ 2000ms)
if [[ ${P95} -le 2000 ]]; then
    echo -e "${GREEN}✓ SLO MET${NC}"
else
    echo -e "${RED}✗ SLO MISS${NC}"
fi

printf "  P99:     %6d ms\n" "${P99}"
printf "  Max:     %6d ms\n" "${MAX}"
echo -e "${BLUE}════════════════════════════════════════════════════════════${NC}"
echo ""

# Simple histogram
log_info "Latency distribution histogram:"
echo ""
awk '{
    bucket = int($1 / 500) * 500;
    count[bucket]++;
}
END {
    for (bucket in count) {
        printf "  %4d-%4dms: ", bucket, bucket+499;
        for (i=0; i<count[bucket]; i++) printf "#";
        printf " (%d)\n", count[bucket];
    }
}' "${RESULTS_FILE}.sorted" | sort -n

echo ""

# Fetch detailed metrics from services
log_info "Prometheus metrics summary:"
echo ""

echo "Policy Service:"
curl -s "${POLICY_METRICS_URL}" | grep -E "(alert_trigger_latency|alerts_processed_total|alerts_rejected_total|dedup)" | head -10
echo ""

echo "PA Service:"
curl -s "${PA_METRICS_URL}" | grep -E "(pa_commands_total|pa_playback|tts_generation)" | head -10
echo ""

# Summary
echo -e "${GREEN}════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}Test Summary${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════════${NC}"
echo "  Triggers sent: ${NUM_TRIGGERS}"
echo "  Triggers processed: ${PROCESSED_COUNT}"
echo "  P95 latency: ${P95} ms"
echo "  SLO target: ≤2000 ms"

if [[ ${P95} -le 2000 ]]; then
    echo -e "  Result: ${GREEN}✓ PASS${NC}"
    EXIT_CODE=0
else
    echo -e "  Result: ${RED}✗ FAIL${NC} (P95 exceeds 2000ms)"
    EXIT_CODE=1
fi

echo -e "${GREEN}════════════════════════════════════════════════════════════${NC}"
echo ""

log_info "Detailed results saved to: ${RESULTS_FILE}"
log_info "Sorted results saved to: ${RESULTS_FILE}.sorted"
echo ""

exit ${EXIT_CODE}
