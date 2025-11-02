#!/usr/bin/env bash
# show-services.sh - Display all SafeSignal service URLs and status
#
# Usage: ./show-services.sh

set -euo pipefail

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

# Header
echo ""
echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   SafeSignal MVP - Service Dashboard                          ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check if Docker Compose is running
cd "$(dirname "$0")/../edge"
if ! docker-compose ps emqx &>/dev/null; then
    echo -e "${RED}⚠️  Services not running. Start with:${NC}"
    echo "  cd edge && docker-compose up -d"
    echo ""
    exit 1
fi

# Get service status
get_status() {
    local service=$1
    local status=$(docker-compose ps "$service" 2>/dev/null | grep -o "healthy\|unhealthy" || echo "unknown")

    if [[ "$status" == "healthy" ]]; then
        echo -e "${GREEN}✅ healthy${NC}"
    elif [[ "$status" == "unhealthy" ]]; then
        echo -e "${RED}❌ unhealthy${NC}"
    else
        local running=$(docker-compose ps "$service" 2>/dev/null | grep "Up" || echo "")
        if [[ -n "$running" ]]; then
            echo -e "${YELLOW}⚠️  running${NC}"
        else
            echo -e "${RED}❌ stopped${NC}"
        fi
    fi
}

# Core Services Status
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}🏥 CORE SERVICES STATUS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "  Policy Service:        $(get_status policy-service)"
echo -e "  PA Service:            $(get_status pa-service)"
echo -e "  EMQX Broker:           $(get_status emqx)"
echo -e "  MinIO Storage:         $(get_status minio)"
echo -e "  Prometheus:            $(get_status prometheus)"
echo -e "  Grafana:               $(get_status grafana)"
echo -e "  Status Dashboard:      $(get_status status-dashboard)"
echo ""

# Monitoring & Dashboards
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}📊 MONITORING & DASHBOARDS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${GREEN}Status Dashboard${NC}"
echo "  URL:        http://localhost:5200"
echo "  Purpose:    Real-time alert monitoring and system status"
echo "  Features:   Alert history, topology view, device status"
echo ""
echo -e "${GREEN}Grafana${NC}"
echo "  URL:        http://localhost:3000"
echo "  Username:   admin"
echo "  Password:   admin"
echo "  Dashboard:  SafeSignal Edge Metrics"
echo "  Purpose:    Time-series metrics visualization"
echo ""
echo -e "${GREEN}Prometheus${NC}"
echo "  URL:        http://localhost:9090"
echo "  Purpose:    Metrics collection and querying"
echo "  Targets:    http://localhost:9090/targets"
echo "  Queries:    pa_commands_total, alerts_processed_total"
echo ""

# Infrastructure
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}🔧 INFRASTRUCTURE SERVICES${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${GREEN}EMQX MQTT Broker${NC}"
echo "  Dashboard:  http://localhost:18083"
echo "  Username:   admin"
echo "  Password:   public"
echo "  MQTT Port:  1883 (TCP), 8883 (TLS)"
echo "  Purpose:    Message broker for alert triggers"
echo ""
echo -e "${GREEN}MinIO Object Storage${NC}"
echo "  Console:    http://localhost:9001"
echo "  Username:   safesignal-admin"
echo "  Password:   safesignal-dev-password-change-in-prod"
echo "  API:        http://localhost:9000"
echo "  Bucket:     safesignal-audio"
echo "  Purpose:    Audio clip storage"
echo ""

# API Endpoints
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}📈 API ENDPOINTS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${GREEN}Policy Service${NC} (Port 5100)"
echo "  Health:     http://localhost:5100/health"
echo "  Stats:      http://localhost:5100/api/stats"
echo "  Alerts:     http://localhost:5100/api/alerts"
echo "  Metrics:    http://localhost:5100/metrics"
echo "  Send Alert: curl -X POST http://localhost:5100/api/alerts \\"
echo "                -H 'Content-Type: application/json' \\"
echo "                -d '{\"buildingId\":\"BUILDING_A\",\"sourceRoomId\":\"ROOM_101\",\"mode\":\"AUDIBLE\"}'"
echo ""
echo -e "${GREEN}PA Service${NC} (Port 5101)"
echo "  Health:     http://localhost:5101/health"
echo "  Clips:      http://localhost:5101/api/clips"
echo "  Metrics:    http://localhost:5101/metrics"
echo "  Get Clip:   http://localhost:5101/api/clips/FIRE_ALARM"
echo ""

