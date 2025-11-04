# Security Critical Gaps - Immediate Action Required

**Date**: 2025-11-04
**Status**: 🔴 **CRITICAL - Production Blockers Identified**
**Priority**: P0 - Must fix before ANY production deployment

---

## Executive Summary

Comprehensive audit reveals **8 critical security gaps** that make the current system **UNSAFE for production use**. These are life-safety blockers that must be addressed before pilot deployment.

**Total Critical Hours**: 43 hours (Phases 1-2)
**Timeline**: 2 weeks with 2 engineers

---

## 🔴 P0 Critical Gaps (BLOCKING)

### 1. EMQX Unauthenticated Access
**File**: `edge/docker-compose.yml:7,45`

**Current**: Port 1883 exposed + `EMQX_ALLOW_ANONYMOUS: "true"`
**Risk**: Anyone on network can trigger emergency alerts
**Fix**: 4 hours

```yaml
ports:
  # REMOVE: - "1883:1883"
  - "8883:8883"  # mTLS only

environment:
  EMQX_ALLOW_ANONYMOUS: "false"

volumes:
  - ./emqx/acl.conf:/opt/emqx/etc/acl.conf
```

**ACL** (`edge/emqx/acl.conf`):
```
{deny, all}.
{allow, {user, "device-{tenantId}-{deviceId}"}, publish, ["alerts/{tenantId}/{buildingId}/#"]}.
{allow, {user, "policy-service"}, subscribe, ["alerts/#"]}.
```

---

### 2. MinIO Unencrypted HTTP
**File**: `edge/docker-compose.yml:113`

**Current**: HTTP only on ports 9000/9001
**Risk**: Audio clips and credentials transmitted in cleartext
**Fix**: 3 hours

```yaml
minio:
  environment:
    MINIO_ROOT_USER: ${MINIO_ROOT_USER}
    MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
  volumes:
    - ./certs/minio/public.crt:/root/.minio/certs/public.crt
    - ./certs/minio/private.key:/root/.minio/certs/private.key
```

---

### 3. ESP32 Hardcoded Credentials
**File**: `firmware/esp32-button/include/config.h:33`

**Current**: WiFi password and private keys in firmware binary
**Risk**: Credentials extractable with `esptool.py read_flash`
**Fix**: 8 hours

**Solution**: Move to encrypted NVS + BLE provisioning
```c
esp_err_t load_wifi_credentials(char* ssid, char* password) {
    nvs_handle_t nvs;
    nvs_open_from_partition("nvs_key", "credentials", NVS_READONLY, &nvs);
    nvs_get_str(nvs, "wifi_ssid", ssid, &len);
    nvs_get_str(nvs, "wifi_pass", password, &len);
    nvs_close(nvs);
    return ESP_OK;
}
```

---

### 4. No Secure Boot or Flash Encryption
**File**: `firmware/esp32-button/README.md:23`

**Current**: Firmware extractable, no signature verification
**Risk**: Malicious firmware installation, credential extraction
**Fix**: 6 hours

**Enable Secure Boot V2**:
```bash
espsecure.py generate_signing_key secure_boot_signing_key.pem
# menuconfig: Security → Enable secure boot V2
espsecure.py sign_data --version 2 --keyfile key.pem -o bootloader-signed.bin bootloader.bin
```

**Enable Flash Encryption**:
```bash
# menuconfig: Security → Enable flash encryption on boot
# First boot encrypts flash automatically
```

---

### 5. No Rate Limiting
**File**: `edge/policy-service/Services/MqttHandlerService.cs`

**Current**: Devices can send unlimited alerts
**Risk**: DoS via alert flooding
**Fix**: 6 hours

**Add RateLimitingService**:
```csharp
public class RateLimitingService
{
    private const int MAX_ALERTS_PER_DEVICE = 1;
    private const int WINDOW_SECONDS = 300;  // 5 minutes

    public async Task<bool> IsAllowedAsync(string deviceId, string tenantId)
    {
        var key = $"rate_limit:{tenantId}:{deviceId}";
        var current = await _cache.GetStringAsync(key);

        if (current != null && int.Parse(current) >= MAX_ALERTS_PER_DEVICE)
            return false;

        await _cache.SetStringAsync(key, "1",
            new DistributedCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(WINDOW_SECONDS)
            });
        return true;
    }
}
```

