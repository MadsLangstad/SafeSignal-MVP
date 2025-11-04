# Two-Person "All Clear" Workflow Design

**Date:** 2025-11-04
**Status:** Design Complete - Ready for Implementation
**Compliance Requirement:** Two-person accountability for alert resolution

## Business Requirements

### Core Workflow
1. **Alert Triggered** - Emergency button pressed or mobile alert sent
2. **First Responder** - Security personnel responds to location
3. **First Verification** - First responder marks situation as "All Clear" (pending)
4. **Second Responder** - Different person must verify
5. **Second Verification** - Second responder confirms "All Clear"
6. **Resolution** - Alert marked as resolved with full audit trail

###Compliance Goals
- **Accountability:** Two different people must verify safety
- **Audit Trail:** Complete record of who verified, when, and from where
- **Non-repudiation:** Digital signatures prevent disputes
- **Temporal Tracking:** Exact timestamps for compliance reporting

## Data Model Design

### New Entity: AlertClearance

**Purpose:** Tracks each clearance action in the two-step process

```csharp
public class AlertClearance
{
    public Guid Id { get; set; }
    public Guid AlertId { get; set; }                // FK to Alert
    public Guid UserId { get; set; }                 // Who cleared it
    public Guid OrganizationId { get; set; }         // For data isolation
    public int ClearanceStep { get; set; }           // 1 = First, 2 = Second
    public DateTime ClearedAt { get; set; }          // Timestamp (UTC)
    public string? Notes { get; set; }               // Optional notes
    public string? Location { get; set; }            // GPS coordinates (JSON)
    public string? DeviceInfo { get; set; }          // Device/browser info
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Alert Alert { get; set; } = null!;
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
```

### Extended Alert Entity

**New Properties:**
```csharp
public class Alert  // Existing entity - ADD these properties
{
    // ... existing properties ...

    // All Clear workflow tracking
    public DateTime? FirstClearanceAt { get; set; }   // When first person cleared
    public Guid? FirstClearanceUserId { get; set; }    // Who cleared first
    public DateTime? SecondClearanceAt { get; set; }   // When second person cleared
    public Guid? SecondClearanceUserId { get; set; }   // Who cleared second
    public DateTime? FullyClearedAt { get; set; }      // When both cleared (denormalized)

    // Navigation properties
    public ICollection<AlertClearance> Clearances { get; set; } = new List<AlertClearance>();
}
```

### Extended AlertStatus Enum

```csharp
public enum AlertStatus
{
    New,                    // Initial state
    Acknowledged,           // Someone saw it
    PendingClearance,       // First person cleared, awaiting second
    Resolved,               // Both people cleared (legacy + new workflow)
    Cancelled               // Alert cancelled/false alarm
}
```

## Database Migration

### Migration: AddAllClearWorkflow

**Up Migration:**
```sql
-- Create alert_clearances table
CREATE TABLE alert_clearances (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_id UUID NOT NULL REFERENCES alert_history(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    clearance_step INT NOT NULL CHECK (clearance_step IN (1, 2)),
    cleared_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    notes TEXT,
    location JSONB,
    device_info VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for performance
CREATE INDEX idx_alert_clearances_alert_id ON alert_clearances(alert_id);
CREATE INDEX idx_alert_clearances_user_id ON alert_clearances(user_id);
CREATE INDEX idx_alert_clearances_org_cleared ON alert_clearances(organization_id, cleared_at);
CREATE UNIQUE INDEX idx_alert_clearances_step ON alert_clearances(alert_id, clearance_step);

-- Add All Clear columns to alert_history
ALTER TABLE alert_history
ADD COLUMN first_clearance_at TIMESTAMPTZ,
ADD COLUMN first_clearance_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
ADD COLUMN second_clearance_at TIMESTAMPTZ,
ADD COLUMN second_clearance_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
ADD COLUMN fully_cleared_at TIMESTAMPTZ;

-- Add PendingClearance to AlertStatus enum
-- Note: PostgreSQL enum modification requires careful handling
ALTER TYPE alert_status RENAME TO alert_status_old;
CREATE TYPE alert_status AS ENUM ('New', 'Acknowledged', 'PendingClearance', 'Resolved', 'Cancelled');
ALTER TABLE alert_history ALTER COLUMN status TYPE alert_status USING status::text::alert_status;
DROP TYPE alert_status_old;

-- Indexes for All Clear queries
CREATE INDEX idx_alert_history_pending_clearance ON alert_history(organization_id, status)
WHERE status = 'PendingClearance';
CREATE INDEX idx_alert_history_first_clearance ON alert_history(first_clearance_user_id, first_clearance_at)
WHERE first_clearance_user_id IS NOT NULL;
```

