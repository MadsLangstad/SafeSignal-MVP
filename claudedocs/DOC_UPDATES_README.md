# Documentation Update: README.md

**Target File**: `../safeSignal-doc/README.md`
**Purpose**: Update marketing claims to reflect MVP reality vs production roadmap

---

## Updates Required

### 1. Enterprise Security Section (Line 30-34)

**Current Text**:
```markdown
### 🔐 Enterprise Security
- **Zero Trust**: SPIFFE/SPIRE workload identity, mTLS everywhere
- **Hardware root of trust**: ATECC608A secure element in devices
- **Signed OTA updates**: Secure boot, dual-bank firmware, rollback safety
- **Immutable audit logs**: WORM storage for compliance and forensics
```

**Replace With**:
```markdown
### 🔐 Security

**MVP Security (v1.0 - Current)**:
- ✅ **mTLS authentication**: Device-to-broker mutual TLS
- ✅ **Multi-tenant isolation**: Strong organization-level data separation
- ✅ **BCrypt password hashing**: Industry-standard credential protection
- ✅ **JWT authentication**: Secure API access with token refresh
- ⚠️ **Development certificates**: Self-signed CA (pilot testing only)

**Production Security Roadmap (v2.0 - Phase 5)**:
- 🚧 **Zero Trust**: SPIFFE/SPIRE workload identity deployment (8 weeks)
- 🚧 **Hardware security**: ATECC608A secure element integration (Phase 1c)
- 🚧 **Signed OTA updates**: Secure boot, dual-bank firmware, rollback safety (Phase 1c)
- 🚧 **Immutable audit logs**: WORM storage for compliance and forensics (Phase 8)

**Security Timeline**: 6-8 weeks critical hardening (see `safeSignal-mvp/claudedocs/REVISED_ROADMAP.md`)
```

---

### 2. Performance Claims Section (Line 80-86)

**Current Text**:
```markdown
Panic Button/App → Edge MQTT Broker → Policy Validation → PA Activation → Cloud Escalation
     (≤100ms)            (≤200ms)          (≤300ms)          (≤400ms)        (async ≤5s)

**Local operations complete in ≤2 seconds** even during complete cloud/WAN outage.
```

**Replace With**:
```markdown
### ⚡ Performance

**MVP Measured Performance (v1.0 - Current)**:
```
Panic Button/App → Edge MQTT Broker → Policy Validation → PA Simulation
     (est 50ms)          (20-30ms)          (50-80ms)        (100-150ms)
```

- ✅ **Edge alert processing**: 50-80ms average (exceeds <100ms target)
- ✅ **Cloud API response**: <100ms average (measured)
- ✅ **Database queries**: <50ms (PostgreSQL with indexes)
- ⚠️ **End-to-end latency**: Not yet measured (ESP32 hardware + Cloud + Mobile)

**Production Performance Target (v2.0)**:
```
Panic Button → Edge MQTT → Policy Validation → PA Hardware → Cloud Escalation
   (≤100ms)      (≤200ms)      (≤300ms)        (≤400ms)      (async ≤5s)
```

**Local operations target: ≤2 seconds** (edge-only, no cloud dependency)

**Performance Testing**: Phase 4 (Week 4) - End-to-end latency verification
```

---

### 3. Add MVP Status Section

**Insert after main feature list, before Architecture Overview**:

```markdown
---

## 📊 Current Status

**Version**: 1.0.0-MVP (Pilot-Ready Foundation)
**Last Updated**: 2025-11-04
**Production Readiness**: ~70% (strong MVP, hardening required)

### ✅ Implemented (v1.0 - Current)

**Edge Infrastructure** (100%):
- ✅ EMQX MQTT broker with mTLS
- ✅ Policy Service (alert FSM, deduplication, source-room exclusion)
- ✅ PA Service (audio clip storage, playback simulation)
- ✅ SQLite persistence with full schema
- ✅ Prometheus + Grafana observability
- ✅ Real-time status dashboard

**Cloud Backend** (95% features, 70% production-ready):
- ✅ .NET 9 REST API with Clean Architecture
- ✅ PostgreSQL database (11 tables, multi-tenant)
- ✅ JWT authentication with BCrypt
- ✅ Organization/building/room/device management
- ✅ Alert triggering, history, acknowledge, resolve
- ⚠️ Security hardening needed (29h - Phase 1a)

**Mobile Application** (100% features, 72% production-ready):
- ✅ React Native + Expo (TypeScript strict mode)
- ✅ Offline-first SQLite with background sync
- ✅ Biometric + password authentication
- ✅ 4 alert modes (Silent, Audible, Lockdown, Evacuation)
- ✅ Alert history with filters and pagination
- ✅ Push notifications (Expo)
- ⚠️ Security fixes needed (13h - Phase 1b)

**ESP32 Firmware** (80% features, 55% production-ready):
- ✅ WiFi + MQTT client (mTLS configured)
- ✅ Button press detection (production-grade debouncing)
- ✅ Alert persistence with NVS queue (zero-loss)
- ✅ Watchdog timer with auto-reboot
- ✅ Time sync via SNTP
- 🔴 Critical security gaps (26h - Phase 1c):
  - Hardcoded WiFi credentials (extractable)
  - Embedded private keys (not ATECC)
  - Placeholder certificates (TLS won't work)
  - No secure boot or flash encryption

### 🚧 Production Roadmap (v2.0)

**Phase 1: Critical Security** (68 hours - Weeks 1-3):
- Backend security hardening (brute force, validation, audit logging)
- Mobile security fixes (error boundary, cert pinning, crash reporting)
- Firmware critical security (encrypted credentials, secure boot, real certs)

**Phase 2: All Clear Feature** (28 hours - Weeks 3-4):
- Two-person approval workflow (backend + mobile)
- Compliance audit trail

**Phase 3: API & Documentation** (4-8 hours - Week 4):
- API versioning (`/api/v1/...`)
- Documentation alignment

**Phase 4: Testing** (70 hours - Weeks 5-6):
- Backend test coverage (target ≥70%)
- Integration and E2E tests
- Performance testing

**Phase 5-6: Production Deployment** (30 hours - Weeks 6-8):
- Zero-Trust SPIFFE/SPIRE (deferred to post-MVP)
- CI/CD pipeline
- Monitoring and alerting
- App store submission

**Phase 7-8: Enterprise Features** (18+ weeks - Post-MVP):
- PA hardware integration
- Kafka/Service Bus messaging
- PSAP adapters
- WORM storage
- SMS/voice/email notifications
- Compliance certifications

**Full Roadmap**: See `safeSignal-mvp/claudedocs/REVISED_ROADMAP.md`

---

### 📈 MVP → Production Timeline

| Milestone | Timeline | Effort |
|-----------|----------|--------|
| **MVP Foundation** | ✅ Complete | ~50 hours invested |
| **Phase 0: Doc Alignment** | ✅ Week 0 (current) | 8-10 hours |
| **Phase 1: Critical Security** | Weeks 1-3 | 68 hours |
| **Phase 2: All Clear** | Weeks 3-4 | 28 hours |
| **Phase 3-4: Testing** | Weeks 4-6 | 74-78 hours |
| **Phase 5-6: Deployment** | Weeks 6-8 | 30 hours |
| **Production-Ready** | **Week 8** | **208-218 hours total** |

**Recommended Team**: 3-4 engineers for 6-8 week sprint

---
```

