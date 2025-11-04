# All Clear Workflow - Quick Start Implementation Guide

**For Developers:** Fast-track guide to complete the two-person "All Clear" workflow implementation.

## 📋 Prerequisites Done

✅ Data model designed
✅ Entities created (AlertClearance, extended Alert)
✅ Repository interfaces defined
✅ Database configuration complete

## 🚀 Implementation Steps (3 Days)

### Day 1: Backend Foundation

#### Step 1: Generate Migration (15 min)
```bash
cd cloud-backend
dotnet ef migrations add AddAllClearWorkflow \
  --project src/Infrastructure \
  --startup-project src/Api
```

**Review Generated Migration:**
- Check `alert_clearances` table creation
- Check `alert_history` column additions
- **CRITICAL:** Check AlertStatus enum update (may need manual adjustment)

**Apply Migration:**
```bash
dotnet ef database update \
  --project src/Infrastructure \
  --startup-project src/Api
```

#### Step 2: Implement AlertClearanceRepository (30 min)

**Create:** `cloud-backend/src/Infrastructure/Repositories/AlertClearanceRepository.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;
using SafeSignal.Cloud.Core.Interfaces;
using SafeSignal.Cloud.Infrastructure.Data;

namespace SafeSignal.Cloud.Infrastructure.Repositories;

public class AlertClearanceRepository : IAlertClearanceRepository
{
    private readonly SafeSignalDbContext _context;

    public AlertClearanceRepository(SafeSignalDbContext context)
    {
        _context = context;
    }

    public async Task<AlertClearance?> GetByIdAsync(Guid id)
    {
        return await _context.AlertClearances
            .Include(c => c.User)
            .Include(c => c.Alert)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<AlertClearance>> GetByAlertIdAsync(Guid alertId)
    {
        return await _context.AlertClearances
            .Include(c => c.User)
            .Where(c => c.AlertId == alertId)
            .OrderBy(c => c.ClearanceStep)
            .ToListAsync();
    }

    public async Task<List<AlertClearance>> GetByOrganizationIdAsync(
        Guid organizationId, DateTime fromDate, DateTime toDate)
    {
        return await _context.AlertClearances
            .Include(c => c.User)
            .Include(c => c.Alert)
            .Where(c => c.OrganizationId == organizationId &&
                       c.ClearedAt >= fromDate &&
                       c.ClearedAt <= toDate)
            .OrderByDescending(c => c.ClearedAt)
            .ToListAsync();
    }

    public async Task AddAsync(AlertClearance clearance)
    {
        await _context.AlertClearances.AddAsync(clearance);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
```

#### Step 3: Extend AlertRepository (20 min)

**Update:** `cloud-backend/src/Infrastructure/Repositories/AlertRepository.cs`

Add these methods:

```csharp
public async Task<Alert?> GetByIdWithClearancesAsync(Guid id)
{
    return await _context.Alerts
        .Include(a => a.Clearances)
            .ThenInclude(c => c.User)
        .Include(a => a.FirstClearanceUser)
        .Include(a => a.SecondClearanceUser)
        .Include(a => a.Building)
        .Include(a => a.Room)
        .FirstOrDefaultAsync(a => a.Id == id);
}

public async Task<List<Alert>> GetPendingClearanceAlertsAsync(
    Guid organizationId, int skip, int take)
{
    return await _context.Alerts
        .Include(a => a.FirstClearanceUser)
        .Include(a => a.Building)
        .Include(a => a.Room)
        .Where(a => a.OrganizationId == organizationId &&
                    a.Status == AlertStatus.PendingClearance)
        .OrderBy(a => a.FirstClearanceAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

#### Step 4: Create DTOs (30 min)

**Create:** `cloud-backend/src/Api/DTOs/AlertClearanceDtos.cs`

```csharp
namespace SafeSignal.Cloud.Api.DTOs;

