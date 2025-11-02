# SafeSignal Complete System Architecture
## MVP → Production Architecture Overview

**Version**: 1.0.0
**Date**: 2025-11-02

---

## System Overview

SafeSignal is a distributed, safety-critical alerting system spanning three primary layers:
1. **Physical Layer** (ESP32 buttons + Edge Gateway)
2. **Cloud Layer** (Multi-tenant backend services)
3. **Client Layer** (Mobile apps + Admin dashboard)

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PHYSICAL LAYER (EDGE)                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                    │
│  │  ESP32-S3    │  │  ESP32-S3    │  │  ESP32-S3    │  Physical Buttons  │
│  │  Button #1   │  │  Button #2   │  │  Button #N   │  (Panic Buttons)   │
│  │ ATECC608A    │  │ ATECC608A    │  │ ATECC608A    │                    │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                    │
│         │ MQTT/mTLS       │ MQTT/mTLS       │ MQTT/mTLS                   │
│         │ (QoS 1)         │ (QoS 1)         │ (QoS 1)                     │
│         └─────────────────┴─────────────────┴────────┐                    │
│                                                       │                    │
│  ┌────────────────────────────────────────────────────▼──────────────────┐ │
│  │                    Edge Gateway (Raspberry Pi CM4)                    │ │
│  │                     Docker / K3s (Kubernetes)                         │ │
│  ├───────────────────────────────────────────────────────────────────────┤ │
│  │                                                                       │ │
│  │  ┌─────────────────────────────────────────────────────────────┐    │ │
│  │  │              EMQX MQTT Broker (v5.4)                        │    │ │
│  │  │  - mTLS authentication (client certificates)                │    │ │
│  │  │  - Tenant-based ACL (topic isolation)                       │    │ │
│  │  │  - QoS 0/1 support, no retained messages                    │    │ │
│  │  └────────┬────────────────────────────────────────────────────┘    │ │
│  │           │                                                          │ │
│  │  ┌────────▼──────────────────────┐  ┌─────────────────────────────┐ │ │
│  │  │  Policy Service (.NET 9)      │  │  PA Service (.NET 9)        │ │ │
│  │  │  - Alert State Machine (FSM)  │  │  - Audio playback mgmt      │ │ │
│  │  │  - Deduplication (300-800ms)  │  │  - MinIO storage client     │ │ │
│  │  │  - Source room exclusion      │  │  - Failover logic           │ │ │
│  │  │  - Topology management        │  │  - PA confirmation tracking │ │ │
│  │  │  - SQLite persistence         │  │  - Prometheus metrics       │ │ │
│  │  └────────┬──────────────────────┘  └─────────┬───────────────────┘ │ │
│  │           │                                    │                     │ │
│  │  ┌────────▼────────────────────────────────────▼───────────────────┐ │ │
│  │  │               PostgreSQL (Edge Cache)                           │ │ │
│  │  │  - Alerts, Topology, Devices, PA Confirmations                  │ │ │
│  │  │  - Synced from cloud via gRPC                                   │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │                                                                       │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │ │
│  │  │               MinIO (S3-compatible storage)                     │ │ │
│  │  │  - Audio clips (TTS, pre-recorded messages)                     │ │ │
│  │  │  - WORM (Write-Once-Read-Many) for audit logs                   │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │                                                                       │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │ │
│  │  │          Observability Stack (Prometheus + Grafana)             │ │ │
│  │  │  - Metrics: Alert latency, PA success rate, MQTT throughput     │ │ │
│  │  │  - Dashboards: Edge health, alert history, device status        │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │                                                                       │ │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │ │
│  │  │              Security: SPIRE Agent + TPM 2.0                    │ │ │
│  │  │  - Workload attestation for services                            │ │ │
│  │  │  - Certificate rotation (24h TTL)                               │ │ │
│  │  │  - TPM-based edge gateway attestation                           │ │ │
│  │  └─────────────────────────────────────────────────────────────────┘ │ │
│  │                                                                       │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                     ▲                                       │
│                                     │ Network: Wi-Fi / Ethernet             │
│                                     │ UPS: 8-hour battery backup            │
│                                     │ Hardware: TPM 2.0, Secure Boot        │
└─────────────────────────────────────┼───────────────────────────────────────┘
                                      │
                                      │ Internet / WAN
                                      │ gRPC bidirectional stream
                                      │ Kafka events (alerts, telemetry)
                                      │ mTLS (SPIFFE SVIDs)
                                      │
