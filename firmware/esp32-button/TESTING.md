# ESP32-S3 Firmware Testing Guide

**Testing Phase**: Week 1 Reliability Features
**Date**: 2025-11-02
**Features Under Test**: Alert Persistence, Watchdog, NTP Sync

---

## Prerequisites

### Hardware Required
- ESP32-S3-DevKitC-1 (8MB flash, 8MB PSRAM)
- USB-C cable
- Computer with ESP-IDF installed
- Optional: WiFi access point you can control

### Software Setup

```bash
# 1. Ensure ESP-IDF is sourced
cd ~/esp/esp-idf
. ./export.sh

# 2. Navigate to firmware directory
cd /path/to/safeSignal-mvp/firmware/esp32-button

# 3. Verify build environment
idf.py --version
# OR for legacy Make:
make --version
```

### Edge Gateway Setup

The firmware expects an MQTT broker with mTLS. Options:

**Option A: Use existing edge gateway**
```bash
cd ../../edge
docker-compose up -d
```

**Option B: Mock MQTT broker** (for standalone testing)
```bash
# Install mosquitto
brew install mosquitto  # macOS
# OR
sudo apt-get install mosquitto  # Linux

# Run without TLS for testing
mosquitto -v -p 1883
```

**Option C: Skip MQTT** (test alert queue only)
- Alerts will queue in NVS
- Can verify queue behavior without broker

---

## Build and Flash

### Method 1: CMake (ESP-IDF 5.x)

```bash
# Configure (first time only)
idf.py menuconfig
# Verify:
# - Serial flasher config → Flash size = 8MB
# - Component config → ESP32S3-Specific → Support for external RAM = Yes

# Build
idf.py build

# Flash
idf.py -p /dev/ttyUSB0 flash

# Monitor
idf.py -p /dev/ttyUSB0 monitor
```

**Exit monitor**: `Ctrl+]`

### Method 2: Legacy Make (ESP-IDF 4.x)

```bash
# Configure (first time only)
make menuconfig

# Build
make -j$(nproc)

# Flash
make flash ESPPORT=/dev/ttyUSB0

# Monitor
make monitor ESPPORT=/dev/ttyUSB0
```

**Exit monitor**: `Ctrl+]`

### Finding Your Serial Port

**macOS**:
```bash
ls /dev/tty.usbserial-* /dev/cu.usbserial-*
# OR
ls /dev/tty.SLAB_USBtoUART
```

**Linux**:
```bash
ls /dev/ttyUSB*
# OR
dmesg | grep tty
```

**Windows**:
- Device Manager → Ports (COM & LPT)
- Look for "Silicon Labs CP210x" or "USB Serial Device"

---

## Test 1: Basic Functionality ✅

**Objective**: Verify firmware boots and basic components initialize.

### Procedure

1. Flash firmware and open serial monitor
2. Observe startup sequence
3. Verify all subsystems initialize

### Expected Output

```
I (xxx) MAIN:
╔═══════════════════════════════════════════════════════════╗
║   SafeSignal ESP32-S3 Button                              ║
║   Version: 1.0.0-alpha                                    ║
║   Device ID: esp32-dev-001                                ║
╚═══════════════════════════════════════════════════════════╝

I (xxx) MAIN: [NVS] Initialized
I (xxx) MAIN: [GPIO] LED configured on GPIO2
I (xxx) MAIN: [GPIO] Button configured on GPIO0
I (xxx) ALERT_QUEUE: [QUEUE] Initialized: 0 pending alerts
I (xxx) WATCHDOG: [WDT] Initialized (timeout: 30 seconds)
I (xxx) WATCHDOG: [WDT] Monitoring task: button_task
I (xxx) WATCHDOG: [WDT] Monitoring task: status_task
I (xxx) WIFI: [WIFI] Connecting to 'SafeSignal-Edge'...
I (xxx) WIFI: [WIFI] Connected to AP
I (xxx) WIFI: [WIFI] Got IP address: 192.168.1.50
I (xxx) WIFI: [WIFI] Signal strength: -45 dBm
I (xxx) TIME_SYNC: [TIME] Initialized, waiting for sync...
I (xxx) TIME_SYNC: [TIME] NTP servers: pool.ntp.org, time.google.com, time.cloudflare.com
I (xxx) MQTT: [MQTT] Client started
I (xxx) MQTT: [MQTT] Connected to broker
I (xxx) TIME_SYNC: [TIME] Synchronized: 2025-11-02 14:30:45 UTC
I (xxx) MAIN: [READY] System initialized
I (xxx) MAIN: [READY] Press button to trigger alert
```

