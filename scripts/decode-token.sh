#!/bin/bash
# Quick script to decode a JWT token

if [ -z "$1" ]; then
  echo "Usage: $0 <jwt-token>"
  echo "Example: $0 eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ..."
  exit 1
fi

TOKEN="$1"

# Extract the payload (second part)
PAYLOAD=$(echo "$TOKEN" | cut -d'.' -f2)

# Add padding if needed
case $((${#PAYLOAD} % 4)) in
  2) PAYLOAD="${PAYLOAD}==" ;;
  3) PAYLOAD="${PAYLOAD}=" ;;
esac

# Decode and pretty print
echo "=== JWT Token Claims ==="
echo "$PAYLOAD" | base64 -d 2>/dev/null | jq '.'

# Check for organizationId
ORG_ID=$(echo "$PAYLOAD" | base64 -d 2>/dev/null | jq -r '.organizationId // empty')

echo ""
if [ -z "$ORG_ID" ]; then
  echo "❌ organizationId claim is MISSING"
else
  echo "✅ organizationId found: $ORG_ID"
fi
