# SafeSignal Cloud API Endpoints

## Base URL
- Development: `http://localhost:5118`
- API Base Path: `/api`

## Authentication

### POST /api/auth/login
Login with email and password.

**Request:**
```json
{
  "email": "test@example.com",
  "password": "testpass123"
}
```

**Response:**
```json
{
  "user": {
    "id": "7e140331-62d0-4b55-8021-578d8b4cb3f9",
    "email": "test@example.com",
    "name": "Test User",
    "tenantId": "85675cb9-61a3-460e-9609-8c3c7b9ae5cc",
    "assignedBuildingId": null,
    "assignedRoomId": null,
    "phoneNumber": null,
    "createdAt": "2025-11-02T15:44:47.141381Z"
  },
  "tokens": {
    "accessToken": "7e140331-62d0-4b55-8021-578d8b4cb3f9_...",
    "refreshToken": "7e140331-62d0-4b55-8021-578d8b4cb3f9_...",
    "expiresAt": "2025-11-03T16:45:00.702192Z"
  }
}
```

## Users

### GET /api/users/me
Get current user information.

**Headers:**
```
Authorization: Bearer {token}
```

**Response:**
```json
{
  "id": "7e140331-62d0-4b55-8021-578d8b4cb3f9",
  "email": "test@example.com",
  "name": "Test User",
  "tenantId": "85675cb9-61a3-460e-9609-8c3c7b9ae5cc",
  "assignedBuildingId": null,
  "assignedRoomId": null,
  "phoneNumber": null,
  "createdAt": "2025-11-02T15:44:47.141381Z"
}
```

## Buildings

### GET /api/buildings
List buildings for an organization.

**Query Parameters:**
- `organizationId` (required): Organization UUID

**Response:**
```json
[
  {
    "id": "uuid",
    "siteId": "uuid",
    "name": "Main Building",
    "address": "123 Main St",
    "floorCount": 3,
    "createdAt": "2025-11-02T15:43:20.006299Z"
  }
]
```

### POST /api/buildings
Create a new building.

**Request:**
```json
{
  "siteId": "uuid",
  "name": "Main Building",
  "address": "123 Main St",
  "floorCount": 3
}
```

## Alerts

### GET /api/alerts
List alerts for an organization.

**Query Parameters:**
- `organizationId` (required): Organization UUID
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20, max: 100)

**Response:**
```json
[
  {
    "id": "uuid",
    "alertId": "string",
    "organizationId": "uuid",
    "deviceId": "uuid",
    "roomId": "uuid",
    "triggeredAt": "2025-11-02T15:43:20Z",
    "severity": "High",
    "alertType": "emergency",
    "status": "New",
    "source": "Mobile"
  }
]
```

### POST /api/alerts/trigger
Trigger a new alert (simplified for mobile app).

**Request:**
```json
{
  "organizationId": "uuid",
  "deviceId": "uuid",
  "roomId": "uuid",
  "metadata": "optional metadata"
}
```

**Response:**
```json
{
  "id": "uuid",
  "alertId": "generated-uuid",
  "organizationId": "uuid",
  "severity": "High",
  "alertType": "emergency",
  "status": "New",
  "source": "Mobile",
  "triggeredAt": "2025-11-02T15:43:20Z"
}
```

## Devices

### GET /api/devices
List devices.

**Query Parameters:**
- `organizationId` (optional): Filter by organization
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20, max: 100)

### POST /api/devices/register
Register a new device.

**Request:**
```json
{
  "deviceId": "ESP32-ABC123",
  "organizationId": "uuid",
  "serialNumber": "SN123456",
  "macAddress": "AA:BB:CC:DD:EE:FF",
  "hardwareVersion": "v1.0",
  "metadata": "optional"
}
```

**Response:**
```json
{
  "id": "uuid",
  "deviceId": "ESP32-ABC123",
  "organizationId": "uuid",
  "deviceType": "Button",
  "status": "Inactive",
  "createdAt": "2025-11-02T15:43:20Z"
}
```

## Organizations

### GET /api/organizations
List all organizations.

**Query Parameters:**
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20, max: 100)

### POST /api/organizations
Create a new organization.

**Request:**
```json
{
  "name": "Test Organization",
  "slug": "test-org"
}
```

## Test User Credentials

For development/testing:
- **Email:** test@example.com
- **Password:** testpass123
- **Organization ID:** 85675cb9-61a3-460e-9609-8c3c7b9ae5cc

## Status Codes

- `200 OK`: Successful request
- `201 Created`: Resource created successfully
- `400 Bad Request`: Invalid request data
- `401 Unauthorized`: Missing or invalid authentication
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Server error

## Notes

**⚠️ SECURITY STATUS (Post-Audit 2025-11-03)**:
- ✅ **Authentication**: JWT-based auth with BCrypt password hashing (IMPLEMENTED)
- ⚠️ **Missing Critical Items**: Login brute force protection, audit logging, token blacklist
- ✅ **Password Hashing**: BCrypt implemented (not SHA256)
- ⚠️ **All Clear Endpoints**: Two-person approval NOT YET IMPLEMENTED (Phase 2)

**General Notes**:
- All timestamps are in UTC
- HTTPS redirect warning in development can be ignored (local dev environment)
- See `claudedocs/REVISED_ROADMAP.md` for production hardening plan