---

### 4. Resilience Section Update (Line 24-28)

**Current Text**:
```markdown
- **72+ hours** autonomous edge operation without WAN
- **UPS backup** (≥8 hours)
- **LTE/5G fallback** for connectivity
- **Mesh-capable** buttons for Wi-Fi jamming scenarios
```

**Replace With**:
```markdown
### 🛡️ Resilience & Reliability

**MVP Implementation (v1.0 - Current)**:
- ✅ **Edge-first architecture**: Alerts process locally without cloud dependency
- ✅ **SQLite persistence**: Survives service restarts
- ✅ **Alert queue with retry**: Zero-loss guarantee (NVS on ESP32)
- ✅ **Watchdog timer**: 30s timeout with auto-reboot
- ⚠️ **Single-region deployment**: No multi-region failover yet

**Production Roadmap (v2.0 - Phase 7)**:
- 🚧 **72+ hours autonomous operation**: UPS + battery backup (Phase 7)
- 🚧 **LTE/5G fallback**: For WAN connectivity loss (Phase 7)
- 🚧 **Mesh networking**: ESP32 WiFi mesh for jamming scenarios (Phase 7+)
- 🚧 **Multi-region**: Geographic redundancy (Phase 7)
```

---

## Summary of Changes

| Section | Change Type | Impact |
|---------|-------------|--------|
| Security Claims | Split MVP / Roadmap | Honest about development certs, SPIFFE deferred |
| Performance Claims | Add measured data | Shows actual 50-80ms edge performance, notes untested E2E |
| MVP Status Section | New comprehensive section | Clear current state + roadmap visibility |
| Resilience Claims | Split MVP / Roadmap | Shows edge-first reality, defers UPS/LTE/mesh |

**Key Message Shift**:
- **Before**: All features presented as complete
- **After**: Clear distinction between MVP (70% production-ready) and production roadmap

---

## Application Instructions

**Option A: Manual Update**
1. Open `../safeSignal-doc/README.md`
2. Update Security section (lines 30-34)
3. Update Performance section (lines 80-86)
4. Insert MVP Status section before Architecture Overview
5. Update Resilience section (lines 24-28)

**Option B: Automated (if git repo)**
```bash
cd ../safeSignal-doc

git checkout -b docs/readme-mvp-reality

# Apply changes manually
# Then commit

git add README.md
git commit -m "docs: Update README with honest MVP status vs production roadmap

Breaking changes in messaging:
- Security: Show MVP (mTLS, BCrypt, JWT) vs Production (SPIFFE, ATECC, WORM)
- Performance: Add measured MVP metrics (50-80ms edge), note E2E untested
- Add comprehensive MVP Status section with implementation percentages
- Resilience: Show edge-first reality, defer UPS/LTE/mesh to Phase 7
- Add 6-8 week production timeline with effort estimates

This aligns marketing claims with actual implementation reality
documented in safeSignal-mvp/DOCUMENTATION_GAPS.md"

git push origin docs/readme-mvp-reality
```

---

## Alternative: Dual README Approach

If you want to maintain aspirational vision for investors/marketing while documenting reality for technical audiences:

**Keep**: `README.md` (aspirational vision - current content)
**Add**: `MVP_README.md` (honest current state - proposed updates above)

Then add a note at top of main README:
```markdown
> **Note**: This README describes the complete production vision for SafeSignal.
> For current MVP implementation status, see [MVP_README.md](MVP_README.md) or
> the `safeSignal-mvp` repository documentation.
```

---

**Related Files**:
- `DOC_UPDATES_system-overview.md` - Architecture updates
- `DOC_UPDATES_api-documentation.md` - API endpoint updates
- `DOCUMENTATION_GAPS.md` - Complete gap analysis
- `../safeSignal-mvp/MVP_STATUS.md` - Detailed honest status report
- `../safeSignal-mvp/claudedocs/REVISED_ROADMAP.md` - 6-8 week production plan

**Next Review**: After Phase 1 completion (Week 3)
**Recommendation**: Apply updates BEFORE external stakeholder review or fundraising
