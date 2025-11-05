# Mobile App 404 Error - Quick Fix

## Issue
Mobile app getting 404 errors when trying to access clearance endpoints:
```
ERROR API Error: {"message": "Request failed with status code 404", "method": "post", "status": 404, "urlPath": "/api/alerts/{id}/clear"}
```

## Root Cause
The controller's `ValidateOrganizationAccess()` method is failing and returning `NotFound()` instead of the alert data.

Possible causes:
1. JWT token missing `organizationId` claim
2. Controller reading wrong claim name
3. User not linked to organization in `user_organizations` table

## Verification Steps

### 1. Check JWT Token Claims
Decode the JWT token from mobile app logs to verify it contains the organizationId claim:
```bash
# Token from logs: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc...
# Decode at https://jwt.io or:
echo "TOKEN_HERE" | cut -d'.' -f2 | base64 -d | jq '.'
```

Expected claims:
```json
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "user-id",
  "organizationId": "a216abd0-2c87-4828-8823-48dc8c9f0a8a",
  ...
}
```

### 2. Check GetAuthenticatedOrganizationId() Method
File: `cloud-backend/src/Api/Controllers/AlertsController.cs`

Look for this method and verify it's reading the correct claim:
```csharp
private Guid GetAuthenticatedOrganizationId()
{
    // Should read from "organizationId" claim or similar
    var orgIdClaim = User.FindFirst("organizationId")?.Value;
    // OR
    var orgIdClaim = User.FindFirst("http://schemas.example.com/organizationId")?.Value;

    return Guid.Parse(orgIdClaim);
}
```

### 3. Verify User Organization Link
```sql
-- Check if user is linked to organization
SELECT u."Email", uo."OrganizationId", uo."Role"
FROM users u
JOIN user_organizations uo ON u."Id" = uo."UserId"
WHERE u."Email" = 'admin@safesignal.com';
```

Expected result:
```
Email                | OrganizationId                       | Role
---------------------|--------------------------------------|------------
admin@safesignal.com | a216abd0-2c87-4828-8823-48dc8c9f0a8a | SuperAdmin
```

## Quick Fix

If the issue is missing organizationId claim in JWT:

### File: `cloud-backend/src/Api/Services/AuthService.cs` (or wherever JWT is generated)

Ensure organization ID is added to JWT claims:
```csharp
// When generating JWT token
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new(JwtRegisteredClaimNames.Email, user.Email),
    new("organizationId", user.OrganizationId.ToString()), // ADD THIS
    // ... other claims
};
```

### Alternative: Check TenantId vs OrganizationId

The login response shows `tenantId` field - the backend might be using "tenantId" instead of "organizationId":
```json
{
  "user": {
    "tenantId": "a216abd0-2c87-4828-8823-48dc8c9f0a8a"
  }
}
```

Update controller to read `tenantId` claim instead:
```csharp
private Guid GetAuthenticatedOrganizationId()
{
    var tenantIdClaim = User.FindFirst("tenantId")?.Value;
    if (string.IsNullOrEmpty(tenantIdClaim))
    {
        throw new UnauthorizedAccessException("No tenant ID in token");
    }
    return Guid.Parse(tenantIdClaim);
}
```

## Testing After Fix

1. Restart backend API
2. Re-login on mobile app (to get fresh JWT with correct claims)
3. Try clearing an alert
4. Should see:
   ```
   LOG [API Request] POST /api/alerts/{id}/clear - Success
   ```

## Workaround (Temporary)

If you need to test immediately without fixing the backend, modify the controller to skip org validation temporarily:

```csharp
[HttpPost("{id:guid}/clear")]
public async Task<ActionResult<ClearAlertResponse>> ClearAlert(Guid id, [FromBody] ClearAlertRequest request)
{
    // TEMPORARY: Comment out org validation
    // ValidateOrganizationAccess(alert.OrganizationId);

    // ... rest of method
}
```

⚠️ **WARNING**: This removes security - only for local testing!

## Related Files
- `cloud-backend/src/Api/Controllers/AlertsController.cs` - Clearance endpoints
- `cloud-backend/src/Api/Services/AuthService.cs` - JWT generation
- `mobile/src/services/api.ts` - API client with token handling
- `mobile/src/screens/AlertClearanceScreen.tsx` - Clearance submission UI

## Status
✅ **FIXED** - 2025-11-04

### Root Cause
The `Building` navigation property relationship was missing from the EF Core DbContext configuration. When the controller called `GetByIdWithClearancesAsync()` which includes `.Include(a => a.Building)`, EF Core couldn't resolve the relationship and returned null, causing the 404 error.

### Solution Applied
**File: `cloud-backend/src/Infrastructure/Data/SafeSignalDbContext.cs`**
Added the Building relationship configuration in the Alert entity:
```csharp
entity.HasOne(e => e.Building)
    .WithMany(b => b.Alerts)
    .HasForeignKey(e => e.BuildingId)
    .OnDelete(DeleteBehavior.SetNull);
```

**File: `cloud-backend/src/Core/Entities/Building.cs`**
Added the Alerts collection navigation property:
```csharp
public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
```

### Verification
All endpoints now return 200 OK:
- ✅ GET `/api/alerts/{id}` - Returns alert details
- ✅ GET `/api/alerts/{id}/clearances` - Returns clearance history
- ✅ POST `/api/alerts/{id}/clear` - Successfully records clearances

**Note**: The JWT token was already correctly configured with the `organizationId` claim. The issue was purely the missing EF Core relationship configuration.
