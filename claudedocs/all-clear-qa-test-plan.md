# All Clear Two-Person Workflow - QA Test Plan

**Feature**: Two-person safety clearance verification for emergency alerts
**Status**: Implementation complete - ready for E2E testing
**Date**: 2025-11-04

## Overview

This document provides a comprehensive test plan for validating the two-person All Clear workflow across firmware, edge, cloud-backend, and mobile app components.

## Test Environment Setup

### Prerequisites
- [ ] Edge services running (EMQX, policy-service with MQTT/rate-limiting)
- [ ] Cloud backend API running on port 5118
- [ ] PostgreSQL database with latest schema (AlertClearance table)
- [ ] Mobile app built with latest code (AlertClearanceScreen, expo-location)
- [ ] At least 2 test user accounts in different organizations
- [ ] Physical ESP32 device OR simulator for alert triggering

### Configuration Verification
```bash
# 1. Check backend is running
curl http://localhost:5118/health

# 2. Verify database schema
psql safesignal_dev -c "\d alert_clearances"

# 3. Check mobile dependencies
cd mobile && npm ls expo-location

# 4. Verify edge policy-service
docker logs policy-service | grep "MQTT Handler Service"
```

---

## Test Scenarios

### Scenario 1: Happy Path - Two Different Users
**SLO Target**: 95% of alerts cleared within 5 minutes with dual verification

#### Steps:
1. **Trigger Alert**
   - User A presses ESP32 button OR uses mobile trigger
   - Verify alert appears in mobile app history for both users
   - **Expected**: Alert status = "New", no clearances shown

2. **First Clearance (User A)**
   - User A navigates to AlertClearanceScreen
   - Grant location permissions when prompted
   - System captures GPS coordinates automatically
   - User A enters notes: "Checked room 101 - false alarm, student prank"
   - User A submits clearance
   - **Expected**:
     - Alert status changes to "PendingClearance"
     - FirstClearanceUserId = User A
     - FirstClearanceAt = current timestamp
     - Alert history shows "1/2" badge
     - API response: "First clearance recorded. Awaiting second verification."

3. **Second Clearance (User B)**
   - User B navigates to AlertClearanceScreen for same alert
   - Grant location permissions
   - User B enters notes: "Double-checked room 101 - confirmed safe"
   - User B submits clearance
   - **Expected**:
     - Alert status changes to "Resolved"
     - SecondClearanceUserId = User B
     - SecondClearanceAt = current timestamp
     - FullyClearedAt = current timestamp
     - Alert history shows "2/2" badge
     - API response: "Second clearance recorded. Alert fully resolved."

4. **Verify Audit Trail**
   ```bash
   # Query audit logs
   psql safesignal_dev -c "SELECT * FROM audit_logs WHERE entity_type = 'Alert' ORDER BY created_at DESC LIMIT 5;"

   # Query clearances
   psql safesignal_dev -c "SELECT * FROM alert_clearances WHERE alert_id = 'YOUR_ALERT_ID' ORDER BY clearance_step;"
   ```
   - **Expected**:
     - 2 audit log entries (one per clearance step)
     - 2 clearance records with step 1 and step 2
     - Location JSON populated with lat/lng/accuracy
     - Notes captured for both clearances

#### Performance Validation:
- [ ] Alert visible in mobile app within 3 seconds of trigger
- [ ] First clearance submitted and confirmed < 30 seconds
- [ ] Second clearance submitted and confirmed < 30 seconds
- [ ] Total workflow completion < 5 minutes (SLO compliance)

---

### Scenario 2: Same User Prevention
**Purpose**: Verify system prevents single user from clearing twice

#### Steps:
1. Trigger alert (any user)
2. User A submits first clearance
3. User A attempts second clearance on same alert
4. **Expected**:
   - API returns 400 Bad Request
   - Error message: "Cannot provide second clearance - you already provided the first clearance"
   - Alert remains in "PendingClearance" status
   - No new clearance record created

---

### Scenario 3: Location Capture Validation
**Purpose**: Verify GPS coordinates are captured accurately

#### Steps:
1. Trigger alert
2. User A submits clearance from known location (e.g., office at lat: 37.7749, lng: -122.4194)
3. Query clearance location data:
   ```sql
   SELECT location FROM alert_clearances WHERE alert_id = 'YOUR_ALERT_ID';
   ```
