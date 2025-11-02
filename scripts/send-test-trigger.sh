#!/usr/bin/env bash
# send-test-trigger.sh - Send test alert trigger via MQTT
#
# Usage:
#   ./send-test-trigger.sh [tenant] [building] [room]
#   ./send-test-trigger.sh           # Uses defaults: tenant-a, building-a, room-1

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CERTS_DIR="${SCRIPT_DIR}/../edge/certs"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Parse arguments
TENANT_ID="${1:-tenant-a}"
BUILDING_ID="${2:-building-a}"
SOURCE_ROOM_ID="${3:-room-1}"

# Generate IDs
ALERT_ID="alert-$(date +%s)-$(openssl rand -hex 4)"
CAUSAL_CHAIN_ID="chain-$(date +%s)"
DEVICE_ID="device-esp32-001"
# Generate ISO 8601 timestamp with milliseconds (macOS compatible)
# macOS date doesn't support %N (nanoseconds), so we use .000Z for milliseconds
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")

# MQTT Configuration
BROKER_HOST="${MQTT_BROKER_HOST:-localhost}"
BROKER_PORT="${MQTT_BROKER_PORT:-8883}"
TOPIC="tenant/${TENANT_ID}/building/${BUILDING_ID}/room/${SOURCE_ROOM_ID}/alert"

# Certificate paths
CA_CERT="${CERTS_DIR}/ca/ca.crt"
CLIENT_CERT="${CERTS_DIR}/devices/esp32-test.crt"
CLIENT_KEY="${CERTS_DIR}/devices/esp32-test.key"

# Check prerequisites
if ! command -v mosquitto_pub &> /dev/null; then
    echo -e "${YELLOW}[WARN]${NC} mosquitto_pub not found. Install with:"
    echo "  macOS: brew install mosquitto"
    echo "  Ubuntu: sudo apt-get install mosquitto-clients"
    exit 1
fi

# Check certificates exist
if [[ ! -f "$CA_CERT" ]] || [[ ! -f "$CLIENT_CERT" ]] || [[ ! -f "$CLIENT_KEY" ]]; then
    echo -e "${YELLOW}[ERROR]${NC} Certificates not found. Run seed-certs.sh first:"
    echo "  cd ${SCRIPT_DIR} && ./seed-certs.sh"
    exit 1
fi

# Build JSON payload
PAYLOAD=$(cat <<EOF
{
  "alertId": "${ALERT_ID}",
  "tenantId": "${TENANT_ID}",
  "buildingId": "${BUILDING_ID}",
  "sourceDeviceId": "${DEVICE_ID}",
  "sourceRoomId": "${SOURCE_ROOM_ID}",
  "origin": "ESP32",
  "causalChainId": "${CAUSAL_CHAIN_ID}",
  "mode": "AUDIBLE",
  "ts": "${TIMESTAMP}",
  "nonce": "$(openssl rand -hex 16)"
}
EOF
)

# Display test information
echo ""
echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   SafeSignal - Send Test Alert Trigger                   ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}Configuration:${NC}"
echo "  Broker: ${BROKER_HOST}:${BROKER_PORT}"
echo "  Topic: ${TOPIC}"
echo "  Alert ID: ${ALERT_ID}"
echo "  Tenant: ${TENANT_ID}"
echo "  Building: ${BUILDING_ID}"
echo "  Source Room: ${SOURCE_ROOM_ID} ${YELLOW}(should NOT be audible)${NC}"
echo ""
echo -e "${GREEN}Payload:${NC}"
echo "${PAYLOAD}" | jq '.' 2>/dev/null || echo "${PAYLOAD}"
echo ""

# Send MQTT message
echo -e "${GREEN}Sending alert trigger...${NC}"

mosquitto_pub \
  -h "${BROKER_HOST}" \
  -p "${BROKER_PORT}" \
  -t "${TOPIC}" \
  -m "${PAYLOAD}" \
  -q 1 \
  --cafile "${CA_CERT}" \
  --cert "${CLIENT_CERT}" \
  --key "${CLIENT_KEY}" \
  --tls-version tlsv1.3 \
  -d

if [[ $? -eq 0 ]]; then
    echo ""
    echo -e "${GREEN}✓ Alert trigger sent successfully!${NC}"
    echo ""
    echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║   Monitor Alert Processing                               ║${NC}"
    echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
    echo ""
    echo -e "${GREEN}📋 Service Logs${NC}"
    echo "  Policy Service:      docker logs safesignal-policy-service -f"
    echo "  PA Service:          docker logs safesignal-pa-service -f"
    echo ""
    echo -e "${GREEN}📊 Dashboards${NC}"
    echo "  Status Dashboard:    http://localhost:5200"
    echo "  Grafana:             http://localhost:3000"
    echo "  Prometheus:          http://localhost:9090"
    echo ""
    echo -e "${GREEN}📈 Metrics${NC}"
    echo "  Policy metrics:      curl http://localhost:5100/metrics | grep alert"
    echo "  PA metrics:          curl http://localhost:5101/metrics | grep pa_"
    echo ""
    echo -e "${GREEN}✅ Verification Checklist${NC}"
    echo "  □ Source room ${SOURCE_ROOM_ID} excluded (check policy logs)"
    echo "  □ PA command received (check PA logs)"
    echo "  □ Audio playback started (check PA logs)"
    echo "  □ Metrics updated (check metrics endpoints)"
    echo ""
else
    echo ""
    echo -e "${YELLOW}✗ Failed to send alert trigger${NC}"
    echo ""
    echo -e "${YELLOW}Troubleshooting:${NC}"
    echo "  - Verify EMQX is running: docker ps | grep emqx"
    echo "  - Check EMQX logs: docker logs safesignal-emqx"
    echo "  - Verify certificates are valid"
    echo "  - Check EMQX dashboard: http://localhost:18083 (admin/public)"
    exit 1
fi