public record ClearAlertRequest(
    string? Notes,
    LocationDto? Location
);

public record LocationDto(
    double Latitude,
    double Longitude
);

public record ClearAlertResponse(
    Guid AlertId,
    string Status,
    string Message,
    int ClearanceStep,
    Guid ClearanceId,
    string ClearedBy,
    DateTime ClearedAt,
    bool RequiresSecondClearance,
    ClearanceInfoDto? FirstClearance = null,
    ClearanceInfoDto? SecondClearance = null
);

public record ClearanceInfoDto(
    Guid UserId,
    string UserName,
    DateTime ClearedAt
);

public record AlertClearanceHistoryResponse(
    Guid AlertId,
    string Status,
    List<AlertClearanceDto> Clearances
);

public record AlertClearanceDto(
    Guid Id,
    int ClearanceStep,
    Guid UserId,
    string UserName,
    string UserEmail,
    DateTime ClearedAt,
    string? Notes,
    LocationDto? Location,
    string? DeviceInfo
);
```

#### Step 5: Add Controller Endpoints (2 hours)

**Update:** `cloud-backend/src/Api/Controllers/AlertsController.cs`

Add these fields to constructor:
```csharp
private readonly IAlertClearanceRepository _clearanceRepository;
private readonly IUserRepository _userRepository;
private readonly IAuditService _auditService;
```

Add endpoint POST /api/alerts/{id}/clear:
```csharp
[HttpPost("{id:guid}/clear")]
public async Task<ActionResult<ClearAlertResponse>> ClearAlert(
    Guid id,
    [FromBody] ClearAlertRequest request)
{
    // Get alert with clearances
    var alert = await _alertRepository.GetByIdWithClearancesAsync(id);
    if (alert == null)
        return NotFound(new { error = "Alert not found" });

    // Validate organization access
    try
    {
        ValidateOrganizationAccess(alert.OrganizationId);
    }
    catch (UnauthorizedAccessException)
    {
        return NotFound();
    }

    var currentUserId = GetAuthenticatedUserId();
    var currentUser = await _userRepository.GetByIdAsync(currentUserId);

    // Check if already fully cleared
    if (alert.Status == AlertStatus.Resolved && alert.FullyClearedAt.HasValue)
    {
        return BadRequest(new
        {
            error = "Alert already fully cleared",
            code = "ALREADY_CLEARED",
            fullyClearedAt = alert.FullyClearedAt
        });
    }

    // Determine clearance step
    int clearanceStep;
    if (alert.FirstClearanceUserId == null)
    {
        clearanceStep = 1;
    }
    else if (alert.SecondClearanceUserId == null)
    {
        clearanceStep = 2;

        // Validate different user
        if (alert.FirstClearanceUserId == currentUserId)
        {
            var firstUser = await _userRepository.GetByIdAsync(alert.FirstClearanceUserId.Value);
            return BadRequest(new
            {
                error = "Same user cannot provide both clearances",
                code = "SAME_USER_CLEARANCE",
                firstClearedBy = firstUser?.FirstName + " " + firstUser?.LastName
            });
        }
    }
    else
    {
        return BadRequest(new
        {
            error = "Alert already has two clearances",
            code = "ALREADY_CLEARED"
        });
    }

    // Create clearance record
    var clearance = new AlertClearance
    {
        Id = Guid.NewGuid(),
        AlertId = id,
        UserId = currentUserId,
        OrganizationId = alert.OrganizationId,
        ClearanceStep = clearanceStep,
        ClearedAt = DateTime.UtcNow,
        Notes = request.Notes,
        Location = request.Location != null
            ? System.Text.Json.JsonSerializer.Serialize(request.Location)
            : null,
        DeviceInfo = GetDeviceInfo(Request),
        CreatedAt = DateTime.UtcNow
    };

    await _clearanceRepository.AddAsync(clearance);

    // Update alert
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
        alert.ResolvedAt = clearance.ClearedAt;
    }

    await _alertRepository.UpdateAsync(alert);
    await _alertRepository.SaveChangesAsync();

    // Audit log
    await _auditService.LogAsync(new AuditLog
    {
        Action = clearanceStep == 1 ? "ALERT_FIRST_CLEARANCE" : "ALERT_SECOND_CLEARANCE",
        EntityType = "Alert",
        EntityId = alert.Id.ToString(),
        UserId = currentUserId,
        OrganizationId = alert.OrganizationId,
        Category = AuditCategory.AlertManagement,
        NewValues = System.Text.Json.JsonSerializer.Serialize(clearance),
        Success = true
    });

    _logger.LogInformation(
        "Alert {AlertId} clearance step {Step} by user {UserId}",
        alert.AlertId, clearanceStep, currentUserId);

    // Build response
    return Ok(MapToClearResponse(alert, clearance, currentUser));
}

