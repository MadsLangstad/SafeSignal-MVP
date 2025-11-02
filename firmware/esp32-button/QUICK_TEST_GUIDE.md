# ESP32-S3 Quick Test Guide

**5-Minute Verification** | Week 1 Reliability Features

---

## Prerequisites Checklist

- [ ] ESP32-S3-DevKitC-1 connected via USB
- [ ] ESP-IDF environment sourced (`. ~/esp/esp-idf/export.sh`)
- [ ] Edge gateway running (`docker ps | grep emqx`)
- [ ] WiFi credentials configured in `include/config.h`

---

## Step 1: Build & Flash (2 minutes)

```bash
cd firmware/esp32-button

# Build
idf.py build          # OR: make -j$(nproc)

# Flash & Monitor
idf.py -p /dev/ttyUSB0 flash monitor    # OR: make flash monitor ESPPORT=/dev/ttyUSB0
```

**Find your port**:
- macOS: `ls /dev/tty.* | grep usb`
- Linux: `ls /dev/ttyUSB*`

---

## Step 2: Verify Startup (30 seconds)

**Look for these lines** in serial output:

```
✅ I (xxx) ALERT_QUEUE: [QUEUE] Initialized: 0 pending alerts
✅ I (xxx) WATCHDOG: [WDT] Initialized (timeout: 30 seconds)
✅ I (xxx) WIFI: [WIFI] Got IP address: 192.168.1.X
✅ I (xxx) MQTT: [MQTT] Connected to broker
✅ I (xxx) TIME_SYNC: [TIME] Synchronized: 2025-11-02 XX:XX:XX UTC
✅ I (xxx) MAIN: [READY] Press button to trigger alert
```

**If missing**:
- Alert Queue → Check NVS partition table
- Watchdog → Check watchdog.c compiled
- WiFi → Update credentials in config.h
- MQTT → Check edge gateway: `docker start safesignal-emqx`
- Time → Wait 10s more, or check internet connection

---

## Step 3: Test Basic Alert (30 seconds)

**Action**: Press BOOT button (GPIO0) on ESP32

**Expected Output**:
```
W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert 123456
I (xxx) MQTT: [MQTT] Alert 123456 published (msg_id=1)
I (xxx) MAIN: [ALERT] ✓ Alert sent (total: 1)
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert 123456 delivered
```

**LED behavior**: Blinks 5 times rapidly, then stays on

**✅ PASS**: Alert enqueued → published → delivered
**❌ FAIL**: No alert or errors → Check button GPIO0 connection

---

## Step 4: Test Alert Persistence (2 minutes)

### 4A: Disconnect MQTT

```bash
# In another terminal
docker stop safesignal-emqx
```

**Expected**: `W (xxx) MQTT: [MQTT] Disconnected from broker`

### 4B: Queue Alerts

**Action**: Press BOOT button **3 times** (wait 2s between presses)

**Expected Output** (each press):
```
W (xxx) MAIN: [BUTTON] *** PANIC BUTTON PRESSED ***
W (xxx) MQTT: [MQTT] Not connected, alert queued for delivery
I (xxx) ALERT_QUEUE: [QUEUE] Enqueued alert XXXXX (index X, Y pending)
```

**Verify**: Count goes from 1 → 2 → 3 pending

### 4C: Reconnect MQTT

```bash
docker start safesignal-emqx
```

**Expected Output** (within 10 seconds):
```
I (xxx) MQTT: [MQTT] Connected to broker
I (xxx) MQTT: [MQTT] Delivered 3 queued alerts on reconnect
I (xxx) ALERT_QUEUE: [QUEUE] Processing 3 pending alerts
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXX delivered
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXX delivered
I (xxx) ALERT_QUEUE: [QUEUE] ✓ Alert XXXXX delivered
I (xxx) ALERT_QUEUE: [QUEUE] Processing complete: 3 delivered, 0 remaining
```

**✅ PASS**: All 3 alerts automatically delivered
**❌ FAIL**: Alerts not delivered → Check MQTT reconnect logic

---

## Step 5: Verify UTC Timestamps (30 seconds)

**In the alert logs, find**:
```json
"timestamp": 1730563845
```

**Verify**:
```bash
date -u -r 1730563845
# Should show current UTC time (within 2 seconds)
```

**✅ PASS**: Timestamp matches current UTC
**❌ FAIL**: Timestamp is 0 or boot time → NTP not synced

---

## Quick Reference: What Should Happen

| Action | Expected Result | Feature Tested |
|--------|----------------|----------------|
| Boot device | All subsystems initialize | Basic functionality |
| Press button | Alert sent immediately | MQTT publish |
| Disconnect MQTT, press button | Alert queued in NVS | Alert persistence |
| Reconnect MQTT | Queued alerts delivered | Auto-retry |
| Check timestamp | UTC time accurate | NTP sync |
| Wait 35 seconds idle | No watchdog reset | Watchdog normal operation |

---

## Common Issues

### Issue: Build fails with "alert_queue.h not found"

**Fix**: Ensure files are in correct location:
```bash
ls main/alert_queue.c main/alert_queue.h
ls main/watchdog.c main/watchdog.h
ls main/time_sync.c main/time_sync.h
```

If missing, code wasn't created properly.

---

### Issue: "ESP_ERR_NVS_NO_FREE_PAGES"

**Fix**: Erase flash and reflash:
```bash
idf.py erase-flash
idf.py flash
# OR
make erase_flash && make flash
```

---

### Issue: Alerts not queued

**Check logs for**:
```
E (xxx) ALERT_QUEUE: [QUEUE] Not initialized
```

**Fix**: Verify `alert_queue_init()` called in `app_main()`

---

### Issue: Watchdog timeout unexpectedly

```
E (30000) task_wdt: Task watchdog got triggered
```

**Debug**:
1. Increase timeout in `config.h`:
   ```c
   #define WATCHDOG_TIMEOUT_SECONDS 60
   ```
2. Rebuild and reflash
3. Check which task timed out in error message

---

## Success Criteria

### ✅ All Systems Green

- [x] Device boots without errors
- [x] WiFi connects and gets IP
- [x] MQTT connects to broker
- [x] NTP syncs time
- [x] Button press → immediate alert delivery
- [x] MQTT disconnect → alerts queue
- [x] MQTT reconnect → queued alerts deliver
- [x] UTC timestamps accurate
- [x] No watchdog resets during normal operation

**If all checked**: Week 1 features working! 🎉

---

## Next Steps After Success

1. **Power Cycle Test** (5 min)
   - Disconnect MQTT
   - Press button 3 times
   - Unplug USB (power off)
   - Plug in USB (power on)
   - Reconnect MQTT
   - Verify 3 alerts delivered

2. **24-Hour Test** (automated)
   - Leave running overnight
   - Check for crashes/resets in morning
   - Verify memory stable

3. **Stress Test** (30 min)
   - Run `scripts/test_alert_queue.sh`
   - Follow automated test procedures

---

## Exit Monitor

Press `Ctrl + ]` to exit serial monitor

---

## Get Full Test Suite

See `TESTING.md` for comprehensive test procedures (20+ tests)

---

## Help

**Can't find serial port?**
```bash
# macOS
ls /dev/tty.* | grep -i usb

# Linux
dmesg | tail | grep tty
```

**Docker not starting?**
```bash
cd ../../edge
docker-compose up -d
docker ps | grep emqx
```

**ESP-IDF not found?**
```bash
cd ~/esp/esp-idf
. ./export.sh
cd - # Return to firmware directory
```

---

**Time to complete**: 5 minutes
**Success rate**: Should be 100% if hardware functional

**Document Version**: 1.0
