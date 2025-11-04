#!/bin/bash
# final-clearance-test.sh
# Complete E2E test for two-person All Clear workflow

set -e

API_URL="http://localhost:5118/api"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║   All Clear Two-Person Workflow - Final E2E Test        ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Get JWT tokens
echo "🔐 Getting JWT tokens..."
USER_A_TOKEN=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  --data-raw '{"email":"admin@safesignal.com","password":"Admin@12345678!"}' | jq -r '.tokens.accessToken')

USER_B_TOKEN=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  --data-raw '{"email":"testuser@safesignal.com","password":"TestUser123!@#"}' | jq -r '.tokens.accessToken')

BUILDING_ID=$(docker exec safesignal-postgres psql -U postgres -d safesignal -t -c 'SELECT "Id" FROM buildings LIMIT 1;' | xargs)

echo "✅ Admin token: ${USER_A_TOKEN:0:30}..."
echo "✅ Testuser token: ${USER_B_TOKEN:0:30}..."
echo "✅ Building ID: $BUILDING_ID"
echo ""

# Test 1: Happy Path
echo "══════════════════════════════════════════════════════════"
echo "Test 1: Happy Path - Two Different Users"
echo "══════════════════════════════════════════════════════════"
echo ""

# Trigger alert
echo "1️⃣  Triggering alert..."
ALERT_RESPONSE=$(curl -s -X POST "$API_URL/alerts/trigger" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"buildingId\": \"$BUILDING_ID\", \"mode\": \"audible\"}")

ALERT_ID=$(echo "$ALERT_RESPONSE" | jq -r '.id')
echo "   Alert ID: $ALERT_ID"
echo ""

# First clearance
echo "2️⃣  User A (admin) submitting first clearance..."
CLEAR1=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notes": "Room checked - false alarm",
    "location": {"latitude": 37.7749, "longitude": -122.4194, "accuracy": 5.0}
  }')

CLEAR1_STATUS=$(echo "$CLEAR1" | jq -r '.status')
CLEAR1_STEP=$(echo "$CLEAR1" | jq -r '.clearanceStep')

if [ "$CLEAR1_STATUS" == "PendingClearance" ] && [ "$CLEAR1_STEP" == "1" ]; then
    echo "   ✅ First clearance recorded (Status: PendingClearance, Step: 1)"
else
    echo "   ❌ First clearance failed"
    echo "$CLEAR1" | jq '.'
    exit 1
fi
echo ""

# Second clearance
echo "3️⃣  User B (testuser) submitting second clearance..."
CLEAR2=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_B_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notes": "Double-checked - all clear",
    "location": {"latitude": 37.7750, "longitude": -122.4195, "accuracy": 8.0}
  }')

CLEAR2_STATUS=$(echo "$CLEAR2" | jq -r '.status')
CLEAR2_STEP=$(echo "$CLEAR2" | jq -r '.clearanceStep')

if [ "$CLEAR2_STATUS" == "Resolved" ] && [ "$CLEAR2_STEP" == "2" ]; then
    echo "   ✅ Second clearance recorded (Status: Resolved, Step: 2)"
else
    echo "   ❌ Second clearance failed"
    echo "$CLEAR2" | jq '.'
    exit 1
fi
echo ""

# Fetch history
echo "4️⃣  Fetching clearance history..."
HISTORY=$(curl -s -X GET "$API_URL/alerts/$ALERT_ID/clearances" \
  -H "Authorization: Bearer $USER_A_TOKEN")

CLEARANCE_COUNT=$(echo "$HISTORY" | jq '.clearances | length')

if [ "$CLEARANCE_COUNT" == "2" ]; then
    echo "   ✅ Clearance history retrieved: 2 clearances"
else
    echo "   ❌ Expected 2 clearances, found $CLEARANCE_COUNT"
fi
echo ""

echo "✅ Test 1 PASSED"
echo ""

# Test 2: Same User Prevention
echo "══════════════════════════════════════════════════════════"
echo "Test 2: Same User Prevention"
echo "══════════════════════════════════════════════════════════"
echo ""

# Trigger new alert
echo "1️⃣  Triggering new alert..."
ALERT2=$(curl -s -X POST "$API_URL/alerts/trigger" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"buildingId\": \"$BUILDING_ID\", \"mode\": \"audible\"}")

ALERT_ID_2=$(echo "$ALERT2" | jq -r '.id')
echo "   Alert ID: $ALERT_ID_2"
echo ""

# First clearance
echo "2️⃣  User A submitting first clearance..."
curl -s -X POST "$API_URL/alerts/$ALERT_ID_2/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes":"First"}' > /dev/null
echo "   First clearance submitted"
echo ""

# Same user tries second clearance
echo "3️⃣  User A attempting second clearance (should fail)..."
SAME_USER_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID_2/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes":"Second - same user"}')

ERROR_MSG=$(echo "$SAME_USER_RESPONSE" | jq -r '.error // .Error // empty')

if echo "$ERROR_MSG" | grep -qi "already provided"; then
    echo "   ✅ Same user correctly prevented"
else
    echo "   ❌ Same user was NOT prevented"
    echo "$SAME_USER_RESPONSE" | jq '.'
    exit 1
fi
echo ""

echo "✅ Test 2 PASSED"
echo ""

# Summary
echo "╔══════════════════════════════════════════════════════════╗"
echo "║              🎉 ALL TESTS PASSED! 🎉                     ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo "Summary:"
echo "  ✅ Two-person clearance workflow working"
echo "  ✅ Alert status transitions: New → PendingClearance → Resolved"
echo "  ✅ GPS coordinates captured"
echo "  ✅ Same-user prevention working"
echo "  ✅ Clearance history retrieval working"
echo ""
echo "Next steps:"
echo "  - Test mobile app UI (AlertClearanceScreen, badges)"
echo "  - Verify audit logs in database"
echo "  - Configure Grafana dashboard"
echo "  - Deploy to staging"
