# SafeSignal Cloud Backend Architecture

**Version**: 1.0.0
**Date**: 2025-11-02
**Status**: Design Phase

---

## Executive Summary

The SafeSignal cloud backend provides centralized management, analytics, and coordination for the distributed edge gateway network. It supports multi-tenancy, device provisioning, alert aggregation, and mobile app APIs.

### Core Responsibilities
1. **Multi-tenant Management** - Organizations, buildings, users, roles
2. **Device Lifecycle** - Registration, provisioning, firmware updates
3. **Alert Aggregation** - Historical queries, analytics, reporting
4. **Mobile App APIs** - Real-time monitoring, notifications, management
5. **Edge Coordination** - Configuration sync, policy updates

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Cloud Backend                             │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  API Gateway (Kong / Azure API Management)               │  │
│  │  - Authentication (JWT)                                   │  │
│  │  - Rate limiting                                          │  │
│  │  - Request routing                                        │  │
│  └──────────────────────────────────────────────────────────┘  │
│                            │                                     │
│        ┌───────────────────┼───────────────────┐                │
│        │                   │                   │                │
│  ┌─────▼─────┐      ┌─────▼─────┐      ┌─────▼─────┐          │
│  │  Tenant   │      │ Device    │      │  Alert    │          │
│  │  API      │      │ API       │      │  API      │          │
│  │  .NET 9   │      │ .NET 9    │      │ .NET 9    │          │
│  └───────────┘      └───────────┘      └───────────┘          │
│        │                   │                   │                │
│  ┌─────▼──────────────────▼───────────────────▼─────┐          │
│  │            PostgreSQL 16 (Primary)                │          │
│  │  - Tenants, Buildings, Devices, Users             │          │
│  │  - Alert history (hot: 90 days)                   │          │
│  └───────────────────────────────────────────────────┘          │
│                            │                                     │
│  ┌─────────────────────────▼─────────────────────────┐          │
│  │  Redis Cluster                                     │          │
│  │  - Session cache                                   │          │
│  │  - Device status cache                             │          │
│  │  - Rate limiting                                   │          │
│  └────────────────────────────────────────────────────┘          │
│                            │                                     │
│  ┌─────────────────────────▼─────────────────────────┐          │
│  │  Event Bus (Kafka / RabbitMQ)                     │          │
│  │  - Edge → Cloud alert streaming                   │          │
│  │  - Cloud → Edge config updates                    │          │
│  │  - Analytics pipeline                              │          │
│  └────────────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
┌───────▼────────┐  ┌──────▼──────┐   ┌────────▼────────┐
│  Edge Gateway  │  │ Mobile Apps │   │  Web Portal    │
│  (100s-1000s)  │  │ (iOS/Android│   │  (Management)  │
└────────────────┘  └─────────────┘   └─────────────────┘
```

---

## Technology Stack

### Core Services
| Component | Technology | Rationale |
|-----------|------------|-----------|
| **API Framework** | .NET 9 Web API | Type safety, performance, async/await |
| **Database** | PostgreSQL 16 | ACID compliance, JSON support, partitioning |
| **Cache** | Redis 7.x | Fast lookups, distributed caching |
| **Event Bus** | Kafka / RabbitMQ | Alert streaming, event sourcing |
| **Object Storage** | MinIO (S3-compatible) | Firmware binaries, audio files |
| **Authentication** | JWT + OAuth 2.0 | Stateless, mobile-friendly |

### Infrastructure
| Component | Technology | Rationale |
|-----------|------------|-----------|
| **Orchestration** | Docker Compose (Dev) → Kubernetes (Prod) | Container management |
| **API Gateway** | Kong / Traefik | Routing, rate limiting, auth |
| **Monitoring** | Prometheus + Grafana | Metrics, alerting |
| **Logging** | Loki / ELK | Centralized logs, search |
| **Tracing** | OpenTelemetry | Distributed tracing |

---

## Database Schema

### Multi-Tenancy Model

```sql
-- Tenant hierarchy: Organization → Sites → Buildings → Floors → Rooms

CREATE TABLE organizations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,  -- URL-friendly identifier
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    status VARCHAR(20) DEFAULT 'active',  -- active, suspended, deleted
    metadata JSONB  -- Flexible metadata storage
);