┌─────────────────────────────────────▼───────────────────────────────────────┐
│                              CLOUD LAYER                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │              Cloud Infrastructure (Azure / AWS / GCP)               │   │
│  │                   Kubernetes (AKS / EKS / GKE)                      │   │
│  ├─────────────────────────────────────────────────────────────────────┤   │
│  │                                                                     │   │
│  │  ┌───────────────────────────────────────────────────────────────┐ │   │
│  │  │           API Gateway / Load Balancer                         │ │   │
│  │  │  - TLS termination                                            │ │   │
│  │  │  - Rate limiting (per tenant)                                 │ │   │
│  │  │  - Authentication (JWT validation)                            │ │   │
│  │  └────────┬──────────────────────────────────────────────────────┘ │   │
│  │           │                                                        │   │
│  │  ┌────────▼──────────────────────────────────────────────────────┐ │   │
│  │  │        SafeSignal Cloud API (.NET 9)                          │ │   │
│  │  │  - REST APIs: Tenant, Building, Room, Device, User, Alert    │ │   │
│  │  │  - gRPC Services: EdgeConfig, EdgeTelemetry, AlertEscalation │ │   │
│  │  │  - Authentication: ASP.NET Identity, JWT tokens               │ │   │
│  │  │  - Multi-tenancy: Tenant isolation at query level            │ │   │
│  │  └────────┬──────────────────────────────────────────────────────┘ │   │
│  │           │                                                        │   │
│  │  ┌────────▼──────────────────────────────────────────────────────┐ │   │
│  │  │         PostgreSQL (Cloud - Authoritative)                    │ │   │
│  │  │  - Tenants, Users, Buildings, Rooms, Devices                 │ │   │
│  │  │  - AlertHistory (cloud-synced alerts)                         │ │   │
│  │  │  - Configurations, AuditLogs (immutable)                      │ │   │
│  │  │  - Read replicas for scaling                                 │ │   │
│  │  │  - Point-in-time recovery (PITR)                             │ │   │
│  │  └───────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │                Redis Cluster                                    │ │   │
│  │  │  - Distributed deduplication cache                              │ │   │
│  │  │  - Session management                                           │ │   │
│  │  │  - Rate limiting state                                          │ │   │
│  │  │  - Configuration cache                                          │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │         Kafka / Azure Service Bus (Event Streaming)             │ │   │
│  │  │  Topics:                                                        │ │   │
│  │  │    - alerts (AlertTriggered, AlertEscalated, AlertCleared)     │ │   │
│  │  │    - telemetry (EdgeMetrics, DeviceStatus)                     │ │   │
│  │  │    - notifications (PushSent, SmsSent, VoiceCalled)            │ │   │
│  │  │    - audit (ConfigChanged, UserAction, SecurityEvent)          │ │   │
│  │  └─────────┬───────────────────────────────────────────────────────┘ │   │
│  │            │                                                         │   │
│  │  ┌─────────▼───────────────────────────────────────────────────────┐ │   │
│  │  │          Notification Service (.NET 9)                          │ │   │
│  │  │  - Push notifications (APNs, FCM)                               │ │   │
│  │  │  - SMS (Twilio / Azure Communication Services)                  │ │   │
│  │  │  - Voice calls (Twilio / Azure Communication Services)          │ │   │
│  │  │  - Template management (multi-language)                         │ │   │
│  │  │  - Audit logging (all messages)                                 │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │          RegionAdapter Service (.NET 9)                         │ │   │
│  │  │  - Regional compliance rules (911/112 auto-dial restrictions)   │ │   │
│  │  │  - Legal validation per tenant region                           │ │   │
│  │  │  - Emergency contact management                                 │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │               MinIO (Cloud Object Storage)                      │ │   │
│  │  │  - Immutable WORM logs (7-year retention)                       │ │   │
│  │  │  - Audio library (TTS, pre-recorded messages)                   │ │   │
│  │  │  - Versioning enabled                                           │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │     Observability Stack (Cloud)                                 │ │   │
│  │  │  - OpenTelemetry Collector (traces, metrics, logs)              │ │   │
│  │  │  - Jaeger / Grafana Tempo (distributed tracing)                 │ │   │
│  │  │  - ELK / Loki (centralized logging)                             │ │   │
│  │  │  - Prometheus + Grafana (metrics + dashboards)                  │ │   │
│  │  │  - PagerDuty / Opsgenie (alerting)                              │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │        Security Infrastructure                                  │ │   │
│  │  │  - SPIRE Server (SPIFFE identity issuer)                        │ │   │
│  │  │  - Vault with HSM (secrets management)                          │ │   │
│  │  │  - Certificate Authority (automated rotation)                   │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │           Admin Dashboard (Blazor / React)                      │ │   │
│  │  │  - Tenant management                                            │ │   │
│  │  │  - Building/room topology editor                                │ │   │
│  │  │  - Device registration and status                               │ │   │
│  │  │  - Alert history and analytics                                  │ │   │
│  │  │  - Drill scheduling and reporting                               │ │   │
│  │  │  - User and role management                                     │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                                                     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │
                                      │ HTTPS / gRPC
                                      │ WebSocket (push notifications)
                                      │ mTLS (client auth)
                                      │