4. **Expected**:
   - Location JSON contains latitude, longitude, accuracy, altitude (optional)
   - Coordinates are within reasonable range of actual location (±100m with good GPS)
   - Timestamp reflects when location was captured

---

### Scenario 4: Permission Denied Handling
**Purpose**: Verify graceful handling of denied location permissions

#### Steps:
1. Trigger alert
2. User navigates to AlertClearanceScreen
3. Deny location permission when prompted
4. **Expected**:
   - App shows error: "Location permission is required to clear alerts"
   - Submission button disabled OR submission fails gracefully
   - User can retry by granting permissions

---

### Scenario 5: Already Resolved Alert
**Purpose**: Verify system prevents clearing already-resolved alerts

#### Steps:
1. Trigger alert
2. User A submits first clearance
3. User B submits second clearance (alert now Resolved)
4. User C attempts to clear same alert
5. **Expected**:
   - API returns 400 Bad Request
   - Error message: "Alert is already fully resolved"
   - No new clearance record created

---

### Scenario 6: Clearance History Retrieval
**Purpose**: Verify GET /alerts/{id}/clearances endpoint

#### Steps:
1. Trigger and fully clear an alert (2 clearances)
2. Call GET /api/alerts/{alertId}/clearances
3. **Expected Response**:
   ```json
   {
     "alertId": "uuid",
     "status": "Resolved",
     "clearances": [
       {
         "id": "uuid",
         "clearanceStep": 1,
         "userId": "uuid",
         "userName": "User A",
         "userEmail": "usera@example.com",
         "clearedAt": "2025-11-04T10:30:00Z",
         "notes": "Checked room 101...",
         "location": {
           "latitude": 37.7749,
           "longitude": -122.4194,
           "accuracy": 5.0
         },
         "deviceInfo": "Expo/1.0..."
       },
       {
         "id": "uuid",
         "clearanceStep": 2,
         "userId": "uuid",
         "userName": "User B",
         "userEmail": "userb@example.com",
         "clearedAt": "2025-11-04T10:32:00Z",
         "notes": "Double-checked...",
         "location": {...},
         "deviceInfo": "..."
       }
     ]
   }
   ```

---

### Scenario 7: Cross-Organization Isolation
**Purpose**: Verify users cannot clear alerts from other organizations

#### Steps:
1. Org A user triggers alert
2. Org B user attempts to clear Org A's alert
3. **Expected**:
   - API returns 404 Not Found (to prevent information leakage)
   - No clearance record created
   - Audit log records unauthorized access attempt

---

### Scenario 8: Network Failure Handling
**Purpose**: Verify mobile app handles network errors gracefully

#### Steps:
1. Trigger alert
2. User navigates to AlertClearanceScreen
3. Disable network connectivity (airplane mode)
4. Attempt clearance submission
5. **Expected**:
   - App shows network error message
   - User can retry submission
6. Re-enable network and retry
7. **Expected**:
   - Submission succeeds
   - No duplicate clearance records created

---

### Scenario 9: Concurrent Clearance Submissions
**Purpose**: Verify system handles race conditions properly

#### Steps:
1. Trigger alert
2. User A and User B simultaneously submit first clearance (within 1 second)
3. **Expected**:
   - Only ONE clearance is recorded as step 1
   - The other submission either:
     - Records as step 2 (if alert already moved to PendingClearance), OR
     - Fails with appropriate error
   - Alert reaches Resolved state with exactly 2 clearances

---

### Scenario 10: Location Permission Re-prompt
**Purpose**: Verify behavior when user previously denied then grants permission

#### Steps:
1. User denies location permission initially
2. User goes to device settings and grants permission
3. User returns to app and attempts clearance
4. **Expected**:
   - App detects new permission status
   - Location is captured successfully
   - Clearance submits with location data

---

## SLO Validation Tests

### SLO 1: Alert Acknowledgment Time
**Target**: 95% of alerts acknowledged within 30 seconds

#### Test:
1. Trigger 20 alerts across different times of day
2. Measure time from alert trigger to mobile app display
3. **Success Criteria**: ≥19 alerts (95%) appear within 30 seconds

### SLO 2: Clearance Workflow Completion
**Target**: 95% of alerts fully cleared within 5 minutes

#### Test:
1. Trigger 20 alerts
2. Complete two-person clearance workflow for each
3. Measure time from trigger to second clearance completion
4. **Success Criteria**: ≥19 alerts (95%) fully cleared within 5 minutes

### SLO 3: Location Accuracy
**Target**: 90% of clearances have location accuracy ≤100m