CREATE TABLE sites (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID REFERENCES organizations(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    address TEXT,
    latitude DECIMAL(10, 8),
    longitude DECIMAL(11, 8),
    timezone VARCHAR(50) DEFAULT 'UTC',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE buildings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    site_id UUID REFERENCES sites(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    address TEXT,
    floor_count INTEGER DEFAULT 1,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE floors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    building_id UUID REFERENCES buildings(id) ON DELETE CASCADE,
    floor_number INTEGER NOT NULL,
    name VARCHAR(100),  -- e.g., "Ground Floor", "Basement"
    UNIQUE(building_id, floor_number)
);

CREATE TABLE rooms (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    floor_id UUID REFERENCES floors(id) ON DELETE CASCADE,
    room_number VARCHAR(50) NOT NULL,
    name VARCHAR(200),
    capacity INTEGER,
    room_type VARCHAR(50),  -- classroom, office, lab, etc.
    UNIQUE(floor_id, room_number)
);
```

### Device Management

```sql
CREATE TABLE devices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id VARCHAR(100) UNIQUE NOT NULL,  -- ESP32 hardware ID
    organization_id UUID REFERENCES organizations(id) ON DELETE CASCADE,
    room_id UUID REFERENCES rooms(id) ON DELETE SET NULL,
    device_type VARCHAR(50) DEFAULT 'button',  -- button, gateway, sensor
    firmware_version VARCHAR(50),
    hardware_version VARCHAR(50),
    serial_number VARCHAR(100) UNIQUE,
    mac_address VARCHAR(17),
    provisioned_at TIMESTAMPTZ,
    last_seen_at TIMESTAMPTZ,
    status VARCHAR(20) DEFAULT 'inactive',  -- active, inactive, maintenance, offline
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    metadata JSONB
);

CREATE INDEX idx_devices_org ON devices(organization_id);
CREATE INDEX idx_devices_room ON devices(room_id);
CREATE INDEX idx_devices_status ON devices(status);
CREATE INDEX idx_devices_last_seen ON devices(last_seen_at DESC);

-- Device health metrics (time-series data)
CREATE TABLE device_metrics (
    id BIGSERIAL PRIMARY KEY,
    device_id UUID REFERENCES devices(id) ON DELETE CASCADE,
    timestamp TIMESTAMPTZ DEFAULT NOW(),
    rssi INTEGER,  -- WiFi signal strength
    battery_voltage DECIMAL(4, 2),
    uptime_seconds BIGINT,
    free_heap_bytes BIGINT,
    alert_count INTEGER DEFAULT 0,
    metadata JSONB
) PARTITION BY RANGE (timestamp);

-- Create partitions for device metrics (monthly)
-- Automated partition management via pg_partman
```

### Alert History

```sql
CREATE TABLE alert_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_id VARCHAR(100) UNIQUE NOT NULL,  -- From edge gateway
    organization_id UUID REFERENCES organizations(id) ON DELETE CASCADE,
    device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
    room_id UUID REFERENCES rooms(id) ON DELETE SET NULL,
    triggered_at TIMESTAMPTZ NOT NULL,
    resolved_at TIMESTAMPTZ,
    severity VARCHAR(20) NOT NULL,  -- low, medium, high, critical
    alert_type VARCHAR(50) NOT NULL,  -- fire, medical, lockdown, etc.
    status VARCHAR(20) NOT NULL,  -- new, acknowledged, resolved, cancelled
    source VARCHAR(50) DEFAULT 'button',  -- button, mobile, web, api
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
) PARTITION BY RANGE (triggered_at);

CREATE INDEX idx_alert_org_time ON alert_history(organization_id, triggered_at DESC);
CREATE INDEX idx_alert_device ON alert_history(device_id);
CREATE INDEX idx_alert_status ON alert_history(status);

-- Create partitions for alert history (monthly)
-- Retention policy: 90 days hot, 2 years warm (compressed), archive after
```

### User Management

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    phone VARCHAR(20),
    password_hash VARCHAR(255),  -- bcrypt hash
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    status VARCHAR(20) DEFAULT 'active',  -- active, suspended, deleted
    email_verified BOOLEAN DEFAULT FALSE,
    phone_verified BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    last_login_at TIMESTAMPTZ
);

CREATE TABLE user_organizations (
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    organization_id UUID REFERENCES organizations(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL,  -- admin, manager, operator, viewer
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (user_id, organization_id)
);

CREATE INDEX idx_user_org ON user_organizations(organization_id);

-- Role-based access control
CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    role VARCHAR(50) NOT NULL,
    resource VARCHAR(100) NOT NULL,  -- alerts, devices, users, etc.
    action VARCHAR(50) NOT NULL,  -- create, read, update, delete
    UNIQUE(role, resource, action)
);
```

---

## API Design

### RESTful Endpoints

#### Tenant Management API

```
POST   /api/v1/organizations              Create organization
GET    /api/v1/organizations              List organizations (paginated)
GET    /api/v1/organizations/{id}         Get organization details
PUT    /api/v1/organizations/{id}         Update organization
DELETE /api/v1/organizations/{id}         Delete organization (soft)

POST   /api/v1/organizations/{id}/sites   Create site
GET    /api/v1/organizations/{id}/sites   List sites
PUT    /api/v1/sites/{id}                 Update site
DELETE /api/v1/sites/{id}                 Delete site

POST   /api/v1/sites/{id}/buildings       Create building
GET    /api/v1/sites/{id}/buildings       List buildings
PUT    /api/v1/buildings/{id}             Update building
DELETE /api/v1/buildings/{id}             Delete building

POST   /api/v1/buildings/{id}/floors      Create floor
GET    /api/v1/buildings/{id}/floors      List floors

POST   /api/v1/floors/{id}/rooms          Create room
GET    /api/v1/floors/{id}/rooms          List rooms
PUT    /api/v1/rooms/{id}                 Update room
DELETE /api/v1/rooms/{id}                 Delete room
```

