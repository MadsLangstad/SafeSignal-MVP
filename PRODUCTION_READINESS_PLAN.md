# Production Readiness Plan

**Date**: 2025-11-04
**Current Status**: 70% Production-Ready MVP Foundation
**Timeline to Production**: 6-8 weeks (43 critical hours + testing)

---

## Executive Summary

SafeSignal MVP has **strong architectural foundations** but requires **critical security hardening** before production deployment. This plan provides the fastest path to unblock compliance and deploy safely.

**Key Documents**:
- `SECURITY_CRITICAL_GAPS.md` - 8 P0 blockers (43h mitigation)
- `DOCUMENTATION_GAPS.md` - External doc vs reality analysis
- `MVP_STATUS.md` - Honest implementation status
- `claudedocs/REVISED_ROADMAP.md` - Detailed 6-8 week plan

---

## Phase 0: Documentation Alignment ✅ COMPLETE

**Deliverables**:
- ✅ Gap analysis between safeSignal-doc and MVP reality
- ✅ Ready-to-apply doc updates (DOC_UPDATES_*.md)
- ✅ Security critical gaps identified
- ✅ Cleanup of redundant documentation

**Next Action**: Apply DOC_UPDATES to `../safeSignal-doc` repository

```bash
cd ../safeSignal-doc
git checkout -b docs/mvp-roadmap-separation

# Apply changes from:
# - DOC_UPDATES_system-overview.md
# - DOC_UPDATES_api-documentation.md
# - DOC_UPDATES_README.md

git commit -m "docs: Separate MVP implementation from production roadmap"
git push origin docs/mvp-roadmap-separation
```

---

## Week 1: Broker & Object Store Security (7 hours)

**Checkpoint**: EMQX + MinIO transport hardening

### Day 1-2: EMQX Security (4h)
**File**: `edge/docker-compose.yml`

```yaml
# REMOVE unauthenticated port
ports:
  # - "1883:1883"  # DELETE
  - "8883:8883"    # mTLS only

environment:
  EMQX_ALLOW_ANONYMOUS: "false"  # ✅ Critical

volumes:
  - ./emqx/acl.conf:/opt/emqx/etc/acl.conf
```

**Create** `edge/emqx/acl.conf`:
```
{deny, all}.
{allow, {user, "device-{tenantId}-{deviceId}"}, publish, ["alerts/{tenantId}/{buildingId}/#"]}.
{allow, {user, "policy-service"}, subscribe, ["alerts/#"]}.
```

**Verify**:
```bash
# Anonymous should FAIL
mosquitto_pub -h localhost -p 1883 -t "test" -m "fail"

# Cross-tenant publish should FAIL
mosquitto_pub -h localhost -p 8883 \
  --cert certs/devices/tenant-a-device-1.crt \
  -t "alerts/tenant-b/building-1/trigger" -m "test"
```

### Day 3: MinIO TLS (3h)
**File**: `edge/docker-compose.yml`

```yaml
minio:
  environment:
    MINIO_ROOT_USER: ${MINIO_ROOT_USER}
    MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
  volumes:
    - ./certs/minio/public.crt:/root/.minio/certs/public.crt
    - ./certs/minio/private.key:/root/.minio/certs/private.key
```

**Update** `edge/pa-service/appsettings.json`:
```json
{
  "MinIO": {
    "UseSSL": true,
    "ValidateCertificate": true
  }
}
```

**Checkpoint Verification**:
- [ ] Port 1883 returns connection refused
- [ ] Cross-tenant MQTT publish denied
- [ ] MinIO serves HTTPS (check with curl -k)
- [ ] No cleartext credentials in Wireshark capture

---

## Week 2: ESP32 Firmware Security (14 hours)

**Checkpoint**: Firmware credential protection + secure boot

### Day 1-2: NVS Encrypted Credentials (4h)
**File**: `firmware/esp32-button/main/nvs_credentials.c` (new)

```c
esp_err_t load_wifi_credentials(char* ssid, char* password) {
    nvs_handle_t nvs;
    esp_err_t err = nvs_open_from_partition("nvs_key",
        "credentials", NVS_READONLY, &nvs);
    if (err != ESP_OK) return err;

    size_t len = 32;
    nvs_get_str(nvs, "wifi_ssid", ssid, &len);
    nvs_get_str(nvs, "wifi_pass", password, &len);
    nvs_close(nvs);
    return ESP_OK;
}
```

**Delete hardcoded creds from** `config.h`:
```c
// REMOVE:
// #define WIFI_SSID "MyNetwork"
// #define WIFI_PASSWORD "password"
```

### Day 3: BLE Provisioning (2h)
**File**: `firmware/esp32-button/main/provisioning.c` (new)

```c
if (gpio_get_level(PROVISIONING_MODE_GPIO) == 0) {
    wifi_prov_mgr_start_provisioning(
        WIFI_PROV_SECURITY_1,
        "PROV_SafeSignal_XXXX",
        pop_pin  // Device-specific PIN
    );
}
```

