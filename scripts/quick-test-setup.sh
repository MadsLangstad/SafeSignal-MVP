#!/bin/bash
# quick-test-setup.sh
# Quick setup for running E2E tests with existing test users

set -e

API_URL="http://localhost:5118/api"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║     Quick Test Setup - All Clear Workflow               ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Try to login with known test users
echo "🔐 Getting JWT token for User A (testuser@safesignal.com)..."
USER_A_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@safesignal.com","password":"Test123!"}')

export USER_A_TOKEN=$(echo "$USER_A_RESPONSE" | jq -r '.token // empty')

if [ -z "$USER_A_TOKEN" ] || [ "$USER_A_TOKEN" == "null" ]; then
    echo "❌ Failed to get token for User A"
    echo "Response: $USER_A_RESPONSE"
    echo ""
    echo "Trying test@example.com instead..."

    USER_A_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
      -H "Content-Type: application/json" \
      -d '{"email":"test@example.com","password":"Test123!"}')

    export USER_A_TOKEN=$(echo "$USER_A_RESPONSE" | jq -r '.token // empty')

    if [ -z "$USER_A_TOKEN" ] || [ "$USER_A_TOKEN" == "null" ]; then
        echo "❌ Failed to get token for test@example.com"
        echo "Please check user credentials or create test users."
        exit 1
    fi
    echo "✅ Got token for test@example.com"
else
    echo "✅ Got token for testuser@safesignal.com"
fi

echo ""
echo "🔐 Getting JWT token for User B (admin@safesignal.com)..."
USER_B_RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@safesignal.com","password":"Admin123!"}')

export USER_B_TOKEN=$(echo "$USER_B_RESPONSE" | jq -r '.token // empty')

if [ -z "$USER_B_TOKEN" ] || [ "$USER_B_TOKEN" == "null" ]; then
    echo "❌ Failed to get token for User B"
    echo "Response: $USER_B_RESPONSE"
    echo ""
    echo "You'll need to provide a second test user email/password."
    exit 1
fi
echo "✅ Got token for admin@safesignal.com"

echo ""
echo "🏢 Getting building ID from database..."
export BUILDING_ID=$(docker exec safesignal-postgres psql -U postgres -d safesignal -t -c 'SELECT "Id" FROM buildings LIMIT 1;' | xargs)

if [ -z "$BUILDING_ID" ]; then
    echo "❌ Failed to get building ID from database"
    exit 1
fi
echo "✅ Got building ID: $BUILDING_ID"

echo ""
echo "════════════════════════════════════════════════════════════"
echo "✅ Environment Setup Complete!"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "Environment variables set:"
echo "USER_A_TOKEN: ${USER_A_TOKEN:0:30}..."
echo "USER_B_TOKEN: ${USER_B_TOKEN:0:30}..."
echo "BUILDING_ID: $BUILDING_ID"
echo ""
echo "════════════════════════════════════════════════════════════"
echo "Running E2E tests..."
echo "════════════════════════════════════════════════════════════"
echo ""

# Run the E2E test
./scripts/test-all-clear-workflow.sh