#### Device Management API

```
POST   /api/v1/devices                    Register device
GET    /api/v1/devices                    List devices (filtered by org)
GET    /api/v1/devices/{id}               Get device details
PUT    /api/v1/devices/{id}               Update device
DELETE /api/v1/devices/{id}               Decommission device

POST   /api/v1/devices/{id}/provision     Provision device (assign to room)
POST   /api/v1/devices/{id}/reset         Factory reset device
GET    /api/v1/devices/{id}/metrics       Get device health metrics
GET    /api/v1/devices/{id}/alerts        Get device alert history
```

#### Alert API

```
GET    /api/v1/alerts                     List alerts (filtered, paginated)
GET    /api/v1/alerts/{id}                Get alert details
PUT    /api/v1/alerts/{id}/acknowledge    Acknowledge alert
PUT    /api/v1/alerts/{id}/resolve        Resolve alert
POST   /api/v1/alerts/{id}/notes          Add note to alert

GET    /api/v1/alerts/statistics          Get alert statistics
GET    /api/v1/alerts/export              Export alerts (CSV/JSON)
```

#### User Management API

```
POST   /api/v1/auth/register              Register user
POST   /api/v1/auth/login                 Login (JWT token)
POST   /api/v1/auth/refresh               Refresh token
POST   /api/v1/auth/logout                Logout

GET    /api/v1/users                      List users (org-scoped)
GET    /api/v1/users/{id}                 Get user details
PUT    /api/v1/users/{id}                 Update user
DELETE /api/v1/users/{id}                 Delete user

POST   /api/v1/users/{id}/organizations   Add user to organization
DELETE /api/v1/users/{id}/organizations/{orgId}  Remove user from org
```

---

## Authentication & Authorization

### JWT Token Structure

```json
{
  "sub": "user-uuid",
  "email": "admin@school.edu",
  "organizations": [
    {
      "id": "org-uuid",
      "role": "admin"
    }
  ],
  "iat": 1730563845,
  "exp": 1730650245
}
```

### Role Hierarchy

| Role | Permissions | Use Case |
|------|-------------|----------|
| **Super Admin** | Full system access | Platform operators |
| **Org Admin** | Full org access | School administrators |
| **Manager** | Manage devices, view alerts | IT managers |
| **Operator** | Trigger/acknowledge alerts | Teachers, staff |
| **Viewer** | Read-only access | Auditors, parents |

---

## Data Flow

### Alert Flow: Edge → Cloud

```
1. ESP32 button press → Edge MQTT broker
2. Edge Policy Service → Alert FSM → SQLite (local)
3. Edge Policy Service → Kafka/HTTP → Cloud Alert API
4. Cloud Alert API → PostgreSQL (append-only)
5. Cloud Alert API → Redis (cache latest)
6. Cloud Alert API → WebSocket/SSE → Mobile apps
7. Cloud Alert API → SMS/Push notification services
```

### Device Registration Flow

```
1. Mobile app scans QR code → Cloud API
2. Cloud API → Generate provisioning token
3. Mobile app → Bluetooth → ESP32 (WiFi credentials + token)
4. ESP32 → Edge MQTT → Authentication request
5. Edge → Cloud API → Validate token → Issue certificate
6. Cloud API → PostgreSQL (device record)
7. Cloud API → Redis (device cache)
```

---

## Performance Requirements

| Metric | Target | Notes |
|--------|--------|-------|
| **API Latency (p95)** | <100ms | Read operations |
| **API Latency (p95)** | <500ms | Write operations |
| **Throughput** | 10,000 req/s | Per API instance |
| **Alert Ingestion** | 1,000 alerts/s | From edge gateways |
| **Database Queries** | <50ms | Hot path queries |
| **Cache Hit Rate** | >90% | Redis caching |

---

## Scalability Strategy

### Horizontal Scaling
- **API Services**: Stateless, scale with load balancer
- **PostgreSQL**: Read replicas for queries, write to primary
- **Redis**: Cluster mode for distributed cache
- **Kafka**: Partition by organization_id

### Data Partitioning
- **PostgreSQL**: Partition by time (alert_history, device_metrics)
- **Redis**: Shard by organization_id
- **Object Storage**: Bucket per organization

---

## Next Steps

1. ✅ Architecture design (this document)
2. ⏳ Create .NET 9 solution structure
3. ⏳ Set up PostgreSQL with Docker Compose
4. ⏳ Implement Tenant Management API
5. ⏳ Implement Device Management API
6. ⏳ Implement Alert History API
7. ⏳ Add authentication and authorization
8. ⏳ Set up Redis caching
9. ⏳ Integrate with edge gateway (event streaming)
10. ⏳ Add OpenAPI/Swagger documentation

---

**Document Version**: 1.0
**Last Updated**: 2025-11-02
**Owner**: Backend Engineering Lead
