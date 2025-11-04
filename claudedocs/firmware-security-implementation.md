# ESP32 Firmware Security Implementation Plan

**Priority**: P0 - Blocking Production
**Timeline**: Week 2 (Per roadmap)
**Risk**: Credential cloning, firmware tampering, DoS attacks

## Current Security Posture (INSECURE)

### 🔴 Critical Vulnerabilities

1. **Hardcoded WiFi Credentials** (`config.h:35-36`)
   ```c
   #define WIFI_SSID "SafeSignal-Edge"
   #define WIFI_PASS "safesignal-dev"
   ```
   **Risk**: Attacker with device access clones credentials

2. **Embedded TLS Certificates** (`firmware/esp32-button/certs/`)
   - Private key embedded in flash (readable with UART/JTAG)
   - Same credentials across all devices (credential cloning)
   **Risk**: Single compromised device = all devices compromised

3. **No Secure Boot** (README.md:263)
   - Unsigned firmware can be flashed
   - Attacker can inject malicious firmware
   **Risk**: Complete device takeover

4. **No Flash Encryption** (README.md:264)
   - Firmware readable from flash chip
   - Secrets extractable with physical access
   **Risk**: Credential theft, reverse engineering

5. **No Rate Limiting** (Gap #5 from analysis)
   - Button spam can DoS policy service
   - No client-side or server-side throttling
   **Risk**: Gateway overload, alert storm

## Implementation Roadmap

### Phase 1: Secure Provisioning (Week 2, Days 1-2)

**Goal**: Remove hardcoded credentials, implement BLE on-boarding

#### 1.1 NVS Encrypted Storage
- Store WiFi credentials in NVS (Non-Volatile Storage)
- Enable NVS encryption with generated key
- Partition table update for NVS storage

**Files to modify:**
- `partitions.csv` - Add NVS partition
- New: `main/provisioning.c/h` - NVS read/write API
- `main/wifi.c` - Read credentials from NVS

**Implementation:**
```c
// provisioning.h
typedef struct {
    char wifi_ssid[32];
    char wifi_password[64];
    char device_id[32];
    char tenant_id[16];
    char building_id[16];
    char room_id[16];
} device_config_t;

esp_err_t provision_init(void);
esp_err_t provision_save_config(const device_config_t *config);
esp_err_t provision_load_config(device_config_t *config);
bool provision_is_provisioned(void);
esp_err_t provision_clear(void);  // Factory reset
```

#### 1.2 BLE Provisioning Service
- Implement BLE GATT server for credential input
- Mobile app or CLI tool for provisioning
- Devices ship blank (no credentials)

**Implementation Steps:**
1. Add BLE stack to ESP-IDF components
2. Create GATT service with characteristics:
   - WiFi SSID (write)
   - WiFi Password (write)
   - Device Identity (write)
   - Provisioning Status (read)
3. Build simple provisioning tool (Python CLI or React Native app)

**Security:**
- BLE pairing required (6-digit PIN)
- Credentials encrypted in transit
- BLE disabled after provisioning complete

#### 1.3 Certificate Provisioning
**Option A: OTA Injection (MVP)**
- Generate unique cert per device
- Upload via BLE during provisioning
- Store in NVS encrypted partition

**Option B: ATECC608A (Production)**
- Private key stored in secure element
- Certificate signed during manufacturing
- Never readable from device

**MVP Implementation (Option A):**
```c
// Certificate storage in NVS
esp_err_t provision_save_certificate(const char *ca_cert,
                                      const char *client_cert,
                                      const char *client_key);
esp_err_t provision_load_certificate(char **ca_cert,
                                      char **client_cert,
                                      char **client_key);
```

### Phase 2: Secure Boot + Flash Encryption (Week 2, Days 3-4)

**Goal**: Prevent unsigned firmware, encrypt flash contents

#### 2.1 Flash Encryption Setup

**Test on Dev Board First:**
```bash
# 1. Enable flash encryption in menuconfig
idf.py menuconfig
# Security features → Enable flash encryption on boot: Yes
# Security features → Enable usage mode: Development (reflashable)

# 2. Build with encryption
idf.py build

# 3. Flash encrypted firmware
idf.py flash

# Device will encrypt flash on first boot (PERMANENT unless dev mode)
```

**Production Settings:**
- Release mode (flash encryption key burned in eFuse, one-way)
- UART download mode disabled (prevents readout)

#### 2.2 Secure Boot V2 Setup

**Test on Dev Board:**
```bash
# 1. Enable secure boot in menuconfig
idf.py menuconfig
# Security features → Enable secure boot v2: Yes
# Security features → Secure boot signing key: secure_boot_signing_key.pem

# 2. Generate signing key (KEEP SECRET)
espsecure.py generate_signing_key secure_boot_signing_key.pem

# 3. Build signed firmware
idf.py build

# 4. Flash (burns secure boot eFuse)
idf.py flash

# Device will ONLY accept signed firmware from now on
```

**⚠️ CRITICAL WARNINGS:**
- Secure boot is **PERMANENT** - bad config = bricked device
- Test on disposable dev board first
- Keep signing key in HSM or secure storage
- Never commit signing key to git

#### 2.3 Rollback Protection

**Enable anti-rollback:**
```c
// app_desc
const esp_app_desc_t esp_app_desc = {
    .version = "1.0.0",
    .secure_version = 1,  // Increment on security-critical updates
    // Bootloader rejects older secure_version
};
```

**eFuse counter incremented on OTA:**
- Prevents downgrade to vulnerable firmware
- Requires OTA update system

### Phase 3: Rate Limiting (Week 2, Day 5)

**Goal**: Prevent button spam DoS attacks

#### 3.1 Firmware-Side Throttling

**Button debounce + rate limit:**
```c
// button.c
#define BUTTON_COOLDOWN_MS 1000  // Min 1 second between alerts

static int64_t last_press_time = 0;

void button_isr_handler(void* arg) {
    int64_t now = esp_timer_get_time() / 1000;  // Convert to ms

    if (now - last_press_time < BUTTON_COOLDOWN_MS) {
        ESP_LOGW(TAG, "Button press ignored (cooldown)");
        return;  // Rate limited
    }

    last_press_time = now;
    // Process alert...
}
```

**Alert queue bounds:**
```c
// alert_queue.c
#define MAX_QUEUED_ALERTS 10  // Drop oldest if exceeded

// Prevents memory exhaustion from rapid button presses
```

#### 3.2 Policy Service Rate Limiting

**Token bucket algorithm:**
```csharp
// Services/RateLimitService.cs
public class RateLimitService
{
    private readonly Dictionary<string, TokenBucket> _buckets = new();

    public bool AllowRequest(string deviceId)
    {
        var bucket = GetOrCreateBucket(deviceId);
        return bucket.TryConsume();
    }
}

public class TokenBucket
{
    private readonly int _capacity = 10;        // Max 10 alerts
    private readonly int _refillRate = 1;       // 1 per 10 seconds
    private readonly TimeSpan _refillInterval = TimeSpan.FromSeconds(10);
    private int _tokens;
    private DateTime _lastRefill;

    public bool TryConsume()
    {
        Refill();
        if (_tokens > 0) {
            _tokens--;
            return true;
        }
        return false;  // Rate limited
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefill;
        var tokensToAdd = (int)(elapsed / _refillInterval) * _refillRate;

        if (tokensToAdd > 0) {
            _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
            _lastRefill = now;
        }
    }
}
```

**Integration:**
```csharp
// MqttSubscriberService.cs
private async Task HandleAlertMessage(MqttApplicationMessageReceivedEventArgs e)
{
    var alert = JsonSerializer.Deserialize<AlertMessage>(payload);

    if (!_rateLimitService.AllowRequest(alert.DeviceId))
    {
        _logger.LogWarning("Alert rate limited: {DeviceId}", alert.DeviceId);
        _metrics.RateLimitedAlerts.Inc();
        return;  // Drop alert
    }

    // Process alert...
}
```

#### 3.3 EMQX MQTT Quotas

**Per-client publish rate:**
```conf
# emqx.conf
## Rate Limiting (per-client)
zone.external.publish_limit = 100,10s  # Max 100 msgs per 10s
zone.external.max_subscriptions = 50
zone.external.max_inflight = 32

## Connection limits
zone.external.idle_timeout = 30s
zone.external.max_packet_size = 256KB
```

**Already configured** in current `edge/emqx/emqx.conf:93-99`

## Testing & Validation

### Secure Provisioning Test Plan

1. **Fresh Device Provisioning:**
   ```bash
   # Flash blank firmware
   idf.py flash

   # Run provisioning tool
   python scripts/provision_device.py --device /dev/ttyUSB0 \
       --wifi-ssid "SafeSignal-Edge" \
       --wifi-pass "password123" \
       --device-id "esp32-prod-001" \
       --tenant "tenant-a"

   # Verify credentials saved to NVS
   # Verify BLE disabled after provisioning
   # Verify device connects to WiFi/MQTT
   ```

2. **Credential Security:**
   ```bash
   # Dump flash (should be encrypted)
   esptool.py read_flash 0 0x400000 flash_dump.bin

   # Search for plaintext credentials (should fail)
   strings flash_dump.bin | grep "SafeSignal-Edge"  # Should not find
   ```

### Secure Boot Test Plan

1. **Valid Firmware:**
   ```bash
   # Build and sign firmware
   idf.py build

   # Flash signed firmware (should succeed)
   idf.py flash
   ```

2. **Invalid Firmware (Unsigned):**
   ```bash
   # Build unsigned firmware
   # Modify signature to corrupt it

   # Flash (should REJECT)
   idf.py flash  # Expected: Bootloader rejects unsigned image
   ```

3. **Rollback Protection:**
   ```bash
   # Flash v1.1.0 (secure_version = 2)
   # Attempt to flash v1.0.0 (secure_version = 1)
   # Expected: Bootloader rejects downgrade
   ```

### Rate Limiting Test Plan

1. **Firmware Throttling:**
   ```python
   # spam_button.py - Simulate rapid button presses
   import time
   for i in range(100):
       trigger_button()
       time.sleep(0.1)  # 10 presses/second

   # Expected: Only 1 alert sent per second (firmware rate limit)
   ```

2. **Policy Service Rate Limit:**
   ```python
   # spam_mqtt.py - Send 100 alerts rapidly
   for i in range(100):
       publish_alert(device_id="esp32-001", alert_id=f"test-{i}")

   # Expected: ~10 alerts processed, rest dropped
   # Check logs for "Alert rate limited"
   ```

3. **EMQX Connection Limits:**
   ```bash
   # Monitor EMQX metrics
   docker exec safesignal-emqx emqx eval 'emqx_metrics:val("messages.dropped.rate_limit").'

   # Should show dropped messages when limit exceeded
   ```

## Deployment Checklist

### Pre-Production
- [ ] Secure provisioning implemented and tested
- [ ] NVS encryption enabled and validated
- [ ] BLE provisioning tool built and tested
- [ ] Secure boot tested on dev board (disposable)
- [ ] Flash encryption tested on dev board
- [ ] Rollback protection verified
- [ ] Rate limiting firmware changes deployed
- [ ] Rate limiting policy service deployed
- [ ] All tests passing

### Production Deployment
- [ ] Generate production signing keys (store in HSM)
- [ ] Generate unique certificates per device
- [ ] Factory provisioning workflow documented
- [ ] Secure boot enabled (PERMANENT)
- [ ] Flash encryption enabled (PERMANENT - release mode)
- [ ] Firmware version tracking system
- [ ] OTA update infrastructure ready
- [ ] Rollback procedure documented
- [ ] Certificate rotation plan

## File Structure

```
firmware/esp32-button/
├── main/
│   ├── provisioning.c/h        # NEW: NVS encrypted storage
│   ├── ble_provisioning.c/h    # NEW: BLE GATT server
│   ├── rate_limit.c/h          # NEW: Client-side throttling
│   ├── button.c                # MODIFY: Add rate limiting
│   ├── wifi.c                  # MODIFY: Read from NVS
│   └── mqtt.c                  # MODIFY: Load certs from NVS
│
├── scripts/
│   ├── provision_device.py     # NEW: CLI provisioning tool
│   ├── generate_device_cert.sh # NEW: Per-device cert generation
│   └── enable_secure_boot.sh   # NEW: Production flashing script
│
├── keys/
│   └── secure_boot_signing_key.pem  # NEW: KEEP SECRET (gitignored)
│
├── partitions_secure.csv       # NEW: Updated partition table
└── sdkconfig.secure            # NEW: Secure boot + encryption config
```

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Bricked devices (secure boot misconfiguration) | Medium | High | Test on disposable dev boards first |
| Lost signing keys | Low | Critical | HSM storage + backup procedure |
| Provisioning UX issues | High | Medium | Simple CLI tool + clear docs |
| Rate limit too aggressive | Medium | Medium | Tunable parameters, monitoring |
| Flash encryption performance | Low | Low | Hardware-accelerated on ESP32-S3 |

## Timeline

**Week 2 (5 days):**
- Day 1: Secure provisioning (NVS + BLE) implementation
- Day 2: Provisioning testing + certificate injection
- Day 3: Flash encryption + secure boot setup (dev board)
- Day 4: Secure boot validation + rollback testing
- Day 5: Rate limiting (firmware + policy service)

**Week 3 (validation):**
- Full end-to-end security testing
- Factory provisioning workflow
- Documentation completion

## Success Criteria

✅ Devices ship with no credentials (blank NVS)
✅ BLE provisioning works reliably
✅ Certificates unique per device
✅ Secure boot rejects unsigned firmware
✅ Flash dump shows encrypted data only
✅ Button spam limited to 1/second (firmware)
✅ Policy service rate limits to 10/10s per device
✅ EMQX enforces 100 msg/10s per client
✅ All security tests passing
✅ Production deployment checklist complete

## References

- [ESP32 Secure Boot V2](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/security/secure-boot-v2.html)
- [ESP32 Flash Encryption](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/security/flash-encryption.html)
- [ESP32 NVS Encryption](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-reference/storage/nvs_flash.html#nvs-encryption)
- [ESP32 BLE Provisioning](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-reference/provisioning/provisioning.html)
- [Security Gap Analysis](./security-gap-analysis.md) (if exists)
- [Revised Roadmap](./REVISED_ROADMAP.md)