### Success Criteria

- [x] All subsystems initialize without errors
- [x] WiFi connects and gets IP address
- [x] MQTT connects to broker
- [x] NTP synchronizes time
- [x] Watchdog monitoring active

### Troubleshooting

**Issue**: WiFi connection fails
```
E (xxx) WIFI: [WIFI] Disconnected, reconnecting...
```
**Fix**: Update WiFi credentials in `include/config.h`:
```c
#define WIFI_SSID "your-network-name"
#define WIFI_PASS "your-password"
```

**Issue**: MQTT connection fails
```
E (xxx) MQTT: [MQTT] Connection failed
```
**Fix**: Check edge gateway is running:
```bash
docker ps | grep emqx
```

**Issue**: NTP sync fails
```
W (xxx) TIME_SYNC: [TIME] Synchronization timeout
```
**Fix**: Verify internet connectivity (NTP requires internet)

---

## Test 2: Alert Persistence (Critical) 🔴

**Objective**: Verify alerts are NEVER lost during network failures.

### Test 2A: Normal Alert Flow

**Procedure**:
1. Ensure WiFi and MQTT connected
2. Press BOOT button (GPIO0)
3. Observe LED blinks 5 times rapidly
4. Check serial output for alert published

**Expected Output**:
```
W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert 123456 (index 0, 1 pending)
I (xxx) MQTT: [MQTT] Alert 123456 published (msg_id=1)
I (xxx) MAIN: [ALERT] ✓ Alert sent (total: 1)
I (xxx) ALERT_QUEUE: [QUEUE] Processing 1 pending alerts
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert 123456 delivered
I (xxx) ALERT_QUEUE: [QUEUE] Processing complete: 1 delivered, 0 remaining
```

**Success**: Alert enqueued → published → removed from queue

---

### Test 2B: Alert Queueing (MQTT Disconnected)

**Procedure**:
1. With device running, disconnect MQTT broker:
   ```bash
   docker stop safesignal-emqx
   ```
   OR stop mosquitto service

2. Wait for MQTT disconnect message:
   ```
   W (xxx) MQTT: [MQTT] Disconnected from broker
   ```

3. Press BOOT button **3 times** (wait 2s between presses)

4. Observe serial output

5. Reconnect MQTT broker:
   ```bash
   docker start safesignal-emqx
   ```

6. Observe automatic delivery

**Expected Output**:

**During disconnect**:
```
W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert 123456 (index 0, 1 pending)

W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert 123457 (index 1, 2 pending)

W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert 123458 (index 2, 3 pending)
```

**On reconnect**:
```
I (xxx) MQTT: [MQTT] Connected to broker
I (xxx) MQTT: [MQTT] Delivered 3 queued alerts on reconnect
I (xxx) ALERT_QUEUE: [QUEUE] Processing 3 pending alerts
I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert 123456 (retry 0)
I (xxx) MQTT: [MQTT] Alert 123456 published (msg_id=1)
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert 123456 delivered
I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert 123457 (retry 0)
I (xxx) MQTT: [MQTT] Alert 123457 published (msg_id=2)
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert 123457 delivered
I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert 123458 (retry 0)
I (xxx) MQTT: [MQTT] Alert 123458 published (msg_id=3)
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert 123458 delivered
I (xxx) ALERT_QUEUE: [QUEUE] Processing complete: 3 delivered, 0 remaining
```

**Success Criteria**:
- [x] All 3 alerts queued during disconnect
- [x] All 3 alerts delivered automatically on reconnect
- [x] Queue count returns to 0

---

### Test 2C: Alert Persistence Across Reboot

**Procedure**:
1. With MQTT disconnected, press button 3 times
2. Verify alerts queued:
   ```
   I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert XXX (index X, 3 pending)
   ```
3. **Power off device** (unplug USB)
4. Wait 10 seconds
5. **Power on device** (plug in USB)
6. Monitor startup logs
7. Reconnect MQTT broker
8. Verify alerts delivered

**Expected Output**:

**On startup**:
```
I (xxx) ALERT_QUEUE: [QUEUE] Initialized: 3 pending alerts  ← Loaded from NVS!
```

