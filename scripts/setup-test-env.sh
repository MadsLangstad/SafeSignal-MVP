#!/bin/bash
# setup-test-env.sh
# Helper script to get JWT tokens and set up test environment

set -e

API_URL="${API_URL:-http://localhost:5118/api}"
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "╔══════════════════════════════════════════════════════════╗"
echo "║       SafeSignal Test Environment Setup                 ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Check if API is running
echo "Checking API health..."
if ! curl -s -f "$API_URL/../health" > /dev/null 2>&1; then
    echo -e "${RED}❌ API not reachable at $API_URL${NC}"
    echo "Please start the backend: cd cloud-backend && dotnet run --project src/Api"
    exit 1
fi
echo -e "${GREEN}✅ API is running${NC}"
echo ""

# Function to login and get token
get_token() {
    local email=$1
    local password=$2

    echo "Attempting login for: $email"

    RESPONSE=$(curl -s -X POST "$API_URL/auth/login" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$email\",\"password\":\"$password\"}" 2>&1)

    TOKEN=$(echo "$RESPONSE" | jq -r '.token // empty' 2>/dev/null)

    if [ -z "$TOKEN" ] || [ "$TOKEN" == "null" ]; then
        echo -e "${RED}❌ Login failed for $email${NC}"
        echo "Response: $RESPONSE"
        return 1
    fi

    echo -e "${GREEN}✅ Login successful${NC}"
    echo "$TOKEN"
}

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo -e "${YELLOW}⚠️  jq is not installed${NC}"
    echo "Install with: brew install jq"
    echo ""
fi

# Check if psql is available for database queries
if command -v psql &> /dev/null; then
    echo "Fetching available users from database..."
    echo ""

    USERS=$(psql safesignal_dev -t -c "SELECT email, first_name, last_name FROM users LIMIT 10;" 2>/dev/null || echo "")

    if [ -n "$USERS" ]; then
        echo -e "${BLUE}Available users in database:${NC}"
        echo "$USERS" | head -5
        echo ""
    fi

    echo "Fetching buildings..."
    BUILDINGS=$(psql safesignal_dev -t -c "SELECT id, name FROM buildings LIMIT 5;" 2>/dev/null || echo "")

    if [ -n "$BUILDINGS" ]; then
        echo -e "${BLUE}Available buildings:${NC}"
        echo "$BUILDINGS"
        echo ""
    fi
fi

echo "════════════════════════════════════════════════════════════"
echo -e "${YELLOW}Setup Instructions:${NC}"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "1. Login to get JWT tokens for two different users:"
echo ""
echo -e "${BLUE}   For User A:${NC}"
echo "   USER_A_TOKEN=\$(curl -s -X POST $API_URL/auth/login \\"
echo "     -H 'Content-Type: application/json' \\"
echo "     -d '{\"email\":\"USER_A_EMAIL\",\"password\":\"PASSWORD\"}' | jq -r '.token')"
echo ""
echo -e "${BLUE}   For User B:${NC}"
echo "   USER_B_TOKEN=\$(curl -s -X POST $API_URL/auth/login \\"
echo "     -H 'Content-Type: application/json' \\"
echo "     -d '{\"email\":\"USER_B_EMAIL\",\"password\":\"PASSWORD\"}' | jq -r '.token')"
echo ""
echo "2. Get a building ID from the database:"
echo "   BUILDING_ID=\$(psql safesignal_dev -t -c \"SELECT id FROM buildings LIMIT 1;\" | xargs)"
echo ""
echo "3. Export the variables:"
echo "   export USER_A_TOKEN"
echo "   export USER_B_TOKEN"
echo "   export BUILDING_ID"
echo ""
echo "4. Run the test:"
echo "   ./scripts/test-all-clear-workflow.sh"
echo ""
echo "════════════════════════════════════════════════════════════"
echo -e "${YELLOW}Quick Test Setup (if you have test users):${NC}"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "If you have test users with known credentials, you can run:"
echo ""
echo "# Example with test@example.com and test2@example.com"
echo "export USER_A_TOKEN=\$(curl -s -X POST $API_URL/auth/login \\"
echo "  -H 'Content-Type: application/json' \\"
echo "  -d '{\"email\":\"test@example.com\",\"password\":\"Test123!\"}' | jq -r '.token')"
echo ""
echo "export USER_B_TOKEN=\$(curl -s -X POST $API_URL/auth/login \\"
echo "  -H 'Content-Type: application/json' \\"
echo "  -d '{\"email\":\"test2@example.com\",\"password\":\"Test123!\"}' | jq -r '.token')"
echo ""
echo "export BUILDING_ID=\$(psql safesignal_dev -t -c \"SELECT id FROM buildings LIMIT 1;\" | xargs)"
echo ""
echo "echo \"Tokens and Building ID set:\""
echo "echo \"USER_A_TOKEN: \${USER_A_TOKEN:0:20}...\""
echo "echo \"USER_B_TOKEN: \${USER_B_TOKEN:0:20}...\""
echo "echo \"BUILDING_ID: \$BUILDING_ID\""
echo ""
echo "════════════════════════════════════════════════════════════"
echo -e "${YELLOW}Create Test Users (if needed):${NC}"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "If you don't have test users, you can create them via API:"
echo ""
echo "curl -X POST $API_URL/auth/register \\"
echo "  -H 'Content-Type: application/json' \\"
echo "  -d '{"
echo "    \"email\":\"testuser1@example.com\","
echo "    \"password\":\"Test123!\","
echo "    \"firstName\":\"Test\","
echo "    \"lastName\":\"User One\","
echo "    \"organizationId\":\"YOUR_ORG_ID\""
echo "  }'"
echo ""
echo "Repeat for second user with different email."
echo ""