┌─────────────────────────────────────▼───────────────────────────────────────┐
│                             CLIENT LAYER                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌───────────────────────────────┐    ┌───────────────────────────────┐   │
│  │   Mobile App (iOS)            │    │   Mobile App (Android)        │   │
│  │   React Native + Expo         │    │   React Native + Expo         │   │
│  ├───────────────────────────────┤    ├───────────────────────────────┤   │
│  │  - Emergency button (trigger) │    │  - Emergency button (trigger) │   │
│  │  - Push notifications (APNs)  │    │  - Push notifications (FCM)   │   │
│  │  - Alert history              │    │  - Alert history              │   │
│  │  - Building/room selection    │    │  - Building/room selection    │   │
│  │  - Offline-first (SQLite)     │    │  - Offline-first (SQLite)     │   │
│  │  - Background sync            │    │  - Background sync            │   │
│  │  - Biometric auth (Face ID)   │    │  - Biometric auth (Finger)    │   │
│  └───────────────────────────────┘    └───────────────────────────────┘   │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                    SMS / Voice (Staff Devices)                        │ │
│  │  - Fallback notifications when app not available                     │ │
│  │  - Emergency escalation calls                                         │ │
│  │  - Two-way confirmation (reply to acknowledge)                        │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow: Alert Lifecycle

### 1. Alert Trigger (Local Building)