**On MQTT reconnect**:
```
I (xxx) MQTT: [MQTT] Connected to broker
I (xxx) MQTT: [MQTT] Delivered 3 queued alerts on reconnect
I (xxx) ALERT_QUEUE: [QUEUE] Processing complete: 3 delivered, 0 remaining
```

**Success Criteria**:
- [x] Queue count restored from NVS on boot
- [x] Queued alerts delivered after reconnect
- [x] **ZERO ALERT LOSS** across power cycle

---

### Test 2D: Queue Statistics

**Procedure**:
1. Queue several alerts (press button with MQTT disconnected)
2. Let some deliver successfully
3. Check logs for statistics

**Expected Output**:
```
I (xxx) STATUS: Queue stats - Total: 10 enqueued, 7 delivered, 0 expired, 0 failed, 3 pending
```

**Validation**:
- `total_enqueued` = number of button presses
- `total_delivered` = successfully sent alerts
- `pending_count` = alerts still in queue

---

## Test 3: Watchdog Timer ⚠️

**Objective**: Verify watchdog detects task hangs and reboots system.

### Test 3A: Normal Watchdog Operation

**Procedure**:
1. Run firmware normally for 5 minutes
2. Press button periodically
3. Verify no watchdog resets

**Expected**: No watchdog timeout messages

---

### Test 3B: Simulated Task Hang (Requires Code Modification)

**⚠️ WARNING**: This test intentionally crashes the device.

**Procedure**:
1. Edit `main.c`, add infinite loop in `button_task()`:
   ```c
   if (bits & BUTTON_PRESSED_BIT) {
       ESP_LOGW(TAG, "[BUTTON] *** SIMULATING HANG ***");
       while(1) {
           vTaskDelay(pdMS_TO_TICKS(1000));  // Don't feed watchdog
       }
   }
   ```

2. Rebuild and flash
3. Press button once
4. Wait 30 seconds
5. Observe watchdog panic

**Expected Output**:
```
W (xxx) MAIN: [BUTTON] *** SIMULATING HANG ***
E (30000) task_wdt: Task watchdog got triggered. The following tasks did not reset the watchdog in time:
E (30000) task_wdt:  - button_task (CPU 0)
E (30000) task_wdt: Tasks currently running:
E (30000) task_wdt: CPU 0: status_task
E (30000) task_wdt: CPU 1: IDLE
Guru Meditation Error: Core 0 panic'ed (Task watchdog timeout)

Core 0 register dump:
...

Rebooting...
```

**Success Criteria**:
- [x] Watchdog triggers after 30 seconds
- [x] System automatically reboots
- [x] Normal operation resumes after reboot

**Cleanup**: Remove infinite loop and rebuild.

---

## Test 4: NTP Time Synchronization 🕐

**Objective**: Verify UTC timestamps are accurate.

### Test 4A: Time Sync on Boot

**Procedure**:
1. Flash firmware with internet-connected WiFi
2. Monitor serial output for NTP sync
3. Note timestamp in sync message

**Expected Output**:
```
I (xxx) TIME_SYNC: [TIME] Initialized, waiting for sync...
I (xxx) TIME_SYNC: [TIME] NTP servers: pool.ntp.org, time.google.com, time.cloudflare.com
I (5234) TIME_SYNC: [TIME] Synchronized: 2025-11-02 14:30:45 UTC
```

**Validation**:
- Timestamp matches current UTC time (within 1 second)
- Sync completes within 10 seconds of WiFi connection

---

### Test 4B: Alert Timestamps

**Procedure**:
1. After NTP sync, press button
2. Check MQTT payload or serial log for timestamp
3. Compare to current UTC time

**Expected**: Alert timestamp = current Unix epoch (±2 seconds)

Example:
```json
{
  "timestamp": 1730563845,  // Unix epoch
  ...
}
```

**Verify**:
```bash
# Convert to human-readable
date -u -r 1730563845
# Should match within 2 seconds of button press time
```

---

### Test 4C: Time Before Sync

**Procedure**:
1. Disconnect internet (keep local WiFi)
2. Reboot device
3. Press button immediately (before NTP sync)
4. Check alert timestamp

**Expected**:
```json
{
  "timestamp": 0,  // or small number (boot time)
  ...
}
```

**Success**: Firmware doesn't crash, uses fallback timestamp

---

## Test 5: Integration Testing 🔗

**Objective**: Verify all features work together under realistic conditions.