**Down Migration:**
```sql
-- Drop alert_clearances table
DROP TABLE IF EXISTS alert_clearances;

-- Remove All Clear columns
ALTER TABLE alert_history
DROP COLUMN IF EXISTS first_clearance_at,
DROP COLUMN IF EXISTS first_clearance_user_id,
DROP COLUMN IF EXISTS second_clearance_at,
DROP COLUMN IF EXISTS second_clearance_user_id,
DROP COLUMN IF EXISTS fully_cleared_at;

-- Revert AlertStatus enum
ALTER TYPE alert_status RENAME TO alert_status_new;
CREATE TYPE alert_status AS ENUM ('New', 'Acknowledged', 'Resolved', 'Cancelled');
ALTER TABLE alert_history ALTER COLUMN status TYPE alert_status USING
    CASE
        WHEN status::text = 'PendingClearance' THEN 'Acknowledged'::alert_status
        ELSE status::text::alert_status
    END;
DROP TYPE alert_status_new;
```

## API Endpoints

### POST /api/alerts/{id}/clear

**Purpose:** Record first or second clearance

**Authorization:** Requires authenticated user

**Request Body:**
```json
{
  "notes": "string (optional, max 1000 chars)",
  "location": {
    "latitude": 40.7128,
    "longitude": -74.0060
  }
}
```

**Response (First Clearance - 200 OK):**
```json
{
  "alertId": "uuid",
  "status": "PendingClearance",
  "message": "First clearance recorded. Awaiting second verification.",
  "clearanceStep": 1,
  "clearanceId": "uuid",
  "clearedBy": "John Doe",
  "clearedAt": "2025-11-04T12:30:00Z",
  "requiresSecondClearance": true
}
```

**Response (Second Clearance - 200 OK):**
```json
{
  "alertId": "uuid",
  "status": "Resolved",
  "message": "Alert fully resolved with two-person verification.",
  "clearanceStep": 2,
  "clearanceId": "uuid",
  "clearedBy": "Jane Smith",
  "clearedAt": "2025-11-04T12:35:00Z",
  "requiresSecondClearance": false,
  "firstClearance": {
    "userId": "uuid",
    "userName": "John Doe",
    "clearedAt": "2025-11-04T12:30:00Z"
  },
  "secondClearance": {
    "userId": "uuid",
    "userName": "Jane Smith",
    "clearedAt": "2025-11-04T12:35:00Z"
  }
}
```

**Error Responses:**

**400 Bad Request - Same User:**
```json
{
  "error": "Same user cannot provide both clearances",
  "code": "SAME_USER_CLEARANCE",
  "firstClearedBy": "John Doe"
}
```

**400 Bad Request - Already Cleared:**
```json
{
  "error": "Alert already fully cleared",
  "code": "ALREADY_CLEARED",
  "fullyClearedAt": "2025-11-04T12:35:00Z"
}
```

**404 Not Found:**
```json
{
  "error": "Alert not found"
}
```

### GET /api/alerts/{id}/clearances

**Purpose:** Get clearance history for an alert

**Response:**
```json
{
  "alertId": "uuid",
  "status": "Resolved",
  "clearances": [
    {
      "id": "uuid",
      "clearanceStep": 1,
      "userId": "uuid",
      "userName": "John Doe",
      "userEmail": "john@example.com",
      "clearedAt": "2025-11-04T12:30:00Z",
      "notes": "Area checked, no hazards",
      "location": {
        "latitude": 40.7128,
        "longitude": -74.0060
      },
      "deviceInfo": "iPhone 14 Pro / iOS 17.1"
    },
    {
      "id": "uuid",
      "clearanceStep": 2,
      "userId": "uuid",
      "userName": "Jane Smith",
      "userEmail": "jane@example.com",
      "clearedAt": "2025-11-04T12:35:00Z",
      "notes": "Verified safe, all clear",
      "location": {
        "latitude": 40.7128,
        "longitude": -74.0060
      },
      "deviceInfo": "Android / Pixel 7"
    }
  ]
}
```

