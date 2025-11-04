#!/bin/bash
# run-e2e-test.sh
# Complete E2E test runner - creates users, gets tokens, runs tests

set -e

API_URL="http://localhost:5118/api"
ORG_ID="a216abd0-2c87-4828-8823-48dc8c9f0a8a"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║   All Clear Two-Person Workflow - E2E Test Runner       ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Step 1: Create or use existing test users
echo "Step 1: Setting up test users..."
echo "  → Creating alice@test.com (if not exists)..."
curl -s -X POST "$API_URL/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"alice@test.com\",\"password\":\"TestPassword123!\",\"firstName\":\"Alice\",\"lastName\":\"Test\",\"organizationId\":\"$ORG_ID\"}" > /dev/null 2>&1 || true

echo "  → Creating bob@test.com (if not exists)..."
curl -s -X POST "$API_URL/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"bob@test.com\",\"password\":\"TestPassword123!\",\"firstName\":\"Bob\",\"lastName\":\"Test\",\"organizationId\":\"$ORG_ID\"}" > /dev/null 2>&1 || true

echo "  ✅ Test users ready"
echo ""

# Step 2: Get JWT tokens
echo "Step 2: Getting JWT tokens..."
echo "  → Logging in as alice@test.com..."
ALICE_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@test.com","password":"TestPassword123!"}')

USER_A_TOKEN=$(echo "$ALICE_RESPONSE" | jq -r '.token')
if [ "$USER_A_TOKEN" == "null" ] || [ -z "$USER_A_TOKEN" ]; then
    echo "  ❌ Failed to get token for Alice"
    echo "$ALICE_RESPONSE" | jq '.'
    exit 1
fi
echo "  ✅ Got token for Alice"

echo "  → Logging in as bob@test.com..."
BOB_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"bob@test.com","password":"TestPassword123!"}')

USER_B_TOKEN=$(echo "$BOB_RESPONSE" | jq -r '.token')
if [ "$USER_B_TOKEN" == "null" ] || [ -z "$USER_B_TOKEN" ]; then
    echo "  ❌ Failed to get token for Bob"
    echo "$BOB_RESPONSE" | jq '.'
    exit 1
fi
echo "  ✅ Got token for Bob"
echo ""

# Step 3: Get building ID
echo "Step 3: Getting building ID from database..."
BUILDING_ID=$(docker exec safesignal-postgres psql -U postgres -d safesignal -t -c 'SELECT "Id" FROM buildings LIMIT 1;' | xargs)
if [ -z "$BUILDING_ID" ]; then
    echo "  ❌ Failed to get building ID"
    exit 1
fi
echo "  ✅ Building ID: $BUILDING_ID"
echo ""

# Step 4: Export variables and run tests
echo "Step 4: Running E2E tests..."
echo ""

export USER_A_TOKEN
export USER_B_TOKEN
export BUILDING_ID

./scripts/test-all-clear-workflow.sh