### Test 5A: 24-Hour Uptime

**Procedure**:
1. Flash firmware
2. Leave running for 24 hours
3. Press button periodically (every 1-2 hours)
4. Check for watchdog resets or crashes

**Success Criteria**:
- [x] No watchdog timeouts
- [x] No crashes or reboots (except watchdog test)
- [x] All alerts delivered successfully
- [x] Memory usage stable (check `freeHeap` in status messages)

**Monitor**:
```bash
# In another terminal, watch for reboots
idf.py monitor | grep "rst:0x"
```

---

### Test 5B: Network Stress Test

**Procedure**:
1. Disconnect WiFi
2. Press button 5 times (queue 5 alerts)
3. Reconnect WiFi
4. Wait for delivery
5. Repeat steps 1-4 ten times

**Success Criteria**:
- [x] All 50 alerts delivered (5 alerts × 10 cycles)
- [x] No queue overflows
- [x] No NVS errors

**Validation**:
```bash
# Count delivered alerts in edge gateway logs
docker logs safesignal-policy-service | grep "Alert received" | wc -l
# Should equal 50
```

---

### Test 5C: Rapid Button Presses

**Objective**: Test debouncing and queue under stress.

**Procedure**:
1. Ensure MQTT connected
2. Press button rapidly 20 times (as fast as possible)
3. Count delivered alerts

**Expected**:
- Debouncing limits to ~20 alerts (50ms debounce)
- All non-debounced presses result in queued alerts
- All queued alerts delivered

**Success**: No crashes, all valid button presses result in alerts

---

## Test 6: Edge Cases 🚨

### Test 6A: Queue Overflow

**Procedure**:
1. Disconnect MQTT
2. Press button 55 times (exceed 50-slot queue)
3. Observe error message

**Expected Output**:
```
E (xxx) ALERT_QUEUE: [QUEUE] Queue full (50 alerts)
E (xxx) MQTT: [MQTT] Failed to enqueue alert: ESP_ERR_NO_MEM
```

**Success**: Firmware doesn't crash, oldest alerts preserved

---

### Test 6B: Alert Expiration

**Procedure**:
1. Disconnect MQTT permanently
2. Press button to queue alert
3. Wait 1 hour (3600 seconds)
4. Reconnect MQTT
5. Observe cleanup

**Expected Output**:
```
W (xxx) ALERT_QUEUE: [QUEUE] Alert 123456 expired, removing
I (xxx) ALERT_QUEUE: [QUEUE] Cleanup: 1 expired alerts removed
```

**Success**: Expired alerts don't retry indefinitely

---

### Test 6C: Max Retry Limit

**Procedure**:
1. Configure MQTT broker to reject all publishes (invalid credentials)
2. Press button once
3. Wait for 10 retry attempts (~100 seconds with 10s processing interval)
4. Observe failure

**Expected Output**:
```
E (xxx) ALERT_QUEUE: [QUEUE] Alert 123456 exceeded retry limit, removing
```

**Success**: Alert removed after 10 failed attempts, no infinite retry

---

## Performance Validation ⚡

### Latency Measurement

**Objective**: Measure button press → MQTT publish latency.

**Procedure**:
1. Modify `button_task()` to log timestamp:
   ```c
   uint32_t press_time = esp_timer_get_time() / 1000;  // ms
   ESP_LOGI(TAG, "[LATENCY] Button pressed at %lu ms", press_time);
   ```

2. Modify `mqtt_publish_alert_from_queue()` to log timestamp:
   ```c
   uint32_t publish_time = esp_timer_get_time() / 1000;  // ms
   ESP_LOGI(TAG, "[LATENCY] Alert published at %lu ms", publish_time);
   ```

3. Press button 10 times
4. Calculate average latency

**Target**: <100ms (currently 50-90ms expected)

---

### Memory Usage

**Check heap usage**:
```c
// In status_task(), log periodically
uint32_t free_heap = esp_get_free_heap_size();
uint32_t min_heap = esp_get_minimum_free_heap_size();
ESP_LOGI(TAG, "[MEM] Free: %lu, Min: %lu", free_heap, min_heap);
```

**Baseline**: ~180KB free initially
**After 24h**: Should remain >170KB (no memory leaks)

---

## Troubleshooting Common Issues

### Issue: Build Fails

**Error**: `fatal error: alert_queue.h: No such file or directory`

