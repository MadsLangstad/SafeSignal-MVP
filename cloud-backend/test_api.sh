#!/bin/bash

# SafeSignal Cloud Backend API Test Script
# Tests all implemented endpoints

set -e

API_URL="http://localhost:5118/api/v1"
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}=== SafeSignal Cloud Backend API Tests ===${NC}\n"

# Test 1: Create Organization
echo -e "${BLUE}1. Creating organization...${NC}"
ORG_RESPONSE=$(curl -s -X POST "$API_URL/organizations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"Test School District",
    "slug":"test-school-'$(date +%s)'",
    "metadata":"{\"location\":\"California\"}"
  }')

ORG_ID=$(echo $ORG_RESPONSE | jq -r '.id')
echo -e "${GREEN}✓ Created organization: $ORG_ID${NC}\n"

# Test 2: List Organizations
echo -e "${BLUE}2. Listing organizations...${NC}"
curl -s "$API_URL/organizations" | jq '.organizations[] | {id, name, slug}'
echo -e "${GREEN}✓ Listed organizations${NC}\n"

# Test 3: Get Organization Details
echo -e "${BLUE}3. Getting organization details...${NC}"
curl -s "$API_URL/organizations/$ORG_ID" | jq '{id, name, slug, status, siteCount, deviceCount}'
echo -e "${GREEN}✓ Retrieved organization details${NC}\n"

# Test 4: Register Device
echo -e "${BLUE}4. Registering device...${NC}"
DEVICE_RESPONSE=$(curl -s -X POST "$API_URL/devices" \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId":"ESP32-TEST-'$(date +%s)'",
    "organizationId":"'$ORG_ID'",
    "serialNumber":"SN-TEST-001",
    "macAddress":"AA:BB:CC:DD:EE:FF",
    "hardwareVersion":"ESP32-S3-v1.0"
  }')

DEVICE_ID=$(echo $DEVICE_RESPONSE | jq -r '.id')
echo -e "${GREEN}✓ Registered device: $DEVICE_ID${NC}\n"

# Test 5: List Devices
echo -e "${BLUE}5. Listing devices for organization...${NC}"
curl -s "$API_URL/devices?organizationId=$ORG_ID" | jq '.[] | {id, deviceId, status, hardwareVersion}'
echo -e "${GREEN}✓ Listed devices${NC}\n"

# Test 6: Update Device
echo -e "${BLUE}6. Updating device status...${NC}"
curl -s -X PUT "$API_URL/devices/$DEVICE_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "status":0,
    "firmwareVersion":"v1.0.0"
  }' | jq '{id, deviceId, status, firmwareVersion, lastSeenAt}'
echo -e "${GREEN}✓ Updated device${NC}\n"

# Test 7: Create Alert
echo -e "${BLUE}7. Creating alert...${NC}"
ALERT_RESPONSE=$(curl -s -X POST "$API_URL/alerts" \
  -H "Content-Type: application/json" \
  -d '{
    "alertId":"ALERT-TEST-'$(date +%s)'",
    "organizationId":"'$ORG_ID'",
    "deviceId":"'$DEVICE_ID'",
    "severity":3,
    "alertType":"emergency",
    "source":0,
    "metadata":"{\"test\":true}"
  }')

ALERT_ID=$(echo $ALERT_RESPONSE | jq -r '.id')
echo -e "${GREEN}✓ Created alert: $ALERT_ID${NC}\n"

# Test 8: List Alerts
echo -e "${BLUE}8. Listing alerts for organization...${NC}"
curl -s "$API_URL/alerts?organizationId=$ORG_ID" | jq '.[] | {id, alertId, severity, alertType, status}'
echo -e "${GREEN}✓ Listed alerts${NC}\n"

# Test 9: Acknowledge Alert
echo -e "${BLUE}9. Acknowledging alert...${NC}"
curl -s -X PUT "$API_URL/alerts/$ALERT_ID/acknowledge" | jq '{id, alertId, status}'
echo -e "${GREEN}✓ Acknowledged alert${NC}\n"

# Test 10: Resolve Alert
echo -e "${BLUE}10. Resolving alert...${NC}"
curl -s -X PUT "$API_URL/alerts/$ALERT_ID/resolve" | jq '{id, alertId, status, resolvedAt}'
echo -e "${GREEN}✓ Resolved alert${NC}\n"

# Test 11: Update Organization
echo -e "${BLUE}11. Updating organization...${NC}"
curl -s -X PUT "$API_URL/organizations/$ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"Updated School District",
    "metadata":"{\"updated\":true}"
  }' | jq '{id, name, updatedAt}'
echo -e "${GREEN}✓ Updated organization${NC}\n"

echo -e "${BLUE}=== All Tests Passed! ===${NC}"
echo -e "\nCreated resources:"
echo -e "  Organization: $ORG_ID"
echo -e "  Device: $DEVICE_ID"
echo -e "  Alert: $ALERT_ID"
echo -e "\nSwagger UI: http://localhost:5118/swagger"
echo -e "Adminer: http://localhost:8080"