### GET /api/alerts?status=PendingClearance

**Purpose:** List alerts awaiting second clearance

**Query Parameters:**
- `status` (optional): Filter by status
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20)

**Response:**
```json
[
  {
    "id": "uuid",
    "alertId": "string",
    "organizationId": "uuid",
    "status": "PendingClearance",
    "triggeredAt": "2025-11-04T12:25:00Z",
    "firstClearance": {
      "userId": "uuid",
      "userName": "John Doe",
      "clearedAt": "2025-11-04T12:30:00Z"
    },
    "buildingName": "Main Building",
    "roomNumber": "101",
    "severity": "High"
  }
]
```

## Backend Implementation

### AlertsController Extensions

```csharp
[HttpPost("{id:guid}/clear")]
public async Task<ActionResult<ClearAlertResponse>> ClearAlert(
    Guid id,
    [FromBody] ClearAlertRequest request)
{
    // 1. Get alert and validate access
    var alert = await _alertRepository.GetByIdWithClearancesAsync(id);
    if (alert == null) return NotFound(new { error = "Alert not found" });

    ValidateOrganizationAccess(alert.OrganizationId);
    var currentUserId = GetAuthenticatedUserId();

    // 2. Check if already fully cleared
    if (alert.Status == AlertStatus.Resolved && alert.FullyClearedAt.HasValue)
    {
        return BadRequest(new {
            error = "Alert already fully cleared",
            code = "ALREADY_CLEARED",
            fullyClearedAt = alert.FullyClearedAt
        });
    }

    // 3. Determine clearance step
    int clearanceStep;
    if (alert.FirstClearanceUserId == null)
    {
        // First clearance
        clearanceStep = 1;
    }
    else if (alert.SecondClearanceUserId == null)
    {
        // Second clearance
        clearanceStep = 2;

        // Validate different user
        if (alert.FirstClearanceUserId == currentUserId)
        {
            return BadRequest(new {
                error = "Same user cannot provide both clearances",
                code = "SAME_USER_CLEARANCE",
                firstClearedBy = alert.FirstClearanceUser?.Name
            });
        }
    }
    else
    {
        return BadRequest(new {
            error = "Alert already has two clearances",
            code = "ALREADY_CLEARED"
        });
    }

    // 4. Create clearance record
    var clearance = new AlertClearance
    {
        Id = Guid.NewGuid(),
        AlertId = id,
        UserId = currentUserId,
        OrganizationId = alert.OrganizationId,
        ClearanceStep = clearanceStep,
        ClearedAt = DateTime.UtcNow,
        Notes = request.Notes,
        Location = request.Location != null ? JsonSerializer.Serialize(request.Location) : null,
        DeviceInfo = GetDeviceInfo(Request),
        CreatedAt = DateTime.UtcNow
    };

    await _clearanceRepository.AddAsync(clearance);

    // 5. Update alert
    if (clearanceStep == 1)
    {
        alert.Status = AlertStatus.PendingClearance;
        alert.FirstClearanceAt = clearance.ClearedAt;
        alert.FirstClearanceUserId = currentUserId;
    }
    else
    {
        alert.Status = AlertStatus.Resolved;
        alert.SecondClearanceAt = clearance.ClearedAt;
        alert.SecondClearanceUserId = currentUserId;
        alert.FullyClearedAt = clearance.ClearedAt;
        alert.ResolvedAt = clearance.ClearedAt; // Legacy field
    }

    await _alertRepository.UpdateAsync(alert);
    await _alertRepository.SaveChangesAsync();

    // 6. Audit log
    await _auditService.LogAsync(new AuditLog
    {
        Action = clearanceStep == 1 ? "ALERT_FIRST_CLEARANCE" : "ALERT_SECOND_CLEARANCE",
        EntityType = "Alert",
        EntityId = alert.Id.ToString(),
        UserId = currentUserId,
        OrganizationId = alert.OrganizationId,
        Category = AuditCategory.AlertManagement,
        NewValues = JsonSerializer.Serialize(clearance),
        Success = true
    });

    _logger.LogInformation(
        "Alert {AlertId} clearance step {Step} by user {UserId}",
        alert.AlertId, clearanceStep, currentUserId);

    // 7. Return response
    return Ok(MapToClearResponse(alert, clearance));
}

[HttpGet("{id:guid}/clearances")]
public async Task<ActionResult<AlertClearanceHistoryResponse>> GetClearances(Guid id)
{
    var alert = await _alertRepository.GetByIdWithClearancesAsync(id);
    if (alert == null) return NotFound();

    ValidateOrganizationAccess(alert.OrganizationId);

    return Ok(new AlertClearanceHistoryResponse
    {
        AlertId = alert.Id,
        Status = alert.Status.ToString(),
        Clearances = alert.Clearances
            .OrderBy(c => c.ClearanceStep)
            .Select(MapToClearanceDto)
            .ToList()
    });
}

private static string GetDeviceInfo(HttpRequest request)
{
    var userAgent = request.Headers["User-Agent"].ToString();
    return userAgent.Length > 500 ? userAgent.Substring(0, 500) : userAgent;
}
```

