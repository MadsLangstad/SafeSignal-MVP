# Two-Person All Clear - Quick Reference Card

## 🎯 What It Does
Ensures emergency alerts require verification from **2 different users** before being marked as resolved.

---

## 🔄 Workflow States

```
Alert Triggered → [New]
    ↓
User A clears → [PendingClearance] (1/2)
    ↓
User B clears → [Resolved] (2/2) ✅
```

---

## 📱 Mobile App Usage

### For Users Clearing Alerts

1. **View Alert History**
   - Open app → See alert list
   - Look for badges: **1/2** (needs 2nd clearance) or **2/2** (resolved)

2. **Submit Clearance**
   - Tap alert → Navigate to clearance screen
   - **Grant location permission** when prompted
   - Enter **notes** describing situation
   - Tap **Submit Clearance**
   - Receive confirmation message

### UI Indicators
| Status | Badge | Meaning |
|--------|-------|---------|
| New | None | No clearances yet |
| PendingClearance | 🟠 1/2 | Needs 2nd verification |
| Resolved | 🟢 2/2 | Fully cleared |

---

## 🔧 API Endpoints

### Submit Clearance
```bash
POST /api/alerts/{id}/clear

Headers:
  Authorization: Bearer {JWT_TOKEN}
  Content-Type: application/json

Body:
{
  "notes": "Room checked - false alarm",
  "location": {
    "latitude": 37.7749,
    "longitude": -122.4194,
    "accuracy": 5.0
  }
}
```

### Get Clearance History
```bash
GET /api/alerts/{id}/clearances

Headers:
  Authorization: Bearer {JWT_TOKEN}
```

---

## 🧪 Testing

### Quick E2E Test
```bash
# Set environment
export USER_A_TOKEN="eyJ..."
export USER_B_TOKEN="eyJ..."
export BUILDING_ID="uuid"

# Run test
./scripts/test-all-clear-workflow.sh
```

### Manual Test Steps
1. Trigger alert (ESP32 button or mobile)
2. User A submits clearance → Verify "1/2" badge appears
3. User B submits clearance → Verify "2/2" badge appears
4. Check database: `SELECT * FROM alert_clearances WHERE alert_id = 'uuid';`

---

## 🛡️ Security Rules

| Rule | Behavior |
|------|----------|
| Same user tries twice | ❌ 400 error: "Cannot provide second clearance" |
| Already resolved alert | ❌ 400 error: "Alert is already fully resolved" |
| Different organization | ❌ 404 error (prevents info leakage) |
| No location permission | ❌ Submission fails or disabled |

---

## 📊 Database Schema (Quick Ref)

### alert_clearances table
```sql
- id (UUID, PK)
- alert_id (UUID, FK → alerts)
- user_id (UUID, FK → users)
- clearance_step (1 or 2)
- cleared_at (timestamp)
- notes (text)
- location (JSONB)
- device_info (text)
```

### alerts table (new columns)
```sql
- first_clearance_user_id (UUID, FK → users)
- first_clearance_at (timestamp)
- second_clearance_user_id (UUID, FK → users)
- second_clearance_at (timestamp)
- fully_cleared_at (timestamp)
```

---

## 🚨 Troubleshooting

### "Location permission required" error
**Fix**: Go to device Settings → SafeSignal → Location → "While Using"

### Same user error
**Fix**: Use a different user account for 2nd clearance

### Alert not showing in app
**Fix**: Pull down to refresh alert list

### API 404 error
**Fix**: Verify alert belongs to your organization

### Network error during submission
**Fix**: Check internet connection and retry

---

## 📈 Performance Targets

| Metric | Target |
|--------|--------|
| Alert appears in app | < 30 seconds |
| Clearance submission | < 5 seconds |
| Full workflow (2 users) | < 5 minutes |
| Location accuracy | ≤ 100 meters (90%) |
| API response time | < 500ms (p95) |

---

## 🔍 Useful SQL Queries

### Check clearance status
```sql
SELECT
  a.alert_id,
  a.status,
  COUNT(ac.id) as clearance_count
FROM alerts a
LEFT JOIN alert_clearances ac ON a.id = ac.alert_id
WHERE a.id = 'YOUR_ALERT_ID'
GROUP BY a.id, a.alert_id, a.status;
```

### View clearance details
```sql
SELECT
  ac.clearance_step,
  u.email as user_email,
  ac.cleared_at,
  ac.notes,
  ac.location::json->>'latitude' as lat,
  ac.location::json->>'longitude' as lng
FROM alert_clearances ac
JOIN users u ON ac.user_id = u.id
WHERE ac.alert_id = 'YOUR_ALERT_ID'
ORDER BY ac.clearance_step;
```

### Find pending clearances
```sql
SELECT
  a.alert_id,
  a.triggered_at,
  u.email as first_user
FROM alerts a
JOIN users u ON a.first_clearance_user_id = u.id
WHERE a.status = 'PendingClearance'
ORDER BY a.triggered_at DESC;
```

---

## 📞 Support

- **QA Test Plan**: `claudedocs/all-clear-qa-test-plan.md`
- **Implementation Summary**: `claudedocs/all-clear-implementation-summary.md`
- **Automated Tests**: `scripts/test-all-clear-workflow.sh`

---

## ✅ Pre-Deployment Checklist

### Backend
- [ ] Database migration applied
- [ ] API endpoints tested
- [ ] AlertClearanceRepository registered in DI
- [ ] Audit logging verified

### Mobile
- [ ] expo-location dependency installed
- [ ] Location permissions configured (iOS: Info.plist, Android: AndroidManifest.xml)
- [ ] Clearance screen tested on iOS and Android
- [ ] API integration validated

### Monitoring
- [ ] Grafana dashboard for clearance metrics
- [ ] Prometheus alerts for failure rate
- [ ] Sentry error tracking for mobile

### Documentation
- [ ] API spec updated
- [ ] User guide created
- [ ] Training materials prepared
- [ ] Release notes drafted

---

**Last Updated**: 2025-11-04
**Version**: 1.0
**Status**: Ready for E2E Testing