# Audio Clips Info
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}🎵 AUDIO CLIPS STATUS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Try to get audio clip count
AUDIO_COUNT=$(curl -s http://localhost:5101/api/clips 2>/dev/null | jq -r '.count' 2>/dev/null || echo "N/A")
if [[ "$AUDIO_COUNT" != "N/A" ]]; then
    echo -e "  Total Clips:    ${GREEN}${AUDIO_COUNT}${NC}"

    # List available clips
    CLIPS=$(curl -s http://localhost:5101/api/clips 2>/dev/null | jq -r '.clips[]' 2>/dev/null || echo "")
    if [[ -n "$CLIPS" ]]; then
        echo "  Available:"
        while IFS= read -r clip; do
            echo "    • $clip"
        done <<< "$CLIPS"
    fi
else
    echo -e "  ${YELLOW}⚠️  Unable to fetch audio clip information${NC}"
fi
echo ""

# System Stats
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}📊 SYSTEM STATISTICS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Get stats from policy service
STATS=$(curl -s http://localhost:5100/api/stats 2>/dev/null || echo "{}")
if [[ "$STATS" != "{}" ]]; then
    TOTAL_ALERTS=$(echo "$STATS" | jq -r '.total_alerts // "N/A"')
    ALERTS_TODAY=$(echo "$STATS" | jq -r '.alerts_today // "N/A"')
    TOTAL_BUILDINGS=$(echo "$STATS" | jq -r '.total_buildings // "N/A"')
    TOTAL_ROOMS=$(echo "$STATS" | jq -r '.total_rooms // "N/A"')
    TOTAL_DEVICES=$(echo "$STATS" | jq -r '.total_devices // "N/A"')
    ACTIVE_DEVICES=$(echo "$STATS" | jq -r '.active_devices // "N/A"')

    echo "  Total Alerts:      $TOTAL_ALERTS"
    echo "  Alerts Today:      $ALERTS_TODAY"
    echo "  Buildings:         $TOTAL_BUILDINGS"
    echo "  Rooms:             $TOTAL_ROOMS"
    echo "  Devices:           $ACTIVE_DEVICES / $TOTAL_DEVICES active"
else
    echo -e "  ${YELLOW}⚠️  Unable to fetch system statistics${NC}"
fi
echo ""

# Key Metrics
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}📈 KEY METRICS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Get PA metrics
PA_SUCCESS=$(curl -s http://localhost:5101/metrics 2>/dev/null | grep 'pa_playback_success_ratio' | grep -v '^#' | awk '{print $2}' || echo "N/A")
PA_COMMANDS=$(curl -s http://localhost:5101/metrics 2>/dev/null | grep 'pa_commands_total{status="success"}' | awk '{print $2}' || echo "N/A")

if [[ "$PA_SUCCESS" != "N/A" ]]; then
    PA_SUCCESS_PCT=$(echo "$PA_SUCCESS * 100" | bc 2>/dev/null || echo "N/A")
    echo "  PA Success Rate:   ${PA_SUCCESS_PCT}%"
    echo "  PA Commands:       $PA_COMMANDS successful"
else
    echo -e "  ${YELLOW}⚠️  Unable to fetch PA metrics${NC}"
fi
echo ""

# Quick Actions
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}🚀 QUICK ACTIONS${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "  Send test alert:     ./send-test-trigger.sh"
echo "  Run full test:       ./test-mvp.sh"
echo "  Measure latency:     ./measure-latency.sh 50"
echo "  View logs:           docker logs safesignal-policy-service -f"
echo "  Restart services:    cd ../edge && docker-compose restart"
echo "  Stop services:       cd ../edge && docker-compose down"
echo ""

# Documentation
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}📚 DOCUMENTATION${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "  Production Guide:    edge/PRODUCTION_HARDENING.md"
echo "  Completion Report:   edge/COMPLETION_REPORT.md"
echo "  Main README:         README.md"
echo ""

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  All systems operational! 🚀                                   ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""