### Repository Extensions

```csharp
public interface IAlertRepository
{
    // ... existing methods ...

    Task<Alert?> GetByIdWithClearancesAsync(Guid id);
    Task<List<Alert>> GetPendingClearanceAlertsAsync(
        Guid organizationId, int skip, int take);
}

public interface IAlertClearanceRepository
{
    Task<AlertClearance?> GetByIdAsync(Guid id);
    Task<List<AlertClearance>> GetByAlertIdAsync(Guid alertId);
    Task AddAsync(AlertClearance clearance);
    Task SaveChangesAsync();
}
```

## Mobile UI Implementation

### Alert History Screen - Show Clearance Status

```tsx
// src/screens/AlertHistoryScreen.tsx - ADD

interface AlertWithClearance {
  // ... existing Alert properties ...
  status: 'New' | 'Acknowledged' | 'PendingClearance' | 'Resolved' | 'Cancelled';
  firstClearance?: {
    userName: string;
    clearedAt: string;
  };
  secondClearance?: {
    userName: string;
    clearedAt: string;
  };
}

function AlertStatusBadge({ alert }: { alert: AlertWithClearance }) {
  if (alert.status === 'PendingClearance') {
    return (
      <View style={styles.pendingBadge}>
        <Text style={styles.pendingText}>
          ⏳ Awaiting 2nd Verification
        </Text>
        <Text style={styles.pendingSubtext}>
          1st cleared by {alert.firstClearance?.userName}
        </Text>
      </View>
    );
  }

  if (alert.status === 'Resolved' && alert.firstClearance && alert.secondClearance) {
    return (
      <View style={styles.resolvedBadge}>
        <Text style={styles.resolvedText}>
          ✅ Verified by 2 People
        </Text>
      </View>
    );
  }

  // ... other statuses ...
}
```

### New Screen: AlertClearanceScreen