**Fix**: Ensure new files are in `main/` directory and `component.mk` includes them:
```makefile
COMPONENT_SRCDIRS := .
COMPONENT_ADD_INCLUDEDIRS := . ../include
```

---

### Issue: NVS Initialization Fails

**Error**: `ESP_ERR_NVS_NO_FREE_PAGES` or `ESP_ERR_NVS_NEW_VERSION_FOUND`

**Fix**: Erase NVS partition:
```bash
idf.py erase-flash
# OR
make erase_flash
```

Then reflash firmware.

---

### Issue: Watchdog Triggers Unexpectedly

**Error**: Task watchdog timeout without intentional hang

**Debug**:
1. Increase timeout in `include/config.h`:
   ```c
   #define WATCHDOG_TIMEOUT_SECONDS 60  // Increase from 30
   ```
2. Add logging before long operations
3. Ensure `watchdog_feed()` called in all task loops

---

### Issue: Time Never Syncs

**Error**: NTP timeout after 30 seconds

**Causes**:
1. No internet connectivity (check with `ping 8.8.8.8`)
2. Firewall blocking NTP (port 123 UDP)
3. NTP servers unreachable from your network

**Fix**: Use local NTP server:
```c
// In time_sync.c
#define NTP_SERVER_PRIMARY "192.168.1.1"  // Your router
```

---

## Test Results Template

Copy and fill out for each test session:

```markdown
## Test Session: [Date/Time]

**Firmware Version**: 1.0.0-alpha
**Hardware**: ESP32-S3-DevKitC-1
**Tester**: [Your Name]

### Test Results Summary

| Test | Status | Notes |
|------|--------|-------|
| 1. Basic Functionality | ✅ PASS | All subsystems initialized |
| 2A. Normal Alert Flow | ✅ PASS | Alert delivered in 67ms |
| 2B. Alert Queueing | ✅ PASS | 3 alerts queued and delivered |
| 2C. Reboot Persistence | ✅ PASS | Alerts survived power cycle |
| 2D. Queue Statistics | ✅ PASS | Stats accurate |
| 3A. Watchdog Normal | ✅ PASS | No false positives in 5min |
| 3B. Watchdog Hang | ✅ PASS | Detected hang, rebooted |
| 4A. NTP Sync | ✅ PASS | Synced in 4.2 seconds |
| 4B. Alert Timestamps | ✅ PASS | UTC timestamps correct |
| 4C. Time Before Sync | ✅ PASS | Fallback to 0 worked |
| 5A. 24-Hour Uptime | ⏳ PENDING | In progress |
| 5B. Network Stress | ✅ PASS | 50/50 alerts delivered |
| 5C. Rapid Presses | ✅ PASS | Debouncing works, no crashes |
| 6A. Queue Overflow | ✅ PASS | Graceful error handling |
| 6B. Alert Expiration | ⏳ PENDING | Requires 1 hour wait |
| 6C. Max Retry | ⏳ PENDING | Requires broker config |

### Performance Metrics

- **Alert Latency**: 67ms average (target <100ms) ✅
- **Memory Usage**: 175KB free after 4 hours ✅
- **Watchdog False Positives**: 0 ✅

### Issues Found

None - all tests passed!

### Next Steps

- Complete 24-hour uptime test
- Test alert expiration (requires 1hr wait)
- Measure latency under load
```

---

## Quick Start: 5-Minute Smoke Test

If you just want to verify it works:

```bash
# 1. Build and flash
idf.py build flash monitor

# 2. Wait for startup, verify you see:
#    - [READY] System initialized
#    - [TIME] Synchronized

# 3. Press BOOT button
#    - LED should blink 5 times
#    - Should see: [ALERT] ✓ Alert sent

# 4. Disconnect MQTT (docker stop safesignal-emqx)
# 5. Press button 3 times
#    - Should see: alert queued for delivery

# 6. Reconnect MQTT (docker start safesignal-emqx)
#    - Should see: 3 delivered alerts

# ✅ If all above works, core functionality is good!
```

---

**Next Steps After Testing**:
- Document any bugs found
- Collect performance metrics
- Proceed to graceful error recovery implementation
- Then move to ATECC608A integration (Week 2-3)

**Questions or Issues?**
- Check `RELIABILITY_HARDENING.md` for implementation details
- Review source code in `main/alert_queue.c`, `main/watchdog.c`, `main/time_sync.c`