### Day 4: Secure Boot V2 (3h)
```bash
# Generate signing key (KEEP OFFLINE!)
espsecure.py generate_signing_key --version 2 secure_boot_key.pem

# menuconfig: Security → Enable secure boot V2
# Secure boot version: Enable secure boot version 2

# Sign bootloader
espsecure.py sign_data --version 2 \
  --keyfile secure_boot_key.pem \
  -o bootloader-signed.bin bootloader.bin
```

### Day 5: Flash Encryption (2h + 3h testing)
```bash
# menuconfig: Security → Enable flash encryption on boot
# Flash encryption mode: Development (testing) → Release (production)

# First boot encrypts flash automatically
# WARNING: After release mode, key extraction impossible
```

**Testing** (3h):
```bash
# Verify credentials not extractable
esptool.py read_flash 0x0 0x400000 dump.bin
hexdump -C dump.bin | grep -i "wifi\|password"
# Expected: No matches (encrypted)

# Verify secure boot
esptool.py verify_signature bootloader-signed.bin
# Expected: Signature valid
```

**Checkpoint Verification**:
- [ ] No hardcoded credentials in firmware source
- [ ] Flash dump shows encrypted data only
- [ ] Unsigned firmware rejected by bootloader
- [ ] Provisioning mode accessible via GPIO

---

## Weeks 3-4: Rate Limiting + All Clear (22 hours)

**Checkpoint**: DoS protection + compliance workflow

### Week 3: Rate Limiting (6h)

**File**: `edge/policy-service/Services/RateLimitingService.cs` (new)

```csharp
public class RateLimitingService
{
    private readonly IDistributedCache _cache;
    private const int MAX_ALERTS_PER_DEVICE = 1;
    private const int WINDOW_SECONDS = 300;

    public async Task<bool> IsAllowedAsync(string deviceId, string tenantId)
    {
        var key = $"rate_limit:{tenantId}:{deviceId}";
        var current = await _cache.GetStringAsync(key);

        if (current != null && int.Parse(current) >= MAX_ALERTS_PER_DEVICE)
        {
            _logger.LogWarning("Rate limit exceeded: {Device}", deviceId);
            _metrics.IncrementRateLimitExceeded(tenantId, deviceId);
            return false;
        }

        await _cache.SetStringAsync(key, "1",
            new DistributedCacheEntryOptions {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(WINDOW_SECONDS)
            });
        return true;
    }
}
```

**Integrate**: `edge/policy-service/Services/MqttHandlerService.cs`
```csharp
public async Task HandleAlertTriggerAsync(MqttMessage message)
{
    // ✅ Add BEFORE processing
    if (!await _rateLimitingService.IsAllowedAsync(
        message.DeviceId, message.TenantId))
    {
        return;  // Drop alert
    }

    await _alertStateMachine.ProcessTriggerAsync(message);
}
```

### Week 3-4: All Clear Workflow (16h)

**Backend** (8h):

**Database** `cloud-backend/src/Infrastructure/Migrations/`:
```sql
CREATE TABLE approval_requests (
    id UUID PRIMARY KEY,
    alert_id UUID NOT NULL REFERENCES alerts(id),
    requester_id UUID NOT NULL REFERENCES users(id),
    approver_id UUID,
    status VARCHAR(50) NOT NULL,
    requested_at TIMESTAMP NOT NULL,
    approved_at TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    mfa_verified BOOLEAN DEFAULT false,
    reason TEXT,
    CONSTRAINT different_users CHECK (requester_id != approver_id)
);
```

**Endpoints** `cloud-backend/src/Api/Controllers/AlertsController.cs`:
```csharp
[HttpPost("{id}/all-clear/request")]
public async Task<IActionResult> RequestAllClear(Guid id,
    [FromBody] AllClearRequest request)
{
    var approval = await _allClearService.CreateRequestAsync(
        id, request.RequesterId, request.Reason);
    return Ok(approval);
}

[HttpPost("{id}/all-clear/approve")]
public async Task<IActionResult> ApproveAllClear(Guid id,
    [FromBody] AllClearApproval approval)
{
    // Verify different user
    if (approval.ApproverId == approval.RequesterId)
        return BadRequest("Cannot approve own request");

    // Verify MFA
    if (!await _mfaService.ValidateAsync(approval.MfaToken))
        return Unauthorized("MFA verification failed");

    var result = await _allClearService.ApproveAsync(
        id, approval.ApproverId, approval.ApprovalRequestId);
    return Ok(result);
}
```

**Mobile** (8h):

**Screens**:
- `AllClearRequestScreen.tsx` - Person 1 requests
- `AllClearApprovalScreen.tsx` - Person 2 approves
- Update `AlertHistoryScreen.tsx` - Show approval status

**Checkpoint Verification**:
- [ ] Device blocked after 1 alert in 5 minutes
- [ ] Rate limit metric increments on block
- [ ] Single person cannot approve All Clear
- [ ] MFA required for approval
- [ ] Approval audit trail captured

---

## Weeks 5-6: Testing & Compliance (Phase 3-4)