```tsx
// src/screens/AlertClearanceScreen.tsx - NEW FILE

import React, { useState } from 'react';
import { View, Text, TextInput, Button, Alert as RNAlert } from 'react-native';
import * as Location from 'expo-location';
import { apiClient } from '../services/api';

interface AlertClearanceScreenProps {
  route: { params: { alertId: string } };
  navigation: any;
}

export function AlertClearanceScreen({ route, navigation }: AlertClearanceScreenProps) {
  const { alertId } = route.params;
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(false);

  const handleClearAlert = async () => {
    try {
      setLoading(true);

      // Get current location
      const { status } = await Location.requestForegroundPermissionsAsync();
      let location = null;

      if (status === 'granted') {
        const position = await Location.getCurrentPositionAsync({});
        location = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude
        };
      }

      // Submit clearance
      const response = await apiClient.post(`/api/alerts/${alertId}/clear`, {
        notes,
        location
      });

      if (response.data.clearanceStep === 1) {
        RNAlert.alert(
          'First Verification Recorded',
          'This alert now requires a second person to verify before it can be fully resolved.',
          [{ text: 'OK', onPress: () => navigation.goBack() }]
        );
      } else {
        RNAlert.alert(
          'Alert Fully Resolved',
          'Two-person verification complete. Alert has been resolved.',
          [{ text: 'OK', onPress: () => navigation.goBack() }]
        );
      }
    } catch (error: any) {
      if (error.response?.data?.code === 'SAME_USER_CLEARANCE') {
        RNAlert.alert(
          'Same User Error',
          'You cannot provide both verifications. A different person must verify.',
          [{ text: 'OK' }]
        );
      } else {
        RNAlert.alert('Error', 'Failed to clear alert. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Verify All Clear</Text>
      <Text style={styles.subtitle}>
        This requires two different people to verify before the alert is fully resolved.
      </Text>

      <TextInput
        style={styles.input}
        placeholder="Notes (optional)"
        value={notes}
        onChangeText={setNotes}
        multiline
        numberOfLines={4}
        maxLength={1000}
      />

      <Button
        title={loading ? 'Submitting...' : 'Verify All Clear'}
        onPress={handleClearAlert}
        disabled={loading}
      />
    </View>
  );
}
```

## Audit Trail Requirements

### AuditLog Category

```csharp
public enum AuditCategory
{
    // ... existing categories ...
    AlertManagement      // Add if not exists
}
```

### Audit Log Events

**Event: ALERT_FIRST_CLEARANCE**
```json
{
  "action": "ALERT_FIRST_CLEARANCE",
  "entityType": "Alert",
  "entityId": "alert-uuid",
  "userId": "user-uuid",
  "userEmail": "john@example.com",
  "organizationId": "org-uuid",
  "category": "AlertManagement",
  "newValues": {
    "clearanceId": "uuid",
    "alertId": "uuid",
    "clearanceStep": 1,
    "clearedAt": "2025-11-04T12:30:00Z",
    "notes": "Area checked, no hazards",
    "location": {"latitude": 40.7128, "longitude": -74.0060}
  },
  "ipAddress": "192.168.1.100",
  "userAgent": "SafeSignal Mobile App/1.0",
  "timestamp": "2025-11-04T12:30:00Z",
  "success": true
}
```

**Event: ALERT_SECOND_CLEARANCE**
```json
{
  "action": "ALERT_SECOND_CLEARANCE",
  "entityType": "Alert",
  "entityId": "alert-uuid",
  "userId": "user-uuid",
  "userEmail": "jane@example.com",
  "organizationId": "org-uuid",
  "category": "AlertManagement",
  "newValues": {
    "clearanceId": "uuid",
    "alertId": "uuid",
    "clearanceStep": 2,
    "clearedAt": "2025-11-04T12:35:00Z",
    "notes": "Verified safe, all clear"
  },
  "additionalInfo": {
    "firstClearanceUserId": "uuid",
    "firstClearanceAt": "2025-11-04T12:30:00Z",
    "timeBetweenClearances": "5 minutes"
  },
  "timestamp": "2025-11-04T12:35:00Z",
  "success": true
}
```

## Compliance Reporting

### Query: Get Two-Person Clearance Report

```sql
SELECT
    a.alert_id,
    a.triggered_at,
    a.fully_cleared_at,
    u1.email AS first_verifier,
    ac1.cleared_at AS first_verification_time,
    ac1.notes AS first_notes,
    u2.email AS second_verifier,
    ac2.cleared_at AS second_verification_time,
    ac2.notes AS second_notes,
    EXTRACT(EPOCH FROM (ac2.cleared_at - ac1.cleared_at)) / 60 AS minutes_between_clearances
FROM alert_history a
INNER JOIN alert_clearances ac1 ON a.id = ac1.alert_id AND ac1.clearance_step = 1
INNER JOIN alert_clearances ac2 ON a.id = ac2.alert_id AND ac2.clearance_step = 2
INNER JOIN users u1 ON ac1.user_id = u1.id
INNER JOIN users u2 ON ac2.user_id = u2.id
WHERE a.organization_id = 'org-uuid'
  AND a.fully_cleared_at IS NOT NULL
  AND a.triggered_at >= '2025-01-01'
ORDER BY a.triggered_at DESC;
```