```
┌─────────────┐
│  Teacher    │ Presses panic button in Room 1
│  Room 1     │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────────────────────────────┐
│  ESP32-S3 Button                                            │
│  - Device ID: esp32-room-1                                  │
│  - Tenant ID: school-a                                      │
│  - Building ID: main-building                               │
│  - Source Room: room-1                                      │
└──────┬──────────────────────────────────────────────────────┘
       │ MQTT Publish (QoS 1, mTLS)
       │ Topic: safesignal/school-a/main-building/alerts/trigger
       │ Payload: { alertId, mode: AUDIBLE, sourceRoom: room-1, ... }
       ▼
┌─────────────────────────────────────────────────────────────┐
│  EMQX Broker (Edge)                                         │
│  - Authenticates ESP32 via client certificate (ATECC608A)   │
│  - Validates topic ACL (tenant isolation)                   │
│  - Routes to policy-service subscriber                      │
└──────┬──────────────────────────────────────────────────────┘
       │ MQTT Delivery
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Policy Service (Edge)                                      │
│  1. Receives alert trigger                                  │
│  2. Deduplication check (300-800ms window)                  │
│     → Check in-memory cache (dev) or Redis (prod)           │
│     → If duplicate: Drop silently                           │
│  3. Alert State Machine (FSM)                               │
│     → Validate alert mode (SILENT/AUDIBLE/LOCKDOWN/EVAC)    │
│     → Load building topology from SQLite                    │
│     → Identify target rooms (all rooms in building)         │
│     → **EXCLUDE source room** (room-1) - CRITICAL           │
│     → Generate PA commands for each target room             │
│  4. Persist alert to SQLite                                 │
│  5. Publish PA commands to MQTT                             │
└──────┬──────────────────────────────────────────────────────┘
       │ MQTT Publish (QoS 0, low-latency)
       │ Topic: safesignal/school-a/main-building/pa/command
       │ Payloads: [
       │   { alertId, targetRoom: room-2, audioClipId, ... },
       │   { alertId, targetRoom: room-3, audioClipId, ... },
       │   { alertId, targetRoom: room-4, audioClipId, ... }
       │ ]
       ▼
┌─────────────────────────────────────────────────────────────┐
│  PA Service (Edge)                                          │
│  1. Receives PA command for each target room                │
│  2. Fetch audio clip from MinIO (TTS or pre-recorded)       │
│  3. Play audio on PA system (simulated in MVP)              │
│  4. Publish confirmation to MQTT                            │
│  5. Log playback result to database                         │
└──────┬──────────────────────────────────────────────────────┘
       │ MQTT Publish (QoS 0)
       │ Topic: safesignal/school-a/main-building/pa/status
       │ Payload: { alertId, targetRoom, success: true, latency: 45ms }
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Policy Service (Edge)                                      │
│  - Receives PA confirmation                                 │
│  - Updates alert status: COMPLETED                          │
│  - Logs to database                                         │
└──────┬──────────────────────────────────────────────────────┘
       │
       ▼
   Alert complete locally (edge autonomous operation)
```

**Latency Target**: <100ms (button press → PA playback)
**Current MVP**: ~50-80ms average

---

### 2. Alert Escalation (Cloud Integration)

```
┌─────────────────────────────────────────────────────────────┐
│  Policy Service (Edge)                                      │
│  - Detects escalation condition:                            │
│    • Alert not cleared within 5 minutes                     │
│    • Manual escalation triggered                            │
│    • Building-wide evacuation mode                          │
└──────┬──────────────────────────────────────────────────────┘
       │ Kafka Event (if cloud connected)
       │ Topic: safesignal.alerts
       │ Event: AlertEscalated { alertId, tenantId, buildingId, ... }
       │
       │ OR gRPC Call (if Kafka unavailable)
       │ Service: AlertEscalationService.EscalateAlert(...)
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Cloud Backend (SafeSignal.Cloud.Api)                       │
│  1. Receives escalation event from Kafka or gRPC            │
│  2. Enriches with tenant context (emergency contacts)       │
│  3. Publishes to notification topic                         │
└──────┬──────────────────────────────────────────────────────┘
       │ Kafka Event
       │ Topic: safesignal.notifications
       │ Event: NotificationRequested { type: ESCALATION, ... }
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Notification Service (Cloud)                               │
│  1. Receives notification request                           │
│  2. Load recipient preferences (push > SMS > voice)         │
│  3. Check RegionAdapter for compliance                      │
│  4. Send notifications in priority order:                   │
│     a) Push notifications (APNs + FCM) to mobile apps       │
│     b) SMS to staff phones (Twilio / Azure Comm)            │
│     c) Voice calls to emergency contacts                    │
│  5. Log all notifications to audit trail (MinIO WORM)       │
└──────┬──────────────────────────────────────────────────────┘
       │
       ▼
┌───────────────────────────────┐  ┌──────────────────────────┐
│  Mobile App (Staff)           │  │  SMS / Voice (Fallback)  │
│  - Receives push notification │  │  - Twilio / Azure Comm   │
│  - Alert banner + sound       │  │  - Delivery confirmation │
│  - Acknowledge button         │  │  - Reply-to-confirm      │
└───────────────────────────────┘  └──────────────────────────┘
```

**Escalation Latency Target**: <2 seconds (edge → cloud → mobile)

---

### 3. Configuration Sync (Cloud → Edge)

