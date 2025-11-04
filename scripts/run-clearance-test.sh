#!/bin/bash
# run-clearance-test.sh
# Complete E2E test with credentials from CREDENTIALS.md

set -e

API_URL="http://localhost:5118/api"
ORG_ID="a216abd0-2c87-4828-8823-48dc8c9f0a8a"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║   All Clear Two-Person Workflow - E2E Test              ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Step 1: Get JWT tokens using existing credentials
echo "Step 1: Getting JWT tokens..."
echo "  → Logging in as admin@safesignal.com..."
ADMIN_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@safesignal.com","password":"Admin@12345678!"}')

USER_A_TOKEN=$(echo "$ADMIN_RESPONSE" | jq -r '.tokens.accessToken // .token')
if [ "$USER_A_TOKEN" == "null" ] || [ -z "$USER_A_TOKEN" ]; then
    echo "  ❌ Failed to get token for admin"
    echo "$ADMIN_RESPONSE" | jq '.'
    exit 1
fi
echo "  ✅ Got token for admin@safesignal.com"

echo "  → Logging in as testuser@safesignal.com..."
TESTUSER_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@safesignal.com","password":"TestUser123!@#"}')

USER_B_TOKEN=$(echo "$TESTUSER_RESPONSE" | jq -r '.tokens.accessToken // .token')
if [ "$USER_B_TOKEN" == "null" ] || [ -z "$USER_B_TOKEN" ]; then
    echo "  ❌ Failed to get token for testuser"
    echo "$TESTUSER_RESPONSE" | jq '.'
    exit 1
fi
echo "  ✅ Got token for testuser@safesignal.com"
echo ""

# Step 2: Get building ID
echo "Step 2: Getting building ID from database..."
BUILDING_ID=$(docker exec safesignal-postgres psql -U postgres -d safesignal -t -c 'SELECT "Id" FROM buildings LIMIT 1;' | xargs)
if [ -z "$BUILDING_ID" ]; then
    echo "  ❌ Failed to get building ID"
    exit 1
fi
echo "  ✅ Building ID: $BUILDING_ID"
echo ""

# Step 3: Export variables and run tests
echo "Step 3: Running E2E tests..."
echo ""

export USER_A_TOKEN
export USER_B_TOKEN
export BUILDING_ID

./scripts/test-all-clear-workflow.sh
