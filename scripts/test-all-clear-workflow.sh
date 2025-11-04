#!/bin/bash
# test-all-clear-workflow.sh
# End-to-end test for two-person All Clear workflow
#
# Prerequisites:
# - Backend API running on port 5118
# - PostgreSQL database with alert_clearances table
# - Two valid JWT tokens (User A and User B from different users)
# - jq installed (brew install jq)

set -e

# Configuration
API_URL="${API_URL:-http://localhost:5118/api}"
USER_A_TOKEN="${USER_A_TOKEN:-}"
USER_B_TOKEN="${USER_B_TOKEN:-}"
BUILDING_ID="${BUILDING_ID:-}"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check prerequisites
check_prerequisites() {
    echo "=== Checking Prerequisites ==="

    if ! command -v jq &> /dev/null; then
        echo -e "${RED}❌ jq is not installed. Install with: brew install jq${NC}"
        exit 1
    fi

    if [ -z "$USER_A_TOKEN" ]; then
        echo -e "${RED}❌ USER_A_TOKEN environment variable not set${NC}"
        echo "Example: export USER_A_TOKEN='eyJ...'"
        exit 1
    fi

    if [ -z "$USER_B_TOKEN" ]; then
        echo -e "${RED}❌ USER_B_TOKEN environment variable not set${NC}"
        echo "Example: export USER_B_TOKEN='eyJ...'"
        exit 1
    fi

    if [ -z "$BUILDING_ID" ]; then
        echo -e "${RED}❌ BUILDING_ID environment variable not set${NC}"
        echo "Example: export BUILDING_ID='uuid-of-building'"
        exit 1
    fi

    # Check API by trying a simple endpoint
    if ! curl -s "$API_URL/alerts" -H "Authorization: Bearer $USER_A_TOKEN" > /dev/null 2>&1; then
        echo -e "${YELLOW}⚠️  Warning: Could not verify API health, but continuing anyway${NC}"
    fi

    echo -e "${GREEN}✅ All prerequisites met${NC}"
    echo ""
}

# Test 1: Happy Path - Two Different Users
test_happy_path() {
    echo "=== Test 1: Happy Path - Two Different Users ==="

    # Trigger alert
    echo "1️⃣  Triggering alert..."
    ALERT_RESPONSE=$(curl -s -X POST "$API_URL/alerts/trigger" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"buildingId\": \"$BUILDING_ID\", \"mode\": \"audible\"}")

    ALERT_ID=$(echo "$ALERT_RESPONSE" | jq -r '.id')
    ALERT_STATUS=$(echo "$ALERT_RESPONSE" | jq -r '.status')

    if [ "$ALERT_ID" == "null" ]; then
        echo -e "${RED}❌ Failed to create alert${NC}"
        echo "$ALERT_RESPONSE" | jq '.'
        exit 1
    fi

    echo -e "${GREEN}✅ Alert created: $ALERT_ID (Status: $ALERT_STATUS)${NC}"
    sleep 1

    # First clearance (User A)
    echo "2️⃣  User A submitting first clearance..."
    CLEAR1_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{
        "notes": "Test clearance 1 - Room checked by User A",
        "location": {"latitude": 37.7749, "longitude": -122.4194, "accuracy": 5.0}
      }')

    CLEAR1_STATUS=$(echo "$CLEAR1_RESPONSE" | jq -r '.status')
    CLEAR1_MESSAGE=$(echo "$CLEAR1_RESPONSE" | jq -r '.message')
    CLEAR1_STEP=$(echo "$CLEAR1_RESPONSE" | jq -r '.clearanceStep')

    if [ "$CLEAR1_STATUS" == "PendingClearance" ] && [ "$CLEAR1_STEP" == "1" ]; then
        echo -e "${GREEN}✅ First clearance recorded${NC}"
        echo "   Status: $CLEAR1_STATUS"
        echo "   Message: $CLEAR1_MESSAGE"
    else
        echo -e "${RED}❌ First clearance failed${NC}"
        echo "$CLEAR1_RESPONSE" | jq '.'
        exit 1
    fi
    sleep 1

    # Second clearance (User B)
    echo "3️⃣  User B submitting second clearance..."
    CLEAR2_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_B_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{
        "notes": "Test clearance 2 - Double-checked by User B",
        "location": {"latitude": 37.7750, "longitude": -122.4195, "accuracy": 8.0}
      }')

    CLEAR2_STATUS=$(echo "$CLEAR2_RESPONSE" | jq -r '.status')
    CLEAR2_MESSAGE=$(echo "$CLEAR2_RESPONSE" | jq -r '.message')
    CLEAR2_STEP=$(echo "$CLEAR2_RESPONSE" | jq -r '.clearanceStep')

    if [ "$CLEAR2_STATUS" == "Resolved" ] && [ "$CLEAR2_STEP" == "2" ]; then
        echo -e "${GREEN}✅ Second clearance recorded${NC}"
        echo "   Status: $CLEAR2_STATUS"
        echo "   Message: $CLEAR2_MESSAGE"
    else
        echo -e "${RED}❌ Second clearance failed${NC}"
        echo "$CLEAR2_RESPONSE" | jq '.'
        exit 1
    fi
    sleep 1

    # Fetch clearance history
    echo "4️⃣  Fetching clearance history..."
    HISTORY=$(curl -s -X GET "$API_URL/alerts/$ALERT_ID/clearances" \
      -H "Authorization: Bearer $USER_A_TOKEN")

    CLEARANCE_COUNT=$(echo "$HISTORY" | jq '.clearances | length')

    if [ "$CLEARANCE_COUNT" == "2" ]; then
        echo -e "${GREEN}✅ Clearance history retrieved: 2 clearances found${NC}"
        echo "$HISTORY" | jq '{status, clearances: [.clearances[] | {step: .clearanceStep, userName, notes, location}]}'
    else
        echo -e "${RED}❌ Expected 2 clearances, found $CLEARANCE_COUNT${NC}"
        echo "$HISTORY" | jq '.'
        exit 1
    fi

    echo -e "${GREEN}✅ Test 1 PASSED${NC}"
    echo ""
}

