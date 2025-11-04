# Documentation Update: system-overview.md

**Target File**: `../safeSignal-doc/documentation/architecture/system-overview.md`
**Purpose**: Separate MVP reality from production roadmap features

---

## Updates Required

### 1. PA / Audio Service Section (Line 71-75)

**Current Text**:
```markdown
**PA / Audio Service**

-   Real-time audio to PA system.
-   Text-to-Speech (espeak-ng) + cached audio clips.
-   Source-room exclusion logic + feedback verification.
```

**Replace With**:
```markdown
**PA / Audio Service**

**MVP Implementation (v1.0 - Current)**:
-   Audio clip storage (MinIO with 8 pre-recorded TTS files)
-   PA command routing and confirmation tracking
-   Source-room exclusion logic
-   Playback simulation (suitable for pilot testing)

**Production Roadmap (v2.0 - Phase 6)**:
-   Real-time hardware PA system integration (Bogen/Valcom)
-   Live Text-to-Speech generation (Azure Cognitive Services or espeak-ng)
-   GPIO/relay feedback verification loop
-   Redundant audio paths with automatic failover
```

---

### 2. Event Bus Section (Line 87-90)

**Current Text**:
```markdown
**Event Bus (Kafka / Azure Service Bus)**

-   Distributed streaming with schema registry.
-   Exactly-once semantics ; dead-letter queues.
```

**Replace With**:
```markdown
**Event Bus**

**MVP Implementation (v1.0 - Current)**:
-   Direct PostgreSQL persistence
-   Synchronous request/response patterns
-   Suitable for single-region deployment

**Production Roadmap (v2.0 - Phase 7)**:
-   Kafka or Azure Service Bus for distributed streaming
-   Schema registry for message evolution
-   Exactly-once semantics with dead-letter queues
-   Multi-region event replication
```

---

### 3. Data Stores Section (Line 92-96)

**Current Text**:
```markdown
**Data Stores**

-   **PostgreSQL:** transactional + tenant configs.
-   **Redis:** caching / sessions / rate-limits.
-   **WORM S3 / MinIO:** immutable audit logs / recordings.
```

**Replace With**:
```markdown
**Data Stores**

**MVP Implementation (v1.0 - Current)**:
-   **PostgreSQL 16:** Transactional data + tenant configs + alert history
-   **Redis 7:** Infrastructure ready (caching planned for Phase 3)
-   **MinIO:** Audio clip storage (development mode)

**Production Roadmap (v2.0 - Phase 8)**:
-   **PostgreSQL:** Production-tuned with read replicas
-   **Redis:** Active caching, sessions, rate-limiting
-   **WORM Storage:** S3 Object Lock or MinIO WORM for immutable audit logs
-   **Time-series DB:** Metrics and telemetry (InfluxDB/TimescaleDB)
```

---

### 4. Phase 2 – Validation & Dedup Section (Line 130-135)

**Current Text**:
```markdown
### Phase 2 – Validation & Dedup (≤ 200 ms)

-   Verify ECDSA signature (ATECC608A).
-   Anti-replay: timestamp + nonce check.
-   Rate limiting per device / tenant.
-   Dedup using `{tenantId, buildingId, sourceRoomId, mode}`.
```

**Replace With**:
```markdown
### Phase 2 – Validation & Dedup (≤ 200 ms)

**MVP Implementation (v1.0 - Current)**:
-   mTLS device authentication via EMQX broker
-   Timestamp-based deduplication (300-800ms window)
-   Dedup key: `{tenantId, buildingId, sourceRoomId, mode}`
-   **Measured latency**: 50-80ms ✅ (exceeds <200ms target)

**Production Roadmap (v2.0 - Phase 1c)**:
-   ECDSA signature verification (ATECC608A secure element)
-   Anti-replay protection: nonce cache with Redis (30-second TTL)
-   Per-device rate limiting (max 1 alert per 5 minutes)
-   Signature verification latency budget: +20ms
```

---

## Summary of Changes

| Section | Change Type | Impact |
|---------|-------------|--------|
| PA / Audio Service | Split MVP / Roadmap | Clarifies simulation vs hardware |
| Event Bus | Split MVP / Roadmap | Honest about PostgreSQL-only MVP |
| Data Stores | Split MVP / Roadmap | Shows Redis infrastructure ready |
| Validation & Dedup | Split MVP / Roadmap | Critical security roadmap |

---

## Application Instructions

**Option A: Manual Update**
1. Open `../safeSignal-doc/documentation/architecture/system-overview.md`
2. Find each section by line number
3. Replace text with updated versions above

**Option B: Automated (if git repo)**
```bash
cd ../safeSignal-doc
# Create feature branch
git checkout -b docs/mvp-roadmap-separation

# Apply changes manually or with sed
# Then commit
git add documentation/architecture/system-overview.md
git commit -m "docs: Separate MVP implementation from production roadmap in system-overview

- Split PA/Audio service into v1.0 MVP (simulation) and v2.0 roadmap (hardware)
- Clarify Event Bus is deferred to Phase 7
- Document current data store reality (PostgreSQL + Redis infra)
- Security validation split: MVP (mTLS + timestamp) vs Production (ATECC + nonce)
- Add measured performance metrics where available"

git push origin docs/mvp-roadmap-separation
```

---

**Related Files**:
- `DOC_UPDATES_api-documentation.md` - API endpoint updates
- `DOC_UPDATES_README.md` - Marketing claims updates
- `DOCUMENTATION_GAPS.md` - Complete gap analysis

**Next Review**: After Phase 1 implementation (Week 3)