### Export Format (CSV)

```csv
Alert ID,Triggered At,Building,Room,First Verifier,First Time,Second Verifier,Second Time,Time Between (min)
ALT-001,2025-11-04 12:25:00,Main Building,101,john@example.com,2025-11-04 12:30:00,jane@example.com,2025-11-04 12:35:00,5
ALT-002,2025-11-04 10:15:00,East Wing,205,jane@example.com,2025-11-04 10:18:00,mike@example.com,2025-11-04 10:22:00,4
```

## Testing Strategy

### Unit Tests

1. **AlertClearance Business Logic**
   - First clearance sets status to PendingClearance
   - Second clearance sets status to Resolved
   - Same user cannot clear twice
   - Cannot clear already-resolved alert

2. **Repository Tests**
   - Insert clearance record
   - Query alerts by PendingClearance status
   - Load alert with clearances (eager loading)

### Integration Tests

1. **API Endpoint Tests**
   - POST /api/alerts/{id}/clear (first clearance)
   - POST /api/alerts/{id}/clear (second clearance)
   - GET /api/alerts/{id}/clearances
   - Error cases (same user, already cleared)

2. **Mobile App Tests**
   - Navigate to clearance screen
   - Submit clearance with location
   - Handle same-user error
   - Display pending clearance status

### End-to-End Tests

1. **Full Workflow Test**
   - User A triggers alert
   - User B clears alert (first)
   - Verify status = PendingClearance
   - User C clears alert (second)
   - Verify status = Resolved
   - Check audit logs

## Implementation Checklist

### Backend (C#/.NET)
- [ ] Create AlertClearance entity
- [ ] Add migration AddAllClearWorkflow
- [ ] Extend Alert entity with clearance fields
- [ ] Add PendingClearance to AlertStatus enum
- [ ] Implement IAlertClearanceRepository
- [ ] Add POST /api/alerts/{id}/clear endpoint
- [ ] Add GET /api/alerts/{id}/clearances endpoint
- [ ] Extend GET /api/alerts to support PendingClearance filter
- [ ] Add audit logging for clearance actions
- [ ] Write unit tests
- [ ] Write integration tests

### Mobile (React Native)
- [ ] Update Alert type with clearance fields
- [ ] Create AlertClearanceScreen component
- [ ] Update AlertHistoryScreen to show clearance status
- [ ] Add "Clear Alert" button to alert detail
- [ ] Implement location permission handling
- [ ] Add same-user error handling
- [ ] Update navigation routes
- [ ] Test on iOS and Android

### Documentation
- [ ] Update API_ENDPOINTS.md
- [ ] Add compliance reporting guide
- [ ] Create user guide for two-person workflow
- [ ] Document audit trail queries

## Security Considerations

1. **Authorization:** All clearance endpoints require authentication
2. **Organization Isolation:** Users can only clear alerts in their organization
3. **Same-User Prevention:** Backend enforces different users for clearances
4. **Audit Logging:** All clearance actions logged with full context
5. **Location Privacy:** GPS coordinates stored but not required
6. **Non-repudiation:** User ID + timestamp + notes create verifiable record

## Performance Considerations

1. **Database Indexes:**
   - `alert_clearances(alert_id)` for fast lookup
   - `alert_history(status)` for PendingClearance queries
   - `alert_clearances(organization_id, cleared_at)` for reporting

2. **Query Optimization:**
   - Use eager loading for clearances when displaying alerts
   - Paginate pending clearance lists

3. **Mobile Performance:**
   - Cache clearance status locally
   - Sync on app launch and after clearance submission

## Future Enhancements (Not MVP)

1. **Push Notifications:** Notify when alert awaits second clearance
2. **Time Windows:** Require clearances within X minutes
3. **Role-Based:** Require specific roles for clearances (e.g., manager)
4. **Photo Verification:** Attach photos to clearance records
5. **Signature Capture:** Digital signatures on mobile
6. **Geofencing:** Verify user is at alert location

---

**Status:** Design complete and ready for implementation
**Estimated Effort:** 2-3 days (backend + mobile + testing)
**Compliance Impact:** Significant - enables full accountability audit trail