private static string GetDeviceInfo(HttpRequest request)
{
    var userAgent = request.Headers["User-Agent"].ToString();
    return userAgent.Length > 500 ? userAgent.Substring(0, 500) : userAgent;
}

private static ClearAlertResponse MapToClearResponse(
    Alert alert, AlertClearance clearance, User currentUser)
{
    return new ClearAlertResponse(
        alert.Id,
        alert.Status.ToString(),
        clearance.ClearanceStep == 1
            ? "First clearance recorded. Awaiting second verification."
            : "Alert fully resolved with two-person verification.",
        clearance.ClearanceStep,
        clearance.Id,
        currentUser.FirstName + " " + currentUser.LastName,
        clearance.ClearedAt,
        clearance.ClearanceStep == 1,
        alert.FirstClearanceUser != null ? new ClearanceInfoDto(
            alert.FirstClearanceUserId!.Value,
            alert.FirstClearanceUser.FirstName + " " + alert.FirstClearanceUser.LastName,
            alert.FirstClearanceAt!.Value
        ) : null,
        alert.SecondClearanceUser != null ? new ClearanceInfoDto(
            alert.SecondClearanceUserId!.Value,
            alert.SecondClearanceUser.FirstName + " " + alert.SecondClearanceUser.LastName,
            alert.SecondClearanceAt!.Value
        ) : null
    );
}
```

Add endpoint GET /api/alerts/{id}/clearances:
```csharp
[HttpGet("{id:guid}/clearances")]
public async Task<ActionResult<AlertClearanceHistoryResponse>> GetClearances(Guid id)
{
    var alert = await _alertRepository.GetByIdWithClearancesAsync(id);
    if (alert == null)
        return NotFound();

    ValidateOrganizationAccess(alert.OrganizationId);

    return Ok(new AlertClearanceHistoryResponse(
        alert.Id,
        alert.Status.ToString(),
        alert.Clearances
            .OrderBy(c => c.ClearanceStep)
            .Select(c => new AlertClearanceDto(
                c.Id,
                c.ClearanceStep,
                c.UserId,
                c.User.FirstName + " " + c.User.LastName,
                c.User.Email,
                c.ClearedAt,
                c.Notes,
                c.Location != null
                    ? System.Text.Json.JsonSerializer.Deserialize<LocationDto>(c.Location)
                    : null,
                c.DeviceInfo
            ))
            .ToList()
    ));
}
```

#### Step 6: Register Services (5 min)

**Update:** `cloud-backend/src/Api/Program.cs`

Add:
```csharp
builder.Services.AddScoped<IAlertClearanceRepository, AlertClearanceRepository>();
```

#### Step 7: Test Backend (1 hour)

```bash
# Start backend
cd cloud-backend
dotnet run --project src/Api

# Test POST /api/alerts/{id}/clear (first clearance)
curl -X POST http://localhost:5118/api/alerts/{alert-id}/clear \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"notes": "Area checked, all clear", "location": {"latitude": 40.7128, "longitude": -74.0060}}'