### API Versioning (4-8h)
- Migrate routes from `/api/` to `/api/v1/`
- Update mobile app API client
- Documentation alignment

### Testing Infrastructure (70h)
- Backend unit tests (target ≥70% coverage)
- Integration tests (E2E flows)
- Performance tests (latency verification)
- Load tests (10k alerts)

### WORM Audit Logging (12h)
- S3 Object Lock or MinIO WORM
- Immutable alert history
- 7-year retention policy

---

## Weeks 7-8: Production Deployment (Phase 5-6)

### CI/CD Pipeline (8h)
- GitHub Actions workflows
- Automated testing
- Docker builds
- Deployment automation

### Monitoring & Alerting (10h)
- Prometheus alert rules
- SLO monitoring (P95 ≤2s local, ≤5s global)
- Grafana dashboards
- PagerDuty integration

### Mobile App Stores (12h)
- TestFlight submission (iOS)
- Play Console submission (Android)
- App store assets
- Privacy policy updates

---

## Post-MVP (Phases 7-8)

**Deferred Features** (not blocking):
- PA hardware integration (16-24h)
- ATECC608A secure element (16h)
- Kafka/Service Bus (40-60h)
- PSAP adapters (80-120h)
- WORM compliance (covered in Phase 3-4)
- SMS/Voice notifications (30-40h)
- SPIFFE/SPIRE (60-80h - optional, secure boot sufficient)

---

## Success Criteria

### Week 1 Checkpoint ✅
- [ ] EMQX `ALLOW_ANONYMOUS=false`
- [ ] Port 1883 closed
- [ ] ACLs prevent cross-tenant access
- [ ] MinIO serves HTTPS only
- [ ] Penetration test passed

### Week 2 Checkpoint ✅
- [ ] No hardcoded credentials in firmware
- [ ] Secure boot prevents unsigned firmware
- [ ] Flash encryption enabled
- [ ] Credentials not extractable from dump

### Week 4 Checkpoint ✅
- [ ] Rate limiting operational
- [ ] Two-person All Clear enforced
- [ ] MFA verification working
- [ ] Compliance audit trail captured

### Week 6 Checkpoint ✅
- [ ] API versioning complete
- [ ] Test coverage ≥70%
- [ ] WORM logging operational
- [ ] E2E latency measured

### Week 8 Production-Ready ✅
- [ ] All P0 gaps closed
- [ ] CI/CD pipeline operational
- [ ] Monitoring with SLO alerts
- [ ] Mobile apps submitted

---

## Risk Management

**Active Risks**:
- Risk 1 (Source room audible) - ✅ Mitigated
- Risk 2 (Alarm loops) - ⚠️ Reopened (needs rate limiting + gRPC filtering)
- Risk 3 (Cert expiry) - ⚠️ Active (manual renewal only)
- Risk 4 (Latency degradation) - ⚠️ Unmanaged (no SLO alerts yet)

**New Risks** (require tracking):
- Risk 5 (Security gaps) - 🔴 Active (43h mitigation in progress)
- Risk 6 (Compliance gaps) - 🔴 Active (WORM + two-person rule needed)
- Risk 7 (OTA compromise) - ⚠️ Active (no kill-switch)
- Risk 8 (Legal liability) - ⚠️ Active (no DPA/DPIA)

**See**: Create `RISK_REGISTER.md` for complete tracking

---

## Resource Requirements

### Team
- 2 engineers (full-time, weeks 1-4)
- 3-4 engineers (weeks 5-8 for testing + deployment)

### Hardware
- 10x ESP32-S3-DevKitC-1 (order week 1)
- Development breadboards
- UPS units for testing

### Infrastructure
- Cloud hosting (Azure/AWS)
- S3/MinIO for WORM storage
- Certificate management
- CI/CD pipeline

---

## Summary Timeline

| Week | Focus | Hours | Checkpoint |
|------|-------|-------|------------|
| 0 | ✅ Doc alignment | 8-10h | Phase 0 complete |
| 1 | EMQX + MinIO security | 7h | Transport hardened |
| 2 | ESP32 firmware security | 14h | Credentials protected |
| 3-4 | Rate limit + All Clear | 22h | Compliance ready |
| 5-6 | Testing + WORM | 74-78h | Production tested |
| 7-8 | Deployment | 30h | Production-ready |

**Total**: 155-167 hours over 8 weeks

---

## Next Immediate Actions

1. **Apply safeSignal-doc updates** (DOC_UPDATES_*.md)
2. **Week 1 Day 1**: Close EMQX port 1883 + configure ACLs
3. **Week 1 Day 2**: Enable MinIO TLS
4. **Week 2**: Start ESP32 firmware hardening

---

**Status**: 🟡 **PROCEED WITH CRITICAL SECURITY HARDENING**

All P0 gaps identified, mitigation plan approved, ready to execute.

Related: `SECURITY_CRITICAL_GAPS.md`, `DOCUMENTATION_GAPS.md`, `MVP_STATUS.md`, `PROJECT_STATUS.md`
