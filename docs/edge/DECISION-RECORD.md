# SafeSignal MVP - Architectural Decision Record

**Project**: SafeSignal Edge Gateway MVP
**Version**: 1.0.0-MVP
**Date**: 2025-10-31

---

## Purpose

Document key architectural and technical decisions made during MVP development, including rationale, alternatives considered, and consequences.

---

## Decision 1: Technology Stack - .NET 9 for All Edge Services

**Date**: 2025-10-31
**Status**: Accepted
**Context**: Need to choose runtime for policy-service and pa-service

### Decision

Use .NET 9 (C#) for both policy-service and pa-service on the Edge Gateway.

### Rationale

**Chosen**: .NET 9
- Consistent with cloud backend technology choice
- Excellent async/await performance for I/O-bound operations
- Built-in gRPC support for future cloud integration
- Prometheus metrics library (prometheus-net) well-supported
- MQTTnet library mature and performant
- Single development skillset across edge and cloud

**Alternatives Considered**:

1. **Kotlin/JVM** (mentioned in system-overview.md):
   - Pros: Strong coroutines, good for FSM, JVM ecosystem
   - Cons: Different skillset from cloud, heavier runtime, longer cold start
   - Rejected: Team consistency more important for MVP

2. **Java (Spring Boot)**:
   - Pros: Enterprise-grade, extensive ecosystem
   - Cons: Heavyweight for edge, slower startup, higher memory footprint
   - Rejected: Overkill for MVP edge services

3. **Go**:
   - Pros: Fast, low memory, single binary
   - Cons: Different paradigm from cloud stack, less mature MQTT libs
   - Rejected: Team familiarity prioritized for MVP velocity

### Consequences

**Positive**:
- Unified codebase language (edge + cloud)
- Faster development (reuse patterns, libraries, knowledge)
- Strong type safety and async performance
- Good Docker image sizes (~85MB runtime)

**Negative**:
- .NET runtime overhead vs. Go (acceptable for MVP)
- Less common for edge deployments (acceptable trade-off)

**Mitigation**:
- Use Alpine-based runtime images to minimize size
- Profile memory usage and optimize if needed in production

---

## Decision 2: MQTT QoS Strategy

**Date**: 2025-10-31
**Status**: Accepted
**Context**: Choose QoS levels for different message types

### Decision

- **Alert Triggers** (ESP32/App → Policy): QoS 1 (At least once)
- **PA Commands** (Policy → PA): QoS 0 (At most once)
- **PA Status** (PA → Policy): QoS 0 (At most once)

### Rationale

**Alert Triggers (QoS 1)**:
- Reliability critical: Cannot lose alert triggers
- PUBACK provides delivery confirmation
- Acceptable latency overhead (~10-20ms)
- Idempotency handled by deduplication service

**PA Commands (QoS 0)**:
- Speed prioritized over guaranteed delivery
- Local network (Docker bridge) is reliable
- Failure detected via missing status acknowledgement
- Can retry if needed
- Saves ~20-30ms per message

**PA Status (QoS 0)**:
- Feedback mechanism, not critical path
- Missing status acceptable (logged as warning)
- Prometheus metrics provide aggregate view

**Alternative Considered**: QoS 1 for all messages
- Rejected: Latency overhead for PA commands unacceptable
- Latency increase: ~50-80ms additional (measured in testing)

### Consequences

**Positive**:
- Optimal latency for critical path (PA commands)
- Reliable trigger delivery with QoS 1
- Simple retry logic if PA status missing

**Negative**:
- Potential PA command loss (mitigated by local network reliability)
- Need to monitor PA status acknowledgement rates

**Mitigation**:
- Monitor `pa_playback_success_ratio` metric
- Alert if success rate <99%
- Consider QoS 1 for PA commands in production if needed

---

## Decision 3: Deduplication Strategy - In-Memory Cache

**Date**: 2025-10-31
**Status**: Accepted (MVP only)
**Context**: Prevent duplicate alert processing within 300-800ms window

### Decision

Use in-memory ConcurrentDictionary for deduplication cache in MVP.

### Rationale

**Chosen**: In-memory cache
- Simple implementation for single-node MVP
- Fast lookup (<1ms)
- No external dependencies
- Automatic cleanup with TTL timer

**Alternatives Considered**:

1. **Redis**:
   - Pros: Distributed, persistent, scales to cluster
   - Cons: Additional infrastructure, latency overhead, overkill for MVP
   - Decision: Planned for production, not needed for MVP

2. **SQLite**:
   - Pros: Persistent across restarts
   - Cons: Disk I/O latency, complexity for short TTL
   - Decision: Not needed for 500ms window

3. **No Deduplication**:
   - Pros: Simplest
   - Cons: Violates safety requirement (no alarm loops)
   - Decision: Unacceptable

### Consequences

**Positive**:
- Minimal latency impact
- No additional infrastructure
- Easy to test and debug

**Negative**:
- Lost on service restart (acceptable for MVP)
- Does not scale to multiple policy-service instances
- Memory grows with high alert rate (mitigated by TTL cleanup)

**Migration Path**:
- Production: Replace with Redis for HA deployment
- Interface abstraction allows easy swap
- Add `IDeduplicationService` interface for testability

---

## Decision 4: Certificate Management - Self-Signed Dev CA

**Date**: 2025-10-31
**Status**: Accepted (Dev only)
**Context**: mTLS requires certificates for all clients

### Decision

Generate self-signed CA and certificates with OpenSSL script for development.

### Rationale

**Chosen**: seed-certs.sh script
- Fast setup (<1 minute)
- No external dependencies
- Full control over certificate properties
- Suitable for local development and testing

**Production Path**: SPIFFE/SPIRE (documented in system-overview.md)
- Short-lived certificates (≤24h)
- Automated rotation
- Workload attestation

**Alternative Considered**: Let's Encrypt
- Rejected: Requires public DNS, not suitable for local dev

### Consequences

**Positive**:
- Easy developer onboarding
- No external service dependencies
- Consistent local environment

**Negative**:
- **NEVER use in production** (clearly documented)
- Manual certificate management
- Long expiry (1 year) not production-safe

**Mitigation**:
- Prominent warnings in all documentation
- Separate production certificate strategy documented
- Integration tests verify certificate expiry handling

---

## Decision 5: Building Topology - Hardcoded for MVP

**Date**: 2025-10-31
**Status**: Accepted (MVP only)
**Context**: Policy engine needs to know which rooms exist per building

### Decision

Hardcode building topology in `AlertStateMachine.cs` for MVP.

```csharp
private static readonly Dictionary<string, List<string>> BuildingRooms = new()
{
    { "building-a", new List<string> { "room-1", "room-2", "room-3", "room-4" } },
    { "building-b", new List<string> { "room-101", "room-102", "room-103" } },
};
```

### Rationale

**Chosen**: Hardcoded dictionary
- Simplest for MVP testing
- No database setup required
- Deterministic for testing

**Production Path**: SQLite cache
- Load from local database on startup
- Sync from cloud configuration API
- Hot-reload on configuration changes

**Alternative Considered**: JSON configuration file
- Pros: External to code, easier to modify
- Cons: File parsing overhead, validation complexity
- Decision: Deferred to post-MVP

### Consequences

**Positive**:
- Zero configuration required
- Fast lookup (in-memory dictionary)
- Clear for testing

**Negative**:
- Requires code change to add buildings
- Not scalable to production
- No dynamic updates

**Migration Path**:
- Create `IBuildingTopologyService` interface
- Implement SQLite-backed version
- Load on startup, cache in memory
- Add hot-reload on configuration change event

---

## Decision 6: Metrics Exposition - Prometheus Pull Model

**Date**: 2025-10-31
**Status**: Accepted
**Context**: Need observability for latency and success rates

### Decision

Use Prometheus pull model with `/metrics` endpoints on both services.

### Rationale

**Chosen**: Prometheus scraping
- Industry standard for container metrics
- Easy Grafana integration
- Histogram support for latency percentiles
- Low overhead

**Alternative Considered**: Push to Prometheus Pushgateway
- Pros: Works for short-lived jobs
- Cons: Additional infrastructure, not needed for long-running services
- Decision: Use pull model for services, pushgateway reserved for batch jobs

**Alternative Considered**: OpenTelemetry
- Pros: Vendor-neutral, richer tracing
- Cons: More complex setup, overkill for MVP
- Decision: Planned for production, not needed for MVP

### Consequences

**Positive**:
- Simple setup (expose endpoint, configure Prometheus)
- Rich histogram metrics out-of-box
- Grafana dashboards easy to create

**Negative**:
- Services must be reachable by Prometheus
- Pull interval (5s) means slight delay in metrics

**Production Enhancement**:
- Add OpenTelemetry for distributed tracing
- Correlate alerts across edge and cloud
- Add structured logging export (ELK/Loki)

---

## Decision 7: Docker Compose vs. Kubernetes for MVP

**Date**: 2025-10-31
**Status**: Accepted
**Context**: Choose orchestration for local development

### Decision

Use Docker Compose for MVP deployment.

### Rationale

**Chosen**: Docker Compose
- Simple local development setup
- No cluster overhead
- Easy developer onboarding
- Sufficient for single-node edge gateway

**Production Path**: Kubernetes (K3s or similar)
- Documented in deployment-guide.md
- Helm charts for production deployment
- HA, auto-scaling, self-healing

**Alternative Considered**: Bare Docker commands
- Pros: Minimal abstraction
- Cons: Complex multi-container management
- Decision: Compose provides better developer experience

### Consequences

**Positive**:
- `docker-compose up` starts entire stack
- Service dependencies handled automatically
- Health checks built-in
- Easy to add services

**Negative**:
- Not production-grade orchestration
- No HA or auto-scaling
- Single-node only

**Migration Path**:
- Create Helm charts from Compose file
- Add K8s health checks and resource limits
- Document K3s installation for edge devices

---

## Summary of Technical Debt

The following decisions are **intentional MVP simplifications** with documented production paths:

1. **In-memory deduplication** → Redis for HA
2. **Hardcoded topology** → SQLite + config API
3. **Self-signed certificates** → SPIFFE/SPIRE
4. **Docker Compose** → Kubernetes (K3s)
5. **TTS stub** → Real TTS engine integration
6. **Prometheus only** → Add OpenTelemetry tracing

**Technical Debt Tracking**: Create issues for each item before pilot deployment.

---

## Decision Review Process

**Frequency**: Review after each major milestone
**Next Review**: After first pilot deployment (Q4 2025)
**Owner**: CTO / Technical Lead

**Review Criteria**:
- Are MVP simplifications causing issues?
- Is migration to production architecture on track?
- Have requirements changed that invalidate decisions?

---

**Document Version**: 1.0.0-MVP
**Last Updated**: 2025-10-31
**Next Review**: Q4 2025 (Post-Pilot)