# Test POST /api/alerts/{id}/clear (second clearance with different user)
curl -X POST http://localhost:5118/api/alerts/{alert-id}/clear \
  -H "Authorization: Bearer {different-user-token}" \
  -H "Content-Type: application/json" \
  -d '{"notes": "Confirmed safe"}'

# Test GET /api/alerts/{id}/clearances
curl http://localhost:5118/api/alerts/{alert-id}/clearances \
  -H "Authorization: Bearer {token}"
```

### Day 2: Mobile Implementation

#### Step 1: Update Types (15 min)

**Update:** `mobile/src/types/index.ts`

```typescript
export enum AlertStatus {
  New = 'New',
  Acknowledged = 'Acknowledged',
  PendingClearance = 'PendingClearance',  // ADD THIS
  Resolved = 'Resolved',
  Cancelled = 'Cancelled'
}

export interface Alert {
  id: string;
  alertId: string;
  organizationId: string;
  status: AlertStatus;
  triggeredAt: string;
  resolvedAt?: string;
  // ... existing fields ...

  // ADD THESE:
  firstClearance?: {
    userId: string;
    userName: string;
    clearedAt: string;
  };
  secondClearance?: {
    userId: string;
    userName: string;
    clearedAt: string;
  };
}
```

#### Step 2: Update API Client (20 min)

**Update:** `mobile/src/services/api.ts`

```typescript
// ADD THESE METHODS:

export async function clearAlert(
  alertId: string,
  notes?: string,
  location?: { latitude: number; longitude: number }
): Promise<any> {
  const response = await apiClient.post(`/api/alerts/${alertId}/clear`, {
    notes,
    location
  });
  return response.data;
}

export async function getAlertClearances(alertId: string): Promise<any> {
  const response = await apiClient.get(`/api/alerts/${alertId}/clearances`);
  return response.data;
}
```

#### Step 3: Create AlertClearanceScreen (1 hour)

**Create:** `mobile/src/screens/AlertClearanceScreen.tsx`

```typescript
import React, { useState } from 'react';
import { View, Text, TextInput, StyleSheet, Alert as RNAlert, ScrollView } from 'react-native';
import * as Location from 'expo-location';
import { Button } from '../components';
import { clearAlert } from '../services/api';

interface Props {
  route: { params: { alertId: string } };
  navigation: any;
}