```
┌─────────────────────────────────────────────────────────────┐
│  Admin Dashboard (Cloud)                                    │
│  - Administrator updates building topology:                 │
│    • Add new room: room-5                                   │
│    • Update room capacity                                   │
│  - Saves changes to PostgreSQL                              │
└──────┬──────────────────────────────────────────────────────┘
       │ Database Write
       │ PostgreSQL: UPDATE rooms SET ...
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Cloud API (SafeSignal.Cloud.Api)                           │
│  - Triggers configuration change event                      │
│  - Publishes to Kafka: ConfigChanged { tenantId, ... }      │
└──────┬──────────────────────────────────────────────────────┘
       │ Kafka Event
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Edge Config Service (Cloud)                                │
│  - Consumes ConfigChanged event                             │
│  - Identifies affected edge gateways (by tenant/building)   │
│  - Pushes configuration via gRPC bidirectional stream       │
└──────┬──────────────────────────────────────────────────────┘
       │ gRPC Stream
       │ EdgeConfigService.StreamConfiguration(...)
       │ Message: ConfigurationUpdate { buildings, rooms, ... }
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Policy Service (Edge)                                      │
│  - Receives configuration update via gRPC                   │
│  - Validates configuration schema                           │
│  - Writes to local SQLite (edge cache)                      │
│  - Reloads in-memory topology                               │
│  - Sends acknowledgement to cloud                           │
└─────────────────────────────────────────────────────────────┘
```

**Sync Latency Target**: <5 seconds (admin change → edge updated)

---

### 4. Telemetry Upload (Edge → Cloud)

```
┌─────────────────────────────────────────────────────────────┐
│  Edge Services (Policy, PA, MQTT)                           │
│  - Collect telemetry data:                                  │
│    • Alert metrics (count, latency, success rate)           │
│    • Device status (battery, RSSI, last seen)               │
│    • System health (CPU, memory, disk)                      │
│  - Batch every 30 seconds                                   │
└──────┬──────────────────────────────────────────────────────┘
       │ gRPC Stream
       │ EdgeTelemetryService.ReportTelemetry(stream)
       │ Messages: [
       │   TelemetryData { type: ALERT_METRICS, ... },
       │   TelemetryData { type: DEVICE_STATUS, ... },
       │   TelemetryData { type: SYSTEM_HEALTH, ... }
       │ ]
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Cloud API (SafeSignal.Cloud.Api)                           │
│  - Receives telemetry stream                                │
│  - Publishes to Kafka: Telemetry { tenantId, data, ... }    │
└──────┬──────────────────────────────────────────────────────┘
       │ Kafka Event
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Telemetry Processor (Cloud)                                │
│  - Aggregates metrics                                       │
│  - Stores in PostgreSQL (time-series optimized)             │
│  - Updates device status                                    │
│  - Triggers alerts if anomalies detected                    │
└─────────────────────────────────────────────────────────────┘
```

**Telemetry Frequency**: Every 30 seconds (batched)
**Network Efficiency**: ~10KB per batch

---

## Security Architecture

### Certificate Management (SPIFFE/SPIRE)

```
┌─────────────────────────────────────────────────────────────────┐
│                    SPIRE Server (Cloud)                         │
│  - Vault backend for key storage (HSM-backed)                   │
│  - Issues SVIDs (SPIFFE Verifiable Identity Documents)          │
│  - TTL: 24 hours (auto-rotate every 12 hours)                   │
│  - Trust domain: spiffe://safesignal.io                         │
└──────┬──────────────────────────────────────────────────────────┘
       │
       ├─────────────────────────────────────────────┐
       │                                             │
       ▼                                             ▼
┌─────────────────────────────┐       ┌─────────────────────────────┐
│  SPIRE Agent (Cloud)        │       │  SPIRE Agent (Edge)         │
│  - Runs on K8s nodes        │       │  - Runs on edge gateway     │
│  - Node attestation         │       │  - TPM 2.0 attestation      │
│  - Workload API for pods    │       │  - Workload API for services│
└──────┬──────────────────────┘       └──────┬──────────────────────┘
       │                                      │
       ▼                                      ▼
┌─────────────────────────────┐       ┌─────────────────────────────┐
│  Cloud Services             │       │  Edge Services              │
│  - API, Notification, etc.  │       │  - Policy, PA, etc.         │
│  - SPIFFE ID example:       │       │  - SPIFFE ID example:       │
│    spiffe://safesignal.io/  │       │    spiffe://safesignal.io/  │
│    cloud/api                │       │    edge/policy-service      │
└─────────────────────────────┘       └─────────────────────────────┘
                                               │
                                               ▼
                                      ┌─────────────────────────────┐
                                      │  ESP32 Devices              │
                                      │  - ATECC608A secure element │
                                      │  - Device certificate       │
                                      │  - SPIFFE ID:               │
                                      │    spiffe://safesignal.io/  │
                                      │    device/{deviceId}        │
                                      └─────────────────────────────┘
```