#### Test:
1. Submit 20 clearances from known locations
2. Compare reported coordinates against actual location
3. **Success Criteria**: ≥18 clearances (90%) within 100m accuracy

---

## Automated Test Scripts

### Script 1: End-to-End Happy Path
```bash
#!/bin/bash
# e2e-all-clear-test.sh

# Prerequisites: jq, curl, psql

API_URL="http://localhost:5118/api"
USER_A_TOKEN="eyJ..." # JWT for User A
USER_B_TOKEN="eyJ..." # JWT for User B
BUILDING_ID="uuid"

echo "=== Triggering Alert ==="
ALERT_RESPONSE=$(curl -s -X POST "$API_URL/alerts/trigger" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"buildingId\": \"$BUILDING_ID\", \"mode\": \"emergency\"}")

ALERT_ID=$(echo $ALERT_RESPONSE | jq -r '.id')
echo "Alert created: $ALERT_ID"

sleep 2

echo "=== User A First Clearance ==="
CLEAR1_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notes": "Test clearance 1",
    "location": {"latitude": 37.7749, "longitude": -122.4194, "accuracy": 5.0}
  }')

echo "Clearance 1: $(echo $CLEAR1_RESPONSE | jq -r '.message')"
echo "Status: $(echo $CLEAR1_RESPONSE | jq -r '.status')"

sleep 2

echo "=== User B Second Clearance ==="
CLEAR2_RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_B_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notes": "Test clearance 2",
    "location": {"latitude": 37.7750, "longitude": -122.4195, "accuracy": 8.0}
  }')

echo "Clearance 2: $(echo $CLEAR2_RESPONSE | jq -r '.message')"
echo "Status: $(echo $CLEAR2_RESPONSE | jq -r '.status')"

echo "=== Fetching Clearance History ==="
HISTORY=$(curl -s -X GET "$API_URL/alerts/$ALERT_ID/clearances" \
  -H "Authorization: Bearer $USER_A_TOKEN")

echo "Clearance count: $(echo $HISTORY | jq '.clearances | length')"
echo $HISTORY | jq '.'

echo "=== Test Complete ==="
```

### Script 2: Same User Prevention Test
```bash
#!/bin/bash
# test-same-user-prevention.sh

API_URL="http://localhost:5118/api"
USER_A_TOKEN="eyJ..."
BUILDING_ID="uuid"

# Trigger alert
ALERT_ID=$(curl -s -X POST "$API_URL/alerts/trigger" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"buildingId\": \"$BUILDING_ID\"}" | jq -r '.id')

# First clearance
curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes": "First"}' > /dev/null

# Attempt second clearance with same user
RESPONSE=$(curl -s -X POST "$API_URL/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes": "Second"}')

# Check for expected error
if echo $RESPONSE | jq -e '.error | contains("already provided")' > /dev/null; then
  echo "✅ PASS: Same user correctly prevented from second clearance"
else
  echo "❌ FAIL: Same user was not prevented"
  echo $RESPONSE | jq '.'
fi
```

---

## Mobile UI Validation Checklist

### AlertClearanceScreen.tsx
- [ ] Screen displays alert details (ID, trigger time, building)
- [ ] Location permission prompt appears on mount
- [ ] Location coordinates display after permission granted
- [ ] Notes text input is functional (multiline)
- [ ] Submit button is disabled until notes entered
- [ ] Loading state shows during submission
- [ ] Success message appears after submission
- [ ] Error messages display for failures (network, permissions, same-user)
- [ ] Screen navigates back to history after successful clearance

### AlertHistoryScreen.tsx
- [ ] "New" alerts show no badge
- [ ] "PendingClearance" alerts show "1/2" badge in orange
- [ ] "Resolved" alerts show "2/2" badge in green
- [ ] Tapping alert navigates to clearance screen
- [ ] Pull-to-refresh updates alert list
- [ ] Empty state displays when no alerts exist
- [ ] Loading spinner shows during data fetch

---

## Database Verification Queries

### Check Clearance Records
```sql
SELECT
  ac.clearance_step,
  u.email as user_email,
  ac.cleared_at,
  ac.notes,
  ac.location::json->>'latitude' as latitude,
  ac.location::json->>'longitude' as longitude,
  a.status as alert_status
FROM alert_clearances ac
JOIN users u ON ac.user_id = u.id
JOIN alerts a ON ac.alert_id = a.id
WHERE ac.alert_id = 'YOUR_ALERT_ID'
ORDER BY ac.clearance_step;
```

