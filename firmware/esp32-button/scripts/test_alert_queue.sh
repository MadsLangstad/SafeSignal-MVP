#!/bin/bash
#
# Alert Queue Test Script
# Automates testing of ESP32-S3 alert persistence features
#

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SERIAL_PORT="${1:-/dev/ttyUSB0}"
MQTT_CONTAINER="safesignal-emqx"
TEST_DURATION=300  # 5 minutes

echo -e "${BLUE}╔══════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  SafeSignal ESP32-S3 Alert Queue Test Suite         ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════════╝${NC}"
echo ""

# Check prerequisites
check_prerequisites() {
    echo -e "${YELLOW}[CHECK]${NC} Verifying prerequisites..."

    # Check serial port
    if [ ! -e "$SERIAL_PORT" ]; then
        echo -e "${RED}[ERROR]${NC} Serial port $SERIAL_PORT not found"
        echo "Available ports:"
        ls /dev/tty* | grep -E "(USB|ACM)" || echo "  None found"
        exit 1
    fi
    echo -e "${GREEN}[OK]${NC} Serial port: $SERIAL_PORT"

    # Check Docker
    if ! command -v docker &> /dev/null; then
        echo -e "${YELLOW}[WARN]${NC} Docker not found (MQTT tests will be skipped)"
        SKIP_MQTT=1
    else
        echo -e "${GREEN}[OK]${NC} Docker installed"
    fi

    # Check MQTT container
    if [ -z "$SKIP_MQTT" ]; then
        if docker ps -a | grep -q "$MQTT_CONTAINER"; then
            MQTT_STATUS=$(docker ps --filter "name=$MQTT_CONTAINER" --format "{{.Status}}")
            if [[ $MQTT_STATUS == Up* ]]; then
                echo -e "${GREEN}[OK]${NC} MQTT broker running: $MQTT_CONTAINER"
            else
                echo -e "${YELLOW}[WARN]${NC} MQTT broker exists but not running"
                echo "  Run: docker start $MQTT_CONTAINER"
            fi
        else
            echo -e "${YELLOW}[WARN]${NC} MQTT broker not found (some tests will be skipped)"
            SKIP_MQTT=1
        fi
    fi

    echo ""
}

# Test 1: Basic connectivity
test_basic_connectivity() {
    echo -e "${BLUE}[TEST 1]${NC} Basic Connectivity"
    echo "  Press Ctrl+] to exit monitor, then run this script again"
    echo "  Checking for startup messages..."
    echo ""

    # Monitor serial for 10 seconds
    timeout 10 cat "$SERIAL_PORT" 2>/dev/null | tee /tmp/esp32_test.log | head -n 50 || true

    # Check for key initialization messages
    if grep -q "READY.*System initialized" /tmp/esp32_test.log; then
        echo -e "${GREEN}[PASS]${NC} Device initialized successfully"
    else
        echo -e "${RED}[FAIL]${NC} Device initialization not detected"
        return 1
    fi

    if grep -q "ALERT_QUEUE.*Initialized" /tmp/esp32_test.log; then
        echo -e "${GREEN}[PASS]${NC} Alert queue initialized"
    else
        echo -e "${RED}[FAIL]${NC} Alert queue not initialized"
        return 1
    fi

    if grep -q "WATCHDOG.*Initialized" /tmp/esp32_test.log; then
        echo -e "${GREEN}[PASS]${NC} Watchdog initialized"
    else
        echo -e "${RED}[FAIL]${NC} Watchdog not initialized"
        return 1
    fi

    if grep -q "TIME.*Synchronized" /tmp/esp32_test.log; then
        echo -e "${GREEN}[PASS]${NC} Time synchronized"
    else
        echo -e "${YELLOW}[WARN]${NC} Time not synchronized (may take longer)"
    fi

    echo ""
}

