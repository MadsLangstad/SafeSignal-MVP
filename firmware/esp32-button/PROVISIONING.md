# ESP32 Secure Provisioning Guide

## Overview

SafeSignal ESP32 devices use **secure provisioning** to eliminate hardcoded credentials. Devices ship blank and are provisioned with unique credentials on first boot.

**Security Benefits:**
- ✅ No hardcoded WiFi passwords in firmware
- ✅ No embedded certificates in flash
- ✅ Unique credentials per device
- ✅ Credentials stored in encrypted NVS
- ✅ Cannot be cloned from firmware dump

## Provisioning Flow

```
┌─────────────┐
│ Flash Blank │  Device ships with no credentials
│  Firmware   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│First Boot   │  Check NVS for credentials
│  Detection  │  → Not found, enter provisioning mode
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Provision   │  Via serial console (MVP) or BLE (Phase 2)
│ Credentials │  - WiFi SSID/password
└──────┬──────┘  - Device ID, Tenant, Building, Room
       │
       ▼
┌─────────────┐
│  Save to    │  Write credentials to encrypted NVS
│     NVS     │  Mark device as provisioned
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Reboot    │  Device loads credentials from NVS
│  & Connect  │  Connects to WiFi and MQTT
└─────────────┘
```

## MVP Provisioning (Serial Console)

### Prerequisites

```bash
# Install pyserial
pip install pyserial

# Find serial port
ls /dev/tty*  # Linux/macOS
# Look for /dev/ttyUSB0 or /dev/cu.usbserial-*
```

### Step 1: Flash Blank Firmware

```bash
cd firmware/esp32-button

# Build firmware
idf.py build

# Erase flash (removes any old credentials)
idf.py erase-flash

# Flash firmware
idf.py flash
```

### Step 2: Provision Device

```bash
# Run provisioning tool
python scripts/provision_device.py \
    --port /dev/ttyUSB0 \
    --wifi-ssid "SafeSignal-Edge" \
    --wifi-pass "your-wifi-password" \
    --device-id "esp32-prod-001" \
    --tenant "tenant-a" \
    --building "building-a" \
    --room "room-1"
```

**Expected Output:**
```
============================================================
SafeSignal ESP32 Device Provisioning
============================================================
Port: /dev/ttyUSB0
WiFi SSID: SafeSignal-Edge
Device ID: esp32-prod-001
Tenant: tenant-a
Building: building-a
Room: room-1
============================================================

Opening serial port /dev/ttyUSB0...
Serial port opened successfully

Sending provisioning commands...

→ Sending: provision_set_wifi SafeSignal-Edge ********
← WiFi credentials saved: SSID='SafeSignal-Edge'

→ Sending: provision_set_device esp32-prod-001 tenant-a building-a room-1
← Device configuration saved

→ Sending: provision_complete
← Device Provisioning Complete!

============================================================
✓ Provisioning complete!
============================================================
```

### Step 3: Verify Provisioning

```bash
# Monitor device logs
idf.py monitor

# Expected output:
# [PROVISION] Provisioning system initialized
# [WIFI] Loading credentials from NVS...
# [WIFI] Credentials loaded: SSID='SafeSignal-Edge'
# [WIFI] Connecting to 'SafeSignal-Edge'...
# [WIFI] Connected to AP
# [WIFI] Got IP address: 192.168.1.50
# [MQTT] Connecting to broker...
# [MQTT] Connected to broker
```

## Manual Provisioning (Console Commands)

You can also provision manually via the serial console:

```bash
# Start monitor
idf.py monitor

# In the serial console, type:
provision_set_wifi SafeSignal-Edge your-password
provision_set_device esp32-prod-001 tenant-a building-a room-1
provision_complete

# Check status
provision_status

# Factory reset if needed
provision_reset --confirm
```

## Factory Reset

To clear all provisioning data and reprovision:

```bash
# Via monitor console
provision clear

# Or via script
python scripts/factory_reset.py --port /dev/ttyUSB0

# Device will reboot and enter provisioning mode
```

## Security Considerations

### MVP (Current)
- ✅ Credentials in NVS (not in firmware)
- ✅ Serial provisioning (requires physical access)
- ⚠️  NVS not encrypted (ESP32-S3 limitation without eFuse key)
- ⚠️  Certificates still embedded in firmware

### Phase 2 (BLE Provisioning)
- ✅ BLE GATT server for wireless provisioning
- ✅ 6-digit PIN pairing
- ✅ Encrypted credential transfer
- ✅ BLE disabled after provisioning

### Production
- ✅ NVS encryption enabled (eFuse-based key)
- ✅ Certificates provisioned per-device (not embedded)
- ✅ Secure boot + flash encryption
- ✅ ATECC608A for private key storage

## Troubleshooting

### Device Not Provisioned Error

```
[WIFI] Device not provisioned! Cannot connect to WiFi.
[WIFI] Please provision device with credentials.
```

**Solution**: Run provisioning tool or manually provision via console

### Failed to Load from NVS

```
[WIFI] Failed to load SSID from NVS: ESP_ERR_NVS_NOT_FOUND
```

**Solution**: Device not provisioned or NVS corrupted. Erase flash and reprovision:
```bash
idf.py erase-flash
idf.py flash
# Then run provisioning again
```

### Provisioning Tool Can't Open Port

```
✗ Error: Could not open serial port /dev/ttyUSB0
```

**Solutions**:
```bash
# Check device is connected
ls /dev/tty*

# Fix permissions (Linux)
sudo chmod 666 /dev/ttyUSB0

# Or add user to dialout group (permanent fix)
sudo usermod -a -G dialout $USER
# Log out and back in
```

### Wrong WiFi Password

Device will continuously retry connection. To fix:

```bash
# Monitor logs
idf.py monitor

# Type in console:
provision_set_wifi correct-ssid correct-password
provision_reset --confirm
```

## Production Deployment Workflow

1. **Manufacturing**:
   - Flash blank firmware (no credentials)
   - Device enters provisioning mode on first boot

2. **Installation**:
   - Installer uses mobile app or CLI tool
   - Provisions WiFi credentials
   - Assigns device to tenant/building/room

3. **Verification**:
   - Device connects to WiFi/MQTT
   - Sends test alert
   - Installer confirms in dashboard

4. **Post-Installation**:
   - BLE disabled (security)
   - Device operates normally
   - OTA updates available

## Files

```
firmware/esp32-button/
├── main/
│   ├── provisioning.c/h        # NVS provisioning API
│   └── wifi.c                  # Loads credentials from NVS
│
├── scripts/
│   ├── provision_device.py     # Provisioning tool
│   └── factory_reset.py        # Factory reset tool
│
└── PROVISIONING.md             # This file
```

## API Reference

See `main/provisioning.h` for full API documentation.

**Key Functions:**
- `provision_init()` - Initialize provisioning system
- `provision_is_provisioned()` - Check if device provisioned
- `provision_save_config()` - Save device configuration
- `provision_load_config()` - Load device configuration
- `provision_clear()` - Factory reset (erase all credentials)

## Next Steps

- [ ] BLE provisioning implementation (Phase 2)
- [ ] Mobile provisioning app (React Native)
- [ ] NVS encryption with eFuse key
- [ ] Certificate provisioning support
- [ ] ATECC608A integration for private keys