### Check Alert State Transitions
```sql
SELECT
  alert_id,
  status,
  first_clearance_user_id,
  first_clearance_at,
  second_clearance_user_id,
  second_clearance_at,
  fully_cleared_at,
  resolved_at
FROM alerts
WHERE id = 'YOUR_ALERT_ID';
```

### Verify Audit Trail
```sql
SELECT
  action,
  success,
  additional_info::json->>'ClearanceStep' as step,
  additional_info::json->>'Status' as status,
  created_at
FROM audit_logs
WHERE entity_type = 'Alert'
  AND entity_id = 'YOUR_ALERT_ID'
ORDER BY created_at;
```

---

## Performance Benchmarks

### API Response Times (Target: p95 < 500ms)
```bash
# Benchmark clearance submission
ab -n 100 -c 10 -T 'application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -p clearance.json \
  "http://localhost:5118/api/alerts/$ALERT_ID/clear"
```

### Database Query Performance
```sql
-- Should use index on alert_id
EXPLAIN ANALYZE
SELECT * FROM alert_clearances WHERE alert_id = 'uuid';

-- Should use index on user_id
EXPLAIN ANALYZE
SELECT * FROM alert_clearances WHERE user_id = 'uuid';
```

---

## Security Validation

### Authorization Checks
- [ ] Unauthenticated requests return 401
- [ ] Cross-organization access returns 404 (not 403 to prevent info leakage)
- [ ] JWT token expiration is enforced
- [ ] Clearance endpoints validate organization access

### Input Validation
- [ ] Notes field sanitized for XSS (backend)
- [ ] Location coordinates validated for reasonable ranges (lat: -90 to 90, lng: -180 to 180)
- [ ] DeviceInfo captured from User-Agent header
- [ ] SQL injection prevented (parameterized queries)

### Audit Logging
- [ ] All clearance submissions logged to audit_logs table
- [ ] Failed attempts logged with reason
- [ ] Logs include user_id, organization_id, timestamp
- [ ] Sensitive data (notes) stored in additional_info JSON

---

## Rollback Plan

If critical issues are discovered:

1. **Backend Rollback**:
   ```bash
   git revert <commit-hash-for-clearance-endpoints>
   cd cloud-backend && dotnet build && dotnet run --project src/Api
   ```

2. **Database Rollback**:
   ```sql
   -- Remove clearance table and alert columns
   DROP TABLE IF EXISTS alert_clearances;
   ALTER TABLE alerts DROP COLUMN IF EXISTS first_clearance_user_id;
   ALTER TABLE alerts DROP COLUMN IF EXISTS first_clearance_at;
   ALTER TABLE alerts DROP COLUMN IF EXISTS second_clearance_user_id;
   ALTER TABLE alerts DROP COLUMN IF EXISTS second_clearance_at;
   ALTER TABLE alerts DROP COLUMN IF EXISTS fully_cleared_at;
   ```

3. **Mobile Rollback**:
   ```bash
   git revert <commit-hash-for-clearance-screen>
   cd mobile && npm install && npm run build
   ```

---

## Sign-Off Checklist

- [ ] All happy path scenarios pass
- [ ] Same-user prevention validated
- [ ] Location capture working on iOS and Android
- [ ] Database schema deployed to all environments
- [ ] API endpoints tested with Postman/curl
- [ ] Mobile UI reviewed by product team
- [ ] SLO metrics defined in monitoring dashboard
- [ ] Security review completed
- [ ] Documentation updated (API spec, user guide)
- [ ] Rollback plan tested in staging environment

---

## Next Steps After QA

1. **Monitoring Setup**:
   - Configure Prometheus alerts for clearance failure rate
   - Add Grafana dashboard for clearance workflow metrics
   - Set up Sentry error tracking for mobile clearance errors

2. **User Training**:
   - Create video walkthrough of two-person clearance workflow
   - Update user manual with screenshots
   - Conduct training session with pilot users

3. **Performance Optimization**:
   - Add database indexes if clearance queries are slow
   - Consider caching alert status in Redis for high-traffic orgs
   - Optimize mobile app location fetching (background vs foreground)

4. **Future Enhancements**:
   - Add push notifications for pending clearances
   - Implement clearance reminders (e.g., "Alert X has been pending for 10 minutes")
   - Add photo attachment capability to clearances
   - Support offline clearance submission with sync when online