# Test 2: Same User Prevention
test_same_user_prevention() {
    echo "=== Test 2: Same User Prevention ==="

    # Trigger alert
    echo "1️⃣  Triggering alert..."
    ALERT_RESPONSE=$(curl -s -X POST "$API_URL/alerts/trigger" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"buildingId\": \"$BUILDING_ID\", \"mode\": \"audible\"}")

    ALERT_ID=$(echo "$ALERT_RESPONSE" | jq -r '.id')
    echo "   Alert: $ALERT_ID"
    sleep 1

    # First clearance
    echo "2️⃣  User A submitting first clearance..."
    curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"notes": "First clearance by User A"}' > /dev/null
    echo "   First clearance submitted"
    sleep 1

    # Attempt second clearance with same user
    echo "3️⃣  User A attempting second clearance (should fail)..."
    CLEAR2_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"notes": "Second clearance by same user"}')

    ERROR_MSG=$(echo "$CLEAR2_RESPONSE" | jq -r '.error // .Error // empty')

    if echo "$ERROR_MSG" | grep -qi "already provided"; then
        echo -e "${GREEN}✅ Same user correctly prevented from second clearance${NC}"
        echo "   Error: $ERROR_MSG"
    else
        echo -e "${RED}❌ Same user was NOT prevented from second clearance${NC}"
        echo "$CLEAR2_RESPONSE" | jq '.'
        exit 1
    fi

    echo -e "${GREEN}✅ Test 2 PASSED${NC}"
    echo ""
}

# Test 3: Already Resolved Alert
test_already_resolved() {
    echo "=== Test 3: Already Resolved Alert ==="

    # Trigger and fully clear an alert
    echo "1️⃣  Creating and fully clearing an alert..."
    ALERT_RESPONSE=$(curl -s -X POST "$API_URL/alerts/trigger" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"buildingId\": \"$BUILDING_ID\"}")

    ALERT_ID=$(echo "$ALERT_RESPONSE" | jq -r '.id')
    echo "   Alert: $ALERT_ID"

    # First clearance
    curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"notes": "First"}' > /dev/null

    sleep 1

    # Second clearance
    curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_B_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"notes": "Second"}' > /dev/null

    echo "   Alert fully resolved"
    sleep 1

    # Attempt third clearance
    echo "2️⃣  Attempting clearance on resolved alert (should fail)..."
    CLEAR3_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
      -H "Authorization: Bearer $USER_A_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"notes": "Third clearance"}')

    ERROR_MSG=$(echo "$CLEAR3_RESPONSE" | jq -r '.error // .Error // empty')

    if echo "$ERROR_MSG" | grep -qi "already.*resolved"; then
        echo -e "${GREEN}✅ System correctly prevented clearing resolved alert${NC}"
        echo "   Error: $ERROR_MSG"
    else
        echo -e "${RED}❌ System did NOT prevent clearing resolved alert${NC}"
        echo "$CLEAR3_RESPONSE" | jq '.'
        exit 1
    fi

    echo -e "${GREEN}✅ Test 3 PASSED${NC}"
    echo ""
}

# Run all tests
main() {
    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║       All Clear Two-Person Workflow E2E Tests           ║"
    echo "╚══════════════════════════════════════════════════════════╝"
    echo ""

    check_prerequisites

    test_happy_path
    test_same_user_prevention
    test_already_resolved

    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║              🎉 ALL TESTS PASSED 🎉                      ║"
    echo "╚══════════════════════════════════════════════════════════╝"
}

main
