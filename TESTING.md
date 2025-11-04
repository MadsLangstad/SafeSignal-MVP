# All Clear Two-Person Workflow - Testing Guide

## Quick Start

Since we don't have known test user passwords, here's how to test the system manually:

### Option 1: Register New Test Users via API

```bash
# 1. Get an organization ID
ORG_ID=$(docker exec safesignal-postgres psql -U postgres -d safesignal -t -c 'SELECT "Id" FROM organizations LIMIT 1;' | xargs)
echo "Organization ID: $ORG_ID"

# 2. Register User A (Alice)
curl -X POST http://localhost:5118/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"alice@testclear.com\",
    \"password\": \"TestPassword123!\",
    \"firstName\": \"Alice\",
    \"lastName\": \"Clearance\",
    \"organizationId\": \"$ORG_ID\"
  }" | jq '.'

# 3. Register User B (Bob)
curl -X POST http://localhost:5118/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"bob@testclear.com\",
    \"password\": \"TestPassword123!\",
    \"firstName\": \"Bob\",
    \"lastName\": \"Clearance\",
    \"organizationId\": \"$ORG_ID\"
  }" | jq '.'
```

### Option 2: Direct Database Insert (Advanced)

```bash
# Generate bcrypt hash for password "TestPassword123!"
# You'll need a bcrypt tool or use Python:
python3 -c "import bcrypt; print(bcrypt.hashpw(b'TestPassword123!', bcrypt.gensalt()).decode())"

# Then insert into database:
docker exec safesignal-postgres psql -U postgres -d safesignal -c "
INSERT INTO users (\"Id\", \"Email\", \"PasswordHash\", \"FirstName\", \"LastName\", \"Status\")
VALUES
  (gen_random_uuid(), 'alice@testclear.com', '\$2b\$12\$YOUR_HASH_HERE', 'Alice', 'Clearance', 'Active'),
  (gen_random_uuid(), 'bob@testclear.com', '\$2b\$12\$YOUR_HASH_HERE', 'Bob', 'Clearance', 'Active');
"
```

### Step-by-Step E2E Test

Once you have two test users created:

#### 1. Get JWT Tokens

```bash
# Login as Alice
USER_A_TOKEN=$(curl -s -X POST http://localhost:5118/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@testclear.com","password":"TestPassword123!"}' | jq -r '.token')

echo "Alice token: ${USER_A_TOKEN:0:40}..."

# Login as Bob
USER_B_TOKEN=$(curl -s -X POST http://localhost:5118/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"bob@testclear.com","password":"TestPassword123!"}' | jq -r '.token')

echo "Bob token: ${USER_B_TOKEN:0:40}..."
```

#### 2. Get Building ID

```bash
BUILDING_ID=$(docker exec safesignal-postgres psql -U postgres -d safesignal -t -c 'SELECT "Id" FROM buildings LIMIT 1;' | xargs)
echo "Building ID: $BUILDING_ID"
```

#### 3. Trigger an Alert

```bash
echo "Triggering alert..."
ALERT_RESPONSE=$(curl -s -X POST "http://localhost:5118/api/alerts/trigger" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"buildingId\": \"$BUILDING_ID\", \"mode\": \"emergency\"}")

ALERT_ID=$(echo "$ALERT_RESPONSE" | jq -r '.id')
ALERT_STATUS=$(echo "$ALERT_RESPONSE" | jq -r '.status')

echo "✅ Alert created: $ALERT_ID (Status: $ALERT_STATUS)"
```

#### 4. First Clearance (Alice)

```bash
echo "Alice submitting first clearance..."
CLEAR1_RESPONSE=$(curl -s -X POST "http://localhost:5118/api/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notes": "Checked room 101 - false alarm, student prank",
    "location": {
      "latitude": 37.7749,
      "longitude": -122.4194,
      "accuracy": 5.0,
      "altitude": 10.5
    }
  }')

echo "$CLEAR1_RESPONSE" | jq '.'

# Expected output:
# {
#   "alertId": "uuid",
#   "status": "PendingClearance",
#   "message": "First clearance recorded. Awaiting second verification.",
#   "clearanceStep": 1,
#   ...
# }
```

#### 5. Second Clearance (Bob)

```bash
echo "Bob submitting second clearance..."
CLEAR2_RESPONSE=$(curl -s -X POST "http://localhost:5118/api/alerts/$ALERT_ID/clear" \
  -H "Authorization: Bearer $USER_B_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "notes": "Double-checked room 101 - confirmed all clear",
    "location": {
      "latitude": 37.7750,
      "longitude": -122.4195,
      "accuracy": 8.0
    }
  }')

echo "$CLEAR2_RESPONSE" | jq '.'

# Expected output:
# {
#   "alertId": "uuid",
#   "status": "Resolved",
#   "message": "Second clearance recorded. Alert fully resolved.",
#   "clearanceStep": 2,
#   ...
# }
```

