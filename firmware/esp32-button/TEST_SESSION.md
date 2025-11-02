# ESP32-S3 Test Session Guide

**Date**: 2025-11-02
**Tester**: Mads
**Objective**: Verify Week 1 reliability features

---

## Step 1: Environment Setup ✓

### 1.1 Check ESP-IDF Environment

Run this in your terminal:

```bash
# Navigate to firmware directory
cd /Users/mads/dev/repos/collab/safeSignal/safeSignal-mvp/firmware/esp32-button

# Check if ESP-IDF is available
which idf.py
```

**Expected**: Path to `idf.py` (e.g., `/Users/mads/esp/esp-idf/tools/idf.py`)

**If not found**, source ESP-IDF:
```bash
cd ~/esp/esp-idf
. ./export.sh
cd /Users/mads/dev/repos/collab/safeSignal/safeSignal-mvp/firmware/esp32-button
```

---

### 1.2 Check Serial Port

**macOS** (you're on Darwin/macOS):
```bash
ls /dev/tty.* | grep -i usb
# OR
ls /dev/cu.* | grep -i usb
```

**Expected**: Something like `/dev/tty.usbserial-XXXXXX` or `/dev/cu.SLAB_USBtoUART`

**Note your port**: _____________________

---

### 1.3 Check Edge Gateway (Optional)

```bash
cd ../../edge
docker ps | grep emqx
```

**Expected**: Container running with status "Up"

**If not running**:
```bash
docker start safesignal-emqx
# OR
docker-compose up -d
```

**Note**: If you don't have edge gateway, that's OK! We can still test alert queueing without MQTT.

---

## Step 2: Build Firmware 🔨

### 2.1 Clean Build (Recommended First Time)

```bash
# Navigate to firmware directory
cd /Users/mads/dev/repos/collab/safeSignal/safeSignal-mvp/firmware/esp32-button

# Clean previous builds
idf.py fullclean
# OR for legacy Make:
make clean

# Build firmware
idf.py build
# OR:
make -j$(sysctl -n hw.ncpu)  # Use all CPU cores on macOS
```

**Expected**:
- Compilation messages
- Final: `Project build complete. To flash, run...`

**If build fails**:
- Error message: _____________________
- Check `include/config.h` exists
- Verify all `.c` files in `main/` directory

**Time estimate**: 2-5 minutes (first build)

---

## Step 3: Flash Firmware 📥

### 3.1 Connect ESP32

1. Connect ESP32-S3-DevKitC-1 via USB-C cable
2. Verify port appears: `ls /dev/tty.* | grep usb`

### 3.2 Flash

```bash
# Replace /dev/ttyUSB0 with your actual port from Step 1.2
idf.py -p /dev/tty.SLAB_USBtoUART flash
# OR:
make flash ESPPORT=/dev/tty.SLAB_USBtoUART
```

**Expected**:
```
Connecting....
Writing at 0x00001000... (X %)
...
Hash of data verified.
Leaving...
Hard resetting via RTS pin...
```

**If flash fails**:
- Hold BOOT button while flashing
- Try different USB cable/port
- Check port permissions

**Time estimate**: 30-60 seconds

---

## Step 4: Monitor Serial Output 👀

### 4.1 Open Monitor

```bash
idf.py -p /dev/tty.SLAB_USBtoUART monitor
# OR:
make monitor ESPPORT=/dev/tty.SLAB_USBtoUART
```

**Expected**: Continuous stream of log messages

**To exit monitor**: Press `Ctrl + ]`

---

### 4.2 Verify Startup Sequence

**Look for these lines** (in order):

```
[ ] I (xxx) MAIN: ╔═══════════════════════════════════════════════════════════╗
[ ] I (xxx) MAIN: ║   SafeSignal ESP32-S3 Button                              ║
[ ] I (xxx) MAIN: ║   Version: 1.0.0-alpha                                    ║
[ ] I (xxx) MAIN: ╚═══════════════════════════════════════════════════════════╝

[ ] I (xxx) MAIN: [NVS] Initialized
[ ] I (xxx) MAIN: [GPIO] LED configured on GPIO2
[ ] I (xxx) MAIN: [GPIO] Button configured on GPIO0

[ ] I (xxx) ALERT_QUEUE: [QUEUE] Initialized: 0 pending alerts
[ ] I (xxx) WATCHDOG: [WDT] Initialized (timeout: 30 seconds)
[ ] I (xxx) WATCHDOG: [WDT] Monitoring task: button_task
[ ] I (xxx) WATCHDOG: [WDT] Monitoring task: status_task

[ ] I (xxx) WIFI: [WIFI] Connecting to 'SafeSignal-Edge'...
[ ] I (xxx) WIFI: [WIFI] Connected to AP
[ ] I (xxx) WIFI: [WIFI] Got IP address: 192.168.1.XX
[ ] I (xxx) WIFI: [WIFI] Signal strength: -XX dBm

[ ] I (xxx) TIME_SYNC: [TIME] Initialized, waiting for sync...
[ ] I (xxx) TIME_SYNC: [TIME] NTP servers: pool.ntp.org, time.google.com, time.cloudflare.com
[ ] I (xxx) TIME_SYNC: [TIME] Synchronized: 2025-11-02 XX:XX:XX UTC

[ ] I (xxx) MQTT: [MQTT] Client started
[ ] I (xxx) MQTT: [MQTT] Connected to broker

[ ] I (xxx) MAIN: [READY] System initialized
[ ] I (xxx) MAIN: [READY] Press button to trigger alert
```

**Check each box** as you see the message.

**If WiFi fails**:
- Update credentials in `include/config.h`:
  ```c
  #define WIFI_SSID "your-wifi-name"
  #define WIFI_PASS "your-wifi-password"
  ```
- Rebuild: `idf.py build flash`

**If MQTT fails**:
- Check edge gateway running: `docker ps | grep emqx`
- Continue anyway (can test alert queue without MQTT)

**If Time sync fails**:
- Wait 10-20 seconds (NTP can be slow)
- Check internet connectivity
- Continue anyway (timestamps will use boot time)

---

## Step 5: Test Basic Alert ✅

### 5.1 Press Button

**Action**: Press and release the BOOT button on ESP32 (labeled "BOOT" or GPIO0)

**LED should**: Blink rapidly 5 times, then stay on

**Serial output should show**:
```
[ ] W (xxx) MAIN:
[ ] W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert XXXXXX (index 0, 1 pending)
[ ] I (xxx) MQTT: [MQTT] Alert XXXXXX published (msg_id=1)
[ ] I (xxx) MQTT: [MQTT] Alert published immediately
[ ] I (xxx) MAIN: [ALERT] ✓ Alert sent (total: 1)
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Processing 1 pending alerts
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert XXXXXX (retry 0)
[ ] I (xxx) MQTT: [MQTT] Alert XXXXXX published (msg_id=2)
[ ] I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXXX delivered
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Processing complete: 1 delivered, 0 remaining
```

**Check boxes** for what you see.

**✅ SUCCESS**: Alert enqueued → published → delivered
**❌ FAILURE**: Write what happened: _____________________

---

## Step 6: Test Alert Persistence 🔒

### 6.1 Disconnect MQTT

**In another terminal window**:
```bash
cd /Users/mads/dev/repos/collab/safeSignal/safeSignal-mvp/edge
docker stop safesignal-emqx
```

**In serial monitor, should see**:
```
[ ] W (xxx) MQTT: [MQTT] Disconnected from broker
```

---

### 6.2 Queue Alerts

**Action**: Press BOOT button **3 times** (wait 2 seconds between each press)

**Serial output for EACH press**:
```
Press 1:
[ ] W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
[ ] W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert XXXXXX (index 0, 1 pending)
[ ] E (xxx) MAIN: [ALERT] ✗ Alert failed (total failures: 1)

Press 2:
[ ] W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
[ ] W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert XXXXXX (index 1, 2 pending)
[ ] E (xxx) MAIN: [ALERT] ✗ Alert failed (total failures: 2)

Press 3:
[ ] W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
[ ] W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert XXXXXX (index 2, 3 pending)
[ ] E (xxx) MAIN: [ALERT] ✗ Alert failed (total failures: 3)
```

**Note**: Pending count should go 1 → 2 → 3

**Did you see 3 pending?** [ ] Yes [ ] No

---

### 6.3 Reconnect MQTT

**In the other terminal**:
```bash
docker start safesignal-emqx
```

**In serial monitor, within 10 seconds should see**:
```
[ ] I (xxx) MQTT: [MQTT] Connected to broker

[ ] I (xxx) MQTT: [MQTT] Delivered 3 queued alerts on reconnect
[ ] I (xxx) ALERT_QUEUE: [QUEUE] Processing 3 pending alerts

[ ] I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert XXXXXX (retry 0)
[ ] I (xxx) MQTT: [MQTT] Alert XXXXXX published (msg_id=X)
[ ] I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXXX delivered

[ ] I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert XXXXXX (retry 0)
[ ] I (xxx) MQTT: [MQTT] Alert XXXXXX published (msg_id=X)
[ ] I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXXX delivered

[ ] I (xxx) ALERT_QUEUE: [QUEUE] Attempting delivery of alert XXXXXX (retry 0)
[ ] I (xxx) MQTT: [MQTT] Alert XXXXXX published (msg_id=X)
[ ] I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXXX delivered

[ ] I (xxx) ALERT_QUEUE: [QUEUE] Processing complete: 3 delivered, 0 remaining
```

**All 3 alerts delivered?** [ ] Yes [ ] No

**✅ SUCCESS**: Zero alert loss confirmed!
**❌ FAILURE**: Write what happened: _____________________

---

## Step 7: Verify UTC Timestamps 🕐

### 7.1 Check Timestamp in Alert

Look for timestamp in the published alert logs (or check MQTT payload if you have edge gateway logs):

```json
"timestamp": 1730563845
```

### 7.2 Verify It's UTC

**In another terminal**:
```bash
date -u -r 1730563845
```

**Expected**: Should show current UTC time (within 2 seconds of when you pressed button)

**Is timestamp accurate?** [ ] Yes [ ] No [ ] Timestamp was 0 (NTP not synced)

---

## Step 8: Verify Watchdog (Optional) ⏱️

**Action**: Just watch the serial monitor for 1 minute. Don't press anything.

**Expected**: No watchdog timeout messages, device continues running normally

**Did it stay running without resets?** [ ] Yes [ ] No

---

## Test Results Summary

### ✅ Success Criteria

- [ ] Device boots without errors
- [ ] Alert queue initialized (0 pending on startup)
- [ ] Watchdog initialized and monitoring 2 tasks
- [ ] WiFi connected and got IP address
- [ ] MQTT connected to broker
- [ ] NTP synchronized time (or showed "waiting for sync")
- [ ] Button press → immediate alert delivery (when MQTT connected)
- [ ] MQTT disconnect → alerts queued (3 alerts)
- [ ] MQTT reconnect → queued alerts automatically delivered (3 alerts)
- [ ] UTC timestamps accurate (or 0 if NTP not synced)
- [ ] No watchdog timeouts during normal operation

**Count**: ____ out of 11 passed

---

## Issues Found

**Issue 1**: _____________________
**Severity**: [ ] Critical [ ] Major [ ] Minor
**Details**: _____________________

**Issue 2**: _____________________
**Severity**: [ ] Critical [ ] Major [ ] Minor
**Details**: _____________________

---

## Next Steps

### If All Tests Passed (9+ out of 11) ✅

**Congratulations!** Week 1 reliability features are working!

**Next actions**:
1. [ ] Run 24-hour uptime test (leave device running overnight)
2. [ ] Test power cycle persistence (see TESTING.md)
3. [ ] Run automated test suite: `scripts/test_alert_queue.sh`
4. [ ] Proceed to graceful error recovery implementation

---

### If Tests Failed ❌

**Debug steps**:
1. Check which specific test failed
2. Review error messages in serial output
3. Verify hardware connections (USB, button, LED)
4. Check `include/config.h` configuration
5. Try `idf.py erase-flash` and reflash

**Report to developer**:
- Which test failed: _____________________
- Error messages: _____________________
- Serial log excerpt: _____________________

---

## Performance Notes

**Alert latency** (time from button press to MQTT publish):
- Observed: _____ ms
- Target: <100 ms
- Status: [ ] Within spec [ ] Exceeds spec

**Memory usage** (from status messages):
- Free heap: _____ KB
- Minimum heap: _____ KB
- Status: [ ] Stable [ ] Degrading

---

## Test Session Complete

**Duration**: _____ minutes
**Overall Result**: [ ] PASS [ ] FAIL [ ] PARTIAL

**Tester signature**: Mads
**Date**: 2025-11-02

**Save this file for records!**

---

## Quick Command Reference

**Exit monitor**: `Ctrl + ]`

**Rebuild and reflash**:
```bash
idf.py build flash monitor
```

**Erase flash (if needed)**:
```bash
idf.py erase-flash
```

**Check Docker**:
```bash
docker ps | grep emqx
docker start safesignal-emqx
```

**Find serial port**:
```bash
ls /dev/tty.* | grep usb
```
