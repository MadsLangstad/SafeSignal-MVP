#!/bin/bash
# Script to verify JWT token contains organizationId claim

set -e

API_URL="${API_URL:-http://localhost:5118}"

echo "=== JWT Token Verification Script ==="
echo "API URL: $API_URL"
echo ""

# Login and get token
echo "1. Logging in as admin@safesignal.com..."
LOGIN_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@safesignal.com",
    "password": "Admin@12345678!"
  }')

# Check for errors
if echo "$LOGIN_RESPONSE" | grep -q '"error"'; then
  echo "❌ Login failed:"
  echo "$LOGIN_RESPONSE" | jq '.'
  exit 1
fi

# Extract access token
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.tokens.accessToken')

if [ -z "$ACCESS_TOKEN" ] || [ "$ACCESS_TOKEN" = "null" ]; then
  echo "❌ Failed to extract access token"
  echo "$LOGIN_RESPONSE" | jq '.'
  exit 1
fi

echo "✅ Login successful"
echo ""

# Decode JWT token (extract payload)
echo "2. Decoding JWT token..."
echo ""

# JWT structure: header.payload.signature
# Extract the payload (second part)
PAYLOAD=$(echo "$ACCESS_TOKEN" | cut -d'.' -f2)

# Add padding if needed (base64 requires length divisible by 4)
case $((${#PAYLOAD} % 4)) in
  2) PAYLOAD="${PAYLOAD}==" ;;
  3) PAYLOAD="${PAYLOAD}=" ;;
esac

# Decode and pretty print
DECODED=$(echo "$PAYLOAD" | base64 -d 2>/dev/null | jq '.')

echo "=== JWT Token Claims ==="
echo "$DECODED"
echo ""

# Check for organizationId claim
ORG_ID=$(echo "$DECODED" | jq -r '.organizationId // empty')

if [ -z "$ORG_ID" ]; then
  echo "❌ FAIL: organizationId claim is MISSING from JWT token"
  echo ""
  echo "Available claims:"
  echo "$DECODED" | jq 'keys'
  exit 1
else
  echo "✅ SUCCESS: organizationId claim found: $ORG_ID"
fi

# Check for userId claim
USER_ID=$(echo "$DECODED" | jq -r '.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] // .sub // empty')

if [ -z "$USER_ID" ]; then
  echo "⚠️  WARNING: userId claim not found"
else
  echo "✅ SUCCESS: userId claim found: $USER_ID"
fi

# Check for email claim
EMAIL=$(echo "$DECODED" | jq -r '.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] // .email // empty')

if [ -z "$EMAIL" ]; then
  echo "⚠️  WARNING: email claim not found"
else
  echo "✅ SUCCESS: email claim found: $EMAIL"
fi

# Check for role claims
ROLES=$(echo "$DECODED" | jq -r '.["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] // empty')

if [ -z "$ROLES" ]; then
  echo "⚠️  WARNING: role claims not found"
else
  echo "✅ SUCCESS: role claims found: $ROLES"
fi

echo ""
echo "=== Verification Complete ==="
echo ""
echo "Next steps for mobile app:"
echo "1. Ensure mobile app is using the latest token (re-login)"
echo "2. Check mobile app is sending Authorization header: 'Bearer $ACCESS_TOKEN'"
echo "3. Verify mobile app API calls are going to: $API_URL"