export function AlertClearanceScreen({ route, navigation }: Props) {
  const { alertId } = route.params;
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(false);

  const handleClear = async () => {
    try {
      setLoading(true);

      // Request location permission
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
      const response = await clearAlert(alertId, notes, location);

      if (response.clearanceStep === 1) {
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
      } else if (error.response?.data?.code === 'ALREADY_CLEARED') {
        RNAlert.alert(
          'Already Cleared',
          'This alert has already been fully cleared.',
          [{ text: 'OK', onPress: () => navigation.goBack() }]
        );
      } else {
        RNAlert.alert('Error', 'Failed to clear alert. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <ScrollView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Verify All Clear</Text>
        <Text style={styles.subtitle}>
          This requires two different people to verify before the alert is fully resolved.
        </Text>

        <TextInput
          style={styles.input}
          placeholder="Notes (optional)"
          placeholderTextColor="#999"
          value={notes}
          onChangeText={setNotes}
          multiline
          numberOfLines={4}
          maxLength={1000}
        />

        <Button
          title={loading ? 'Submitting...' : 'Verify All Clear'}
          onPress={handleClear}
          disabled={loading}
        />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  content: {
    padding: 20,
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    marginBottom: 10,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
    marginBottom: 20,
  },
  input: {
    backgroundColor: 'white',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    minHeight: 100,
    textAlignVertical: 'top',
    marginBottom: 20,
    borderWidth: 1,
    borderColor: '#ddd',
  },
});
```

#### Step 4: Update AlertHistoryScreen (30 min)

**Update:** `mobile/src/screens/AlertHistoryScreen.tsx`

Add status badge component:
```typescript
function AlertStatusBadge({ alert }: { alert: Alert }) {
  if (alert.status === 'PendingClearance') {
    return (
      <View style={styles.pendingBadge}>
        <Text style={styles.pendingText}>⏳ Awaiting 2nd Verification</Text>
        {alert.firstClearance && (
          <Text style={styles.pendingSubtext}>
            1st cleared by {alert.firstClearance.userName}
          </Text>
        )}
      </View>
    );
  }

  if (alert.status === 'Resolved' && alert.firstClearance && alert.secondClearance) {
    return (
      <View style={styles.resolvedBadge}>
        <Text style={styles.resolvedText}>✅ Verified by 2 People</Text>
      </View>
    );
  }

  // ... other status badges ...
}
```

Add "Clear Alert" button to alert items:
```typescript
<Button
  title="Clear Alert"
  onPress={() => navigation.navigate('AlertClearance', { alertId: alert.id })}
  variant="secondary"
  size="small"
/>
```

#### Step 5: Update Navigation (10 min)

**Update:** `mobile/src/navigation/index.tsx`

```typescript
import { AlertClearanceScreen } from '../screens/AlertClearanceScreen';

// Add to stack navigator:
<Stack.Screen
  name="AlertClearance"
  component={AlertClearanceScreen}
  options={{ title: 'Clear Alert' }}
/>
```

#### Step 6: Test Mobile (1 hour)

```bash
# Run mobile app
cd mobile
npm start

# Test workflow:
# 1. Login as user A
# 2. Navigate to alert history
# 3. Tap "Clear Alert" on an alert
# 4. Enter notes and submit
# 5. Verify shows "Awaiting 2nd Verification"
# 6. Logout, login as user B
# 7. Tap "Clear Alert" on same alert
# 8. Submit clearance
# 9. Verify shows "Verified by 2 People"
```

### Day 3: Testing & Documentation

- Unit tests for repositories
- Integration tests for API endpoints
- E2E test for full workflow
- Update API_ENDPOINTS.md
- Create compliance reporting guide
- Create user guide

## ✅ Completion Checklist

### Backend
- [ ] Migration generated and applied
- [ ] AlertClearanceRepository implemented
- [ ] AlertRepository extended
- [ ] DTOs created
- [ ] POST /api/alerts/{id}/clear works
- [ ] GET /api/alerts/{id}/clearances works
- [ ] Same-user prevention works
- [ ] Audit logs created
- [ ] Service registered in DI

### Mobile
- [ ] Types updated with clearance fields
- [ ] API methods added
- [ ] AlertClearanceScreen created
- [ ] AlertHistoryScreen updated
- [ ] Navigation configured
- [ ] Location permissions handled
- [ ] Error handling works

### Testing
- [ ] Can trigger alert
- [ ] Can clear alert (first)
- [ ] Can clear alert (second, different user)
- [ ] Same-user error handled
- [ ] Already-cleared error handled
- [ ] Pending clearance shown correctly
- [ ] Fully cleared shown correctly
- [ ] Audit logs verified

## 🎯 Success Metrics

- **Compliance:** Two different users required ✅
- **Audit Trail:** Complete record captured ✅
- **User Experience:** Clear workflow, good error messages ✅
- **Performance:** Fast queries with proper indexes ✅
- **Security:** Organization isolation enforced ✅

## 📚 Reference Documents

- **Design:** `claudedocs/all-clear-workflow-design.md` (comprehensive spec)
- **Status:** `claudedocs/all-clear-implementation-status.md` (current progress)
- **This Guide:** `claudedocs/all-clear-quick-start.md` (you are here)

---

**Estimated Time:** 3 days
**Priority:** HIGH (compliance critical)
**Impact:** Enables credible two-person accountability audit trail
