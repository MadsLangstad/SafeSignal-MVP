#!/bin/bash
# Test the alert clearance endpoint with proper authentication

set -e

API_URL="${API_URL:-http://localhost:5118}"
ALERT_ID="e7774503-ad77-441e-b599-b4f87fd24c27"

echo "=== Testing Alert Clearance Endpoint ==="
echo "API URL: $API_URL"
echo "Alert ID: $ALERT_ID"
echo ""

# Step 1: Login
echo "1. Logging in..."
LOGIN_JSON=$(cat <<'EOF'
{
  "email": "admin@safesignal.com",
  "password": "Admin@12345678!"
}
EOF
)

LOGIN_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "$LOGIN_JSON")

# Check for login errors
if echo "$LOGIN_RESPONSE" | grep -q '"error"'; then
  echo "❌ Login failed:"
  echo "$LOGIN_RESPONSE" | jq '.'
  exit 1
fi

# Extract token
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.tokens.accessToken')

if [ -z "$ACCESS_TOKEN" ] || [ "$ACCESS_TOKEN" = "null" ]; then
  echo "❌ Failed to get access token:"
  echo "$LOGIN_RESPONSE" | jq '.'
  exit 1
fi

echo "✅ Login successful"
echo ""

# Decode token to verify organizationId
echo "2. Verifying token contains organizationId..."
PAYLOAD=$(echo "$ACCESS_TOKEN" | cut -d'.' -f2)
case $((${#PAYLOAD} % 4)) in
  2) PAYLOAD="${PAYLOAD}==" ;;
  3) PAYLOAD="${PAYLOAD}=" ;;
esac

ORG_ID=$(echo "$PAYLOAD" | base64 -d 2>/dev/null | jq -r '.organizationId')
echo "Token organizationId: $ORG_ID"
echo ""

# Step 2: Get Alert
echo "3. Testing GET /api/alerts/$ALERT_ID..."
GET_RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/api/alerts/$ALERT_ID" \
  -H "Authorization: Bearer $ACCESS_TOKEN")

HTTP_CODE=$(echo "$GET_RESPONSE" | tail -n1)
BODY=$(echo "$GET_RESPONSE" | sed '$ d')

echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
  echo "✅ GET successful"
  echo "$BODY" | jq '.'
elif [ "$HTTP_CODE" = "404" ]; then
  echo "❌ GET failed with 404"
  echo "$BODY" | jq '.' || echo "$BODY"
else
  echo "❌ GET failed with status $HTTP_CODE"
  echo "$BODY" | jq '.' || echo "$BODY"
fi
echo ""

# Step 3: Get Clearances
echo "4. Testing GET /api/alerts/$ALERT_ID/clearances..."
CLEARANCES_RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/api/alerts/$ALERT_ID/clearances" \
  -H "Authorization: Bearer $ACCESS_TOKEN")

HTTP_CODE=$(echo "$CLEARANCES_RESPONSE" | tail -n1)
BODY=$(echo "$CLEARANCES_RESPONSE" | sed '$ d')

echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
  echo "✅ GET clearances successful"
  echo "$BODY" | jq '.'
elif [ "$HTTP_CODE" = "404" ]; then
  echo "❌ GET clearances failed with 404"
  echo "$BODY" | jq '.' || echo "$BODY"
else
  echo "❌ GET clearances failed with status $HTTP_CODE"
  echo "$BODY" | jq '.' || echo "$BODY"
fi
echo ""

# Step 4: Try to Clear Alert
echo "5. Testing POST /api/alerts/$ALERT_ID/clear..."
CLEAR_JSON=$(cat <<'EOF'
{
  "notes": "Test clearance from script",
  "location": {
    "latitude": 37.7749,
    "longitude": -122.4194,
    "accuracy": 10.0
  }
}
EOF
)

CLEAR_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/api/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$CLEAR_JSON")

HTTP_CODE=$(echo "$CLEAR_RESPONSE" | tail -n1)
BODY=$(echo "$CLEAR_RESPONSE" | sed '$ d')

echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
  echo "✅ Clear alert successful"
  echo "$BODY" | jq '.'
elif [ "$HTTP_CODE" = "404" ]; then
  echo "❌ Clear alert failed with 404"
  echo "$BODY" | jq '.' || echo "$BODY"
  echo ""
  echo "Possible causes:"
  echo "- ValidateOrganizationAccess() is throwing UnauthorizedAccessException"
  echo "- Token organizationId ($ORG_ID) doesn't match alert's organizationId"
  echo "- Alert not found in database"
else
  echo "❌ Clear alert failed with status $HTTP_CODE"
  echo "$BODY" | jq '.' || echo "$BODY"
fi

echo ""
echo "=== Test Complete ==="