# Test 2: Alert queueing with MQTT disconnect
test_alert_persistence() {
    if [ ! -z "$SKIP_MQTT" ]; then
        echo -e "${YELLOW}[SKIP]${NC} Test 2: Alert Persistence (MQTT not available)"
        return 0
    fi

    echo -e "${BLUE}[TEST 2]${NC} Alert Persistence with MQTT Disconnect"
    echo ""

    # Step 1: Disconnect MQTT
    echo "  [1/5] Stopping MQTT broker..."
    docker stop "$MQTT_CONTAINER" > /dev/null 2>&1 || true
    sleep 2
    echo -e "        ${GREEN}Done${NC}"

    # Step 2: Monitor serial and prompt for button presses
    echo "  [2/5] Monitoring device..."
    echo "        ${YELLOW}MANUAL ACTION REQUIRED:${NC}"
    echo "        Press the BOOT button (GPIO0) on the ESP32 ${YELLOW}3 times${NC}"
    echo "        Press Enter when done..."
    read -p ""

    # Step 3: Check queue
    echo "  [3/5] Checking alert queue..."
    timeout 5 cat "$SERIAL_PORT" 2>/dev/null | grep -i "queued" | tail -n 3

    # Step 4: Restart MQTT
    echo "  [4/5] Restarting MQTT broker..."
    docker start "$MQTT_CONTAINER" > /dev/null 2>&1
    sleep 3
    echo -e "        ${GREEN}Done${NC}"

    # Step 5: Check for delivery
    echo "  [5/5] Checking for alert delivery..."
    timeout 10 cat "$SERIAL_PORT" 2>/dev/null | grep -i "delivered" | tail -n 5 || true

    # Verify
    DELIVERED=$(timeout 5 cat "$SERIAL_PORT" 2>/dev/null | grep -c "✓.*delivered" || echo "0")
    if [ "$DELIVERED" -ge "1" ]; then
        echo -e "${GREEN}[PASS]${NC} Alerts delivered after reconnect ($DELIVERED alerts)"
    else
        echo -e "${RED}[FAIL]${NC} Alerts not delivered"
        return 1
    fi

    echo ""
}

# Test 3: Queue statistics
test_queue_stats() {
    echo -e "${BLUE}[TEST 3]${NC} Queue Statistics"
    echo ""

    echo "  Querying alert queue stats..."
    timeout 10 cat "$SERIAL_PORT" 2>/dev/null | grep -i "queue.*stats\|pending" | tail -n 5 || true

    echo -e "${GREEN}[INFO]${NC} Check logs above for queue statistics"
    echo ""
}

# Test 4: Memory usage
test_memory() {
    echo -e "${BLUE}[TEST 4]${NC} Memory Usage"
    echo ""

    echo "  Checking heap usage..."
    timeout 10 cat "$SERIAL_PORT" 2>/dev/null | grep -i "heap\|mem" | tail -n 5 || true

    echo -e "${GREEN}[INFO]${NC} Check logs above for memory statistics"
    echo ""
}

# Summary
print_summary() {
    echo -e "${BLUE}╔══════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║  Test Summary                                        ║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════════════════════╝${NC}"
    echo ""
    echo "Manual verification checklist:"
    echo "  [ ] Device boots and initializes all subsystems"
    echo "  [ ] Alert queue initialized with correct pending count"
    echo "  [ ] Watchdog monitoring active"
    echo "  [ ] Time synchronized via NTP"
    echo "  [ ] Alerts queued when MQTT disconnected"
    echo "  [ ] Alerts automatically delivered on reconnect"
    echo "  [ ] Queue statistics accurate"
    echo "  [ ] Memory usage stable"
    echo ""
    echo "Full logs saved to: /tmp/esp32_test.log"
    echo ""
    echo "Next steps:"
    echo "  1. Review full serial output: cat $SERIAL_PORT"
    echo "  2. Run 24-hour uptime test"
    echo "  3. Test power cycle persistence"
    echo "  4. Measure alert latency"
    echo ""
}

# Main execution
main() {
    check_prerequisites

    # Run tests
    test_basic_connectivity || exit 1
    test_alert_persistence
    test_queue_stats
    test_memory

    print_summary
}

# Help message
if [ "$1" = "-h" ] || [ "$1" = "--help" ]; then
    echo "Usage: $0 [serial_port]"
    echo ""
    echo "Arguments:"
    echo "  serial_port   Path to ESP32 serial port (default: /dev/ttyUSB0)"
    echo ""
    echo "Examples:"
    echo "  $0                           # Use default /dev/ttyUSB0"
    echo "  $0 /dev/tty.SLAB_USBtoUART   # macOS"
    echo "  $0 /dev/ttyUSB1              # Linux alternate port"
    echo ""
    exit 0
fi

main
