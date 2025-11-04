# Documentation Update: api-documentation.md

**Target File**: `../safeSignal-doc/documentation/development/api-documentation.md`
**Purpose**: Clarify MVP endpoints vs production roadmap features (especially All Clear workflow)

---

## Updates Required

### 1. Alert Clear/Resolve Endpoint (Line 55-67)

**Current Text**:
```markdown
Terminates an active alert. Requires MFA + two-person rule.

POST /api/v1/alerts/{alertId}/clear
Authorization: Bearer <JWT>

{
  "userId": "uuid",
  "secondApproverId": "uuid",
  "reason": "Drill completed"
}
```

**Replace With**:
```markdown
### Alert Resolution

**MVP Implementation (v1.0 - Current)**:

Resolve an alert (single-person action suitable for pilot testing):

```http
PUT /api/alerts/{alertId}/resolve
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "userId": "uuid",
  "reason": "Drill completed",
  "notes": "Optional resolution notes"
}
```

**Response**:
```json
{
  "success": true,
  "alertId": "uuid",
  "resolvedAt": "2025-11-04T12:34:56Z",
  "resolvedBy": "uuid"
}
```

---

**Production Roadmap (v2.0 - Phase 2 - All Clear Feature)**:

Two-person approval workflow for production compliance:

**Step 1: Request All Clear** (Person 1)
```http
POST /api/v1/alerts/{alertId}/all-clear/request
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "requesterId": "uuid",
  "reason": "Drill completed - all personnel accounted for",
  "checklistConfirmed": true
}
```

**Response**:
```json
{
  "approvalRequestId": "uuid",
  "status": "pending_approval",
  "requestedBy": "user@example.com",
  "requestedAt": "2025-11-04T12:34:56Z",
  "expiresAt": "2025-11-04T13:34:56Z"
}
```

**Step 2: Approve All Clear** (Person 2 - different user)
```http
POST /api/v1/alerts/{alertId}/all-clear/approve
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "approvalRequestId": "uuid",
  "approverId": "uuid",
  "mfaToken": "123456",
  "confirmation": "I confirm all clear conditions are met"
}
```

**Response**:
```json
{
  "success": true,
  "alertId": "uuid",
  "clearedAt": "2025-11-04T12:35:30Z",
  "requestedBy": "user1@example.com",
  "approvedBy": "user2@example.com",
  "auditTrail": {
    "requestTimestamp": "2025-11-04T12:34:56Z",
    "approvalTimestamp": "2025-11-04T12:35:30Z",
    "mfaVerified": true
  }
}
```

**Implementation Timeline**: Phase 2 (Weeks 3-4)
**Development Effort**: 28 hours (backend 16h + mobile 12h)
**Compliance**: Meets two-person rule requirements for safety-critical systems
```

---

### 2. Add API Versioning Note

**Insert at top of API Endpoints section**:

```markdown
## API Versioning

**MVP Implementation (v1.0 - Current)**:
- API routes: `/api/{controller}` (e.g., `/api/alerts`, `/api/auth/login`)
- No versioning prefix for MVP simplicity

**Production Roadmap (v2.0 - Phase 3)**:
- API routes: `/api/v1/{controller}`
- Versioning strategy for backward compatibility
- Deprecation policy and migration guides

**Note**: Documentation shows `/api/v1/...` for consistency with production plans. Current MVP uses `/api/...` without version prefix.
```

---

### 3. Alert Acknowledge Endpoint - Add MVP Marker

**Find the Acknowledge section and update**:

```markdown
### Acknowledge Alert

**MVP Implementation (v1.0 - Current)** ✅

Acknowledge that an alert has been seen by a responder:

```http
PUT /api/alerts/{alertId}/acknowledge
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "userId": "uuid",
  "acknowledgedAt": "2025-11-04T12:34:56Z",
  "notes": "Security team dispatched"
}
```

**Response**:
```json
{
  "success": true,
  "alertId": "uuid",
  "acknowledgedBy": "user@example.com",
  "acknowledgedAt": "2025-11-04T12:34:56Z"
}
```

**Status**: ✅ Implemented and tested
```

---

### 4. Add Current Endpoints Summary Section

**Insert after the API Versioning note**:

```markdown
## MVP Endpoints (v1.0 - Current Implementation)

### ✅ Implemented and Functional

#### Authentication & Users
```http
POST   /api/auth/login           # JWT authentication
POST   /api/auth/refresh         # Token refresh
GET    /api/users/me             # Current user profile
PUT    /api/users/me             # Update profile
```

#### Organizations & Buildings
```http
POST   /api/organizations        # Create organization
GET    /api/organizations        # List organizations (paginated)
GET    /api/organizations/{id}   # Get organization details
PUT    /api/organizations/{id}   # Update organization
DELETE /api/organizations/{id}   # Soft delete organization

GET    /api/buildings                    # List buildings by organization
POST   /api/buildings                    # Create building
GET    /api/buildings/{id}/rooms         # List rooms in building
POST   /api/buildings/{buildingId}/rooms # Create room
```

#### Alerts
```http
GET    /api/alerts               # List alerts (paginated, filterable)
POST   /api/alerts/trigger       # Trigger emergency alert
GET    /api/alerts/{id}          # Get alert details
PUT    /api/alerts/{id}/acknowledge # Acknowledge alert
PUT    /api/alerts/{id}/resolve  # Resolve alert (single-person MVP)
```

#### Devices
```http
POST   /api/devices/register     # Register ESP32 device
GET    /api/devices              # List devices by organization
PUT    /api/devices/{id}/push-token # Update push notification token
```

---

### 🚧 Roadmap Features (Not Yet Implemented)

#### Phase 2: All Clear Two-Person Workflow (Weeks 3-4)
```http
POST   /api/v1/alerts/{id}/all-clear/request   # Request All Clear (person 1)
POST   /api/v1/alerts/{id}/all-clear/approve   # Approve All Clear (person 2)
GET    /api/v1/alerts/{id}/all-clear/status    # Check approval status
```

#### Phase 3: Policy Management (Week 5)
```http
GET    /api/v1/policies                 # List alert policies
POST   /api/v1/policies                 # Create policy
PUT    /api/v1/policies/{id}            # Update policy
DELETE /api/v1/policies/{id}            # Delete policy
```

#### Phase 6: Notifications (Weeks 7-8)
```http
POST   /api/v1/notifications/sms        # Send SMS notification
POST   /api/v1/notifications/voice      # Trigger voice call
POST   /api/v1/notifications/email      # Send email notification
GET    /api/v1/notifications/history    # Notification delivery history
```

---
```

---

## Summary of Changes

| Section | Change Type | Impact |
|---------|-------------|--------|
| All Clear Endpoint | Split MVP / Roadmap | Shows single-person resolve (current) vs two-person approval (roadmap) |
| API Versioning | New section | Explains `/api/` vs `/api/v1/` discrepancy |
| Endpoints Summary | New section | Clear list of implemented vs roadmap endpoints |
| Acknowledge Endpoint | Add MVP marker | Confirms implementation status |

---

## Application Instructions

**Option A: Manual Update**
1. Open `../safeSignal-doc/documentation/development/api-documentation.md`
2. Add API Versioning note at top of endpoints section
3. Replace All Clear section with MVP + Roadmap split
4. Add Current Endpoints Summary
5. Mark implemented endpoints with ✅

**Option B: Automated (if git repo)**
```bash
cd ../safeSignal-doc

git checkout -b docs/api-mvp-roadmap

# Apply changes manually
# Then commit

git add documentation/development/api-documentation.md
git commit -m "docs: Clarify MVP API endpoints vs production roadmap

- Add API versioning note (/api vs /api/v1 explanation)
- Split All Clear: MVP single-person resolve vs Production two-person approval
- Add comprehensive current endpoints summary
- Mark implemented endpoints with ✅ status
- Document Phase 2 All Clear feature (28h, Weeks 3-4)
- Add roadmap sections for policy management and notifications"

git push origin docs/api-mvp-roadmap
```

---

**Related Files**:
- `DOC_UPDATES_system-overview.md` - Architecture updates
- `DOC_UPDATES_README.md` - Marketing claims updates
- `DOCUMENTATION_GAPS.md` - Complete gap analysis
- `../safeSignal-mvp/cloudedocs/REVISED_ROADMAP.md` - Implementation timeline

**Next Review**: After Phase 2 All Clear implementation (Week 4)