### Communication Security

| Connection | Protocol | Authentication | Encryption |
|------------|----------|----------------|------------|
| ESP32 → EMQX | MQTT/TLS | mTLS (ATECC608A cert) | TLS 1.3 |
| Policy ↔ EMQX | MQTT/TLS | mTLS (SPIRE SVID) | TLS 1.3 |
| PA ↔ EMQX | MQTT/TLS | mTLS (SPIRE SVID) | TLS 1.3 |
| Edge → Cloud | gRPC | mTLS (SPIRE SVID) | TLS 1.3 |
| Mobile → Cloud | HTTPS | JWT + biometric | TLS 1.3 |
| Admin → Cloud | HTTPS | JWT + RBAC | TLS 1.3 |

---

## Observability Architecture

### Distributed Tracing (OpenTelemetry)

```
Alert Trigger → [Trace ID: a1b2c3d4]

ESP32 Button (span: button-press)
  └─► EMQX Broker (span: mqtt-publish)
      └─► Policy Service (span: alert-fsm)
          ├─► Deduplication Check (span: dedup-check)
          ├─► Load Topology (span: db-query)
          ├─► Generate PA Commands (span: fsm-logic)
          └─► Publish to PA Topic (span: mqtt-publish)
              └─► PA Service (span: audio-playback)
                  ├─► Fetch Audio (span: minio-fetch)
                  ├─► Play Audio (span: pa-play)
                  └─► Publish Status (span: mqtt-publish)
                      └─► Policy Service (span: status-update)

End-to-End Latency: 78ms
P95 Latency by Span:
  - button-press: 5ms
  - mqtt-publish (trigger): 8ms
  - alert-fsm: 25ms
    - dedup-check: 2ms
    - db-query: 5ms
    - fsm-logic: 10ms
    - mqtt-publish (pa-cmd): 8ms
  - audio-playback: 40ms
    - minio-fetch: 10ms
    - pa-play: 25ms
    - mqtt-publish (status): 5ms
```

### Metrics (Prometheus)

**Edge Metrics**:
- `alerts_processed_total{tenant, building, mode, status}` (counter)
- `alert_processing_duration_seconds{tenant, building}` (histogram)
- `pa_commands_sent_total{tenant, building, room}` (counter)
- `pa_playback_success_rate{tenant, building}` (gauge)
- `mqtt_messages_received_total{topic}` (counter)
- `deduplication_hits_total{tenant}` (counter)

**Cloud Metrics**:
- `api_requests_total{endpoint, method, status}` (counter)
- `api_request_duration_seconds{endpoint}` (histogram)
- `notifications_sent_total{type, status}` (counter)
- `edge_gateways_connected{tenant}` (gauge)
- `database_query_duration_seconds{query_type}` (histogram)

---

## Deployment Architecture

### Edge Deployment (Per Building)

**Hardware**:
- Raspberry Pi CM4 or mini-PC (Intel NUC)
- 8GB RAM, 64GB storage (minimum)
- TPM 2.0 module
- UPS (uninterruptible power supply, 8-hour battery)
- Ethernet + Wi-Fi (redundant connectivity)

**Software Stack**:
- OS: Ubuntu 22.04 LTS or Debian 12
- Container Runtime: Docker or K3s (lightweight Kubernetes)
- Services: EMQX, Policy, PA, PostgreSQL, MinIO, Prometheus, Grafana
- Security: SPIRE Agent, firewall (UFW), fail2ban