#### 6. Verify Clearance History

```bash
echo "Fetching clearance history..."
HISTORY=$(curl -s -X GET "http://localhost:5118/api/alerts/$ALERT_ID/clearances" \
  -H "Authorization: Bearer $USER_A_TOKEN")

echo "$HISTORY" | jq '.'

# Expected: 2 clearance records
```

#### 7. Test Same-User Prevention

```bash
echo "Testing same-user prevention..."

# Trigger new alert
ALERT2=$(curl -s -X POST "http://localhost:5118/api/alerts/trigger" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"buildingId\": \"$BUILDING_ID\"}")

ALERT_ID_2=$(echo "$ALERT2" | jq -r '.id')

# Alice clears first time
curl -s -X POST "http://localhost:5118/api/alerts/$ALERT_ID_2/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes":"First clearance"}' > /dev/null

# Alice tries to clear second time (should fail)
SAME_USER_TEST=$(curl -s -X POST "http://localhost:5118/api/alerts/$ALERT_ID_2/clear" \
  -H "Authorization: Bearer $USER_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"notes":"Second clearance - same user"}')

echo "$SAME_USER_TEST" | jq '.'

# Expected:
# {
#   "error": "Cannot provide second clearance - you already provided the first clearance"
# }
```

### Database Verification

```bash
# Check alert status
docker exec safesignal-postgres psql -U postgres -d safesignal -c "
SELECT
  \"AlertId\",
  \"Status\",
  \"FirstClearanceAt\",
  \"SecondClearanceAt\",
  \"FullyClearedAt\"
FROM alert_history
WHERE \"Id\" = '$ALERT_ID';
"

# Check clearance records
docker exec safesignal-postgres psql -U postgres -d safesignal -c "
SELECT
  \"ClearanceStep\",
  u.\"Email\" as user_email,
  \"ClearedAt\",
  \"Notes\",
  \"Location\"
FROM alert_clearances ac
JOIN users u ON ac.\"UserId\" = u.\"Id\"
WHERE ac.\"AlertId\" = '$ALERT_ID'
ORDER BY \"ClearanceStep\";
"

# Check audit logs
docker exec safesignal-postgres psql -U postgres -d safesignal -c "
SELECT
  \"Action\",
  \"Success\",
  \"AdditionalInfo\",
  \"CreatedAt\"
FROM audit_logs
WHERE \"EntityType\" = 'Alert'
  AND \"EntityId\" = '$ALERT_ID'
ORDER BY \"CreatedAt\";
"
```

---

## Automated Test Script

Once you have test users set up, you can use the automated script:

```bash
# Export tokens and building ID
export USER_A_TOKEN="<alice-jwt-token>"
export USER_B_TOKEN="<bob-jwt-token>"
export BUILDING_ID="<building-uuid>"

# Run automated tests
./scripts/test-all-clear-workflow.sh
```

---

## Mobile App Testing

### iOS/Android Setup

1. Build and run mobile app:
   ```bash
   cd mobile
   npm install
   npm run ios  # or npm run android
   ```

2. Login as Alice (User A)

3. Trigger an alert (via ESP32 button or API)

4. In mobile app:
   - View alert history
   - Tap on alert
   - Grant location permissions
   - Enter notes
   - Submit clearance
   - Verify "1/2" badge appears

5. Login as Bob (User B) on second device

6. In mobile app:
   - View alert history
   - Tap on same alert (shows "1/2" badge)
   - Grant location permissions
   - Enter notes
   - Submit clearance
   - Verify "2/2" badge appears

---

## Troubleshooting

### Can't login with existing users
The existing users in the database might have different passwords. Either:
1. Use the register endpoint to create new test users
2. Contact the team for test account credentials

### Registration fails
Check:
- Password is at least 12 characters
- Organization ID exists in database
- Email is not already registered

### Alert not appearing in mobile app
- Check backend API is running
- Verify user's organization matches alert's organization
- Pull down to refresh alert list

### Location permissions denied
- Go to device Settings → SafeSignal → Location → "While Using"
- Restart app and try again

---

## Success Criteria

- ✅ Two different users can clear an alert (step 1 and step 2)
- ✅ Same user cannot clear twice
- ✅ Alert status progresses: New → PendingClearance → Resolved
- ✅ GPS coordinates captured for both clearances
- ✅ Audit logs created for both clearance steps
- ✅ Mobile app shows 1/2 and 2/2 badges correctly
- ✅ Clearance history endpoint returns both clearances

---

## Next Steps

After successful testing:
1. Configure Grafana dashboard for clearance metrics
2. Set up Prometheus alerts for clearance failure rate
3. Add Sentry error tracking for mobile clearance errors
4. Conduct user training session
5. Deploy to staging environment
6. Gradual production rollout (10% → 50% → 100%)
