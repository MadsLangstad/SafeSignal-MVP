#!/bin/bash
# Test clearing a NEW alert

API_URL="http://localhost:5118"
ALERT_ID="d96d7f84-f1c1-4c72-afc8-722461a0dd35"

# Login
cat > /tmp/login.json <<'EOF'
{
  "email": "admin@safesignal.com",
  "password": "Admin@12345678!"
}
EOF

TOKEN=$(curl -s $API_URL/api/auth/login -H 'Content-Type: application/json' -d @/tmp/login.json | jq -r '.tokens.accessToken')

if [ "$TOKEN" = "null" ] || [ -z "$TOKEN" ]; then
  echo "Failed to get token"
  exit 1
fi

echo "✅ Got token: ${TOKEN:0:50}..."
echo ""

# Test clearing the alert
echo "Testing POST /api/alerts/$ALERT_ID/clear..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/api/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes":"Test from script","location":{"latitude":37.7749,"longitude":-122.4194,"accuracy":10.0}}')

HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$ d')

echo "HTTP Status: $HTTP_CODE"
echo "$BODY" | jq '.'

if [ "$HTTP_CODE" = "200" ]; then
  echo ""
  echo "✅ SUCCESS - Alert cleared"
elif [ "$HTTP_CODE" = "400" ]; then
  echo ""
  echo "⚠️  400 Bad Request - Check error message above"
else
  echo ""
  echo "❌ Unexpected status: $HTTP_CODE"
fi