---

### 6. No Two-Person All Clear
**File**: `cloud-backend/src/Api/Controllers/AlertsController.cs:227`

**Current**: Single person can resolve alerts
**Risk**: Premature "All Clear" without verification
**Fix**: 16 hours (backend 8h + mobile 8h)

**Database**:
```sql
CREATE TABLE approval_requests (
    id UUID PRIMARY KEY,
    alert_id UUID NOT NULL REFERENCES alerts(id),
    requester_id UUID NOT NULL,
    approver_id UUID,
    status VARCHAR(50),
    requested_at TIMESTAMP NOT NULL,
    approved_at TIMESTAMP,
    mfa_verified BOOLEAN,
    CONSTRAINT different_users CHECK (requester_id != approver_id)
);
```

**Endpoints**:
```http
POST /api/v1/alerts/{id}/all-clear/request
POST /api/v1/alerts/{id}/all-clear/approve
```

---

### 7. No ATECC608A Integration
**File**: `firmware/esp32-button/include/config.h:33`

**Current**: Private keys in flash (extractable)
**Risk**: Device cloning, key extraction
**Fix**: 16 hours (DEFER to Phase 7+)

**Recommendation**: Use secure boot + flash encryption (Phase 1c - 6h) instead.
ATECC requires hardware changes, acceptable to defer for MVP.

---

### 8. No WORM Audit Logging
**File**: PostgreSQL (mutable logs)

**Current**: Alerts in PostgreSQL can be modified
**Risk**: Cannot meet compliance requirements (ISO 27001, SOC 2)
**Fix**: 12 hours (Phase 3-4)

**S3 Object Lock**:
```csharp
await _s3Client.PutObjectAsync(new() {
    BucketName = "safesignal-audit-logs",
    Key = $"alerts/{DateTime.UtcNow:yyyy/MM/dd}/{alertId}.json",
    ObjectLockMode = ObjectLockMode.COMPLIANCE,
    ObjectLockRetainUntilDate = DateTime.UtcNow.AddYears(7)
});
```

---

## Timeline Summary

| Gap | Hours | Phase | Blocking? |
|-----|-------|-------|-----------|
| EMQX Auth | 4h | 1a | ✅ YES |
| MinIO TLS | 3h | 1a | ✅ YES |
| ESP32 Creds | 8h | 1c | ✅ YES |
| Secure Boot | 6h | 1c | ✅ YES |
| Rate Limiting | 6h | 1c | ✅ YES |
| All Clear | 16h | 2 | ✅ YES |
| ATECC608A | 16h | 7+ | ⚠️ Defer |
| WORM Logs | 12h | 3-4 | ⚠️ Before compliance |

**Critical Path**: 43 hours over 2 weeks

---

## Immediate Action (This Week)

### Days 1-2: Edge Security (7h)
1. Disable EMQX port 1883
2. Configure EMQX ACLs
3. Enable MinIO TLS
4. Test end-to-end

### Days 3-5: Firmware Security (14h)
1. NVS encrypted credentials
2. BLE provisioning
3. Secure boot V2
4. Flash encryption
5. Testing

### Week 2: Rate Limiting + All Clear (22h)
1. Rate limiting service
2. All Clear backend
3. All Clear mobile UI

---

## Verification Checklist

- [ ] EMQX `ALLOW_ANONYMOUS=false`
- [ ] Port 1883 CLOSED
- [ ] ACLs prevent cross-tenant publishing
- [ ] MinIO serves HTTPS only
- [ ] ESP32 has NO hardcoded credentials
- [ ] Secure boot prevents unsigned firmware
- [ ] Flash encryption prevents extraction
- [ ] Rate limiting blocks >1 alert/5min
- [ ] Two-person All Clear enforced
- [ ] Penetration test passed

---

**Status**: 🔴 **DO NOT DEPLOY WITHOUT THESE FIXES**

Related: `DOCUMENTATION_GAPS.md`, `REVISED_ROADMAP.md`, `MVP_STATUS.md`