### Cloud Deployment (Multi-Tenant)

**Infrastructure**:
- Kubernetes cluster (Azure AKS, AWS EKS, or GCP GKE)
- Node pools: System (3 nodes), Application (auto-scaling 3-10 nodes)
- PostgreSQL: Managed service (Azure Database, AWS RDS)
- Redis: Managed cluster (Azure Cache, AWS ElastiCache)
- Kafka: Managed service (Confluent Cloud, Azure Event Hubs)

**Services**:
- API: 3+ replicas (auto-scaling)
- Notification Service: 2+ replicas
- RegionAdapter: 2+ replicas
- SPIRE Server: 2 replicas (HA)
- Vault: 3 replicas (Raft consensus)

---

## Network Resilience

### Edge Autonomy Strategy

**When Cloud is Unreachable**:
- ✅ Local alerts continue working (100% autonomy)
- ✅ PA playback from local MinIO cache
- ✅ Database writes to local SQLite
- ✅ Telemetry queued locally (max 10,000 events)
- ✅ Configuration served from local cache
- ❌ Alert escalation delayed (SMS, voice, external push)
- ❌ Configuration changes not received
- ❌ Admin dashboard not updated

**Reconnection Behavior**:
1. Detect cloud availability (gRPC health check)
2. Replay queued telemetry events (oldest first)
3. Sync configuration (pull latest from cloud)
4. Resume normal operation

**Resilience Target**: 72 hours autonomous operation

---

## Compliance and Certification

### ISO 27001 (Information Security)
- **Scope**: Complete system (edge + cloud + mobile)
- **Controls**: 114 controls across 14 domains
- **Evidence**: Audit logs, risk assessments, security policies
- **Audit**: Annual external audit

### ISO 22301 (Business Continuity)
- **RTO** (Recovery Time Objective): 4 hours
- **RPO** (Recovery Point Objective): 1 hour
- **DRP** (Disaster Recovery Plan): Quarterly testing
- **Backup**: Daily backups, off-site replication

### EN 50136 (Alarm Transmission Systems)
- **Security Grade**: Grade 2 or 3
- **Transmission Latency**: <5 seconds
- **Fault Tolerance**: Redundant paths (Ethernet + Wi-Fi + Cellular)
- **Monitoring**: Continuous supervision with heartbeats

### CE/RED/FCC (Hardware)
- **CE** (Europe): Electromagnetic compatibility, safety
- **RED** (Europe): Radio equipment directive (2.4GHz Wi-Fi)
- **FCC** (US): Part 15 (radio frequency devices)
- **Testing**: EMC, radio, safety (accredited test lab)

### GDPR (Data Protection)
- **Data Minimization**: Only collect necessary data
- **Consent**: Explicit user consent for personal data
- **Right to Access**: Users can download their data
- **Right to Erasure**: Users can delete their account
- **Data Breach Notification**: Within 72 hours
- **DPA** (Data Processing Agreements): With all sub-processors

---

## Summary

SafeSignal architecture is designed for:
- ✅ **Safety-critical reliability**: 99.9% uptime, <100ms latency
- ✅ **Edge autonomy**: Operates without cloud for 72+ hours
- ✅ **Multi-tenancy**: Strict data isolation at all layers
- ✅ **Security by design**: mTLS, SPIFFE/SPIRE, HSM-backed secrets
- ✅ **Compliance-ready**: ISO 27001/22301, EN 50136, GDPR, CE/FCC
- ✅ **Scalability**: Horizontal scaling in cloud, edge gateway per building
- ✅ **Observability**: End-to-end tracing, centralized logging, metrics

**Current MVP**: Strong edge foundation (30-35% complete)
**Next Steps**: ESP32 firmware + cloud backend + mobile app (Phases 1-4)
**Production Timeline**: 8-10 months with full team

---

**For Implementation Details**: See [IMPLEMENTATION-PLAN.md](./IMPLEMENTATION-PLAN.md)
**For Quick Reference**: See [IMPLEMENTATION-SUMMARY.md](./IMPLEMENTATION-SUMMARY.md)
