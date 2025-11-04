# SafeSignal Console Commands

Interactive console commands for device provisioning and configuration via UART serial connection.

## Overview

When a device is not provisioned, it automatically starts an interactive console that allows manual configuration via serial connection. This provides a convenient way to provision devices during development, testing, or factory operations.

## Console Access

**Connection:**
- Baud rate: 115200
- Data: 8 bits
- Parity: None
- Stop bits: 1
- Flow control: None

**Tools:**
```bash
# ESP-IDF monitor (recommended)
idf.py monitor

# Screen
screen /dev/ttyUSB0 115200

# Minicom
minicom -D /dev/ttyUSB0 -b 115200
```

## Available Commands

### `provision_status`

Display current provisioning status and configuration.

**Usage:**
```
safesignal> provision_status
```

**Output:**
```
╔═══════════════════════════════════════════════════════════╗
║   SafeSignal Provisioning Status                          ║
╚═══════════════════════════════════════════════════════════╝

Status: PROVISIONED ✓

Configuration:
  WiFi SSID:    MyNetwork
  WiFi Pass:    [HIDDEN]
  Device ID:    button-001
  Tenant ID:    acme
  Building ID:  bldg-5
  Room ID:      rm-201

Certificates: PRESENT ✓
```

---

### `provision_set_wifi`

Configure WiFi credentials (SSID and password).

**Usage:**
```
safesignal> provision_set_wifi <ssid> <password>
```

**Examples:**
```bash
# WPA2 network
safesignal> provision_set_wifi "MyNetwork" "MyPassword123"

# Open network (no password)
safesignal> provision_set_wifi "OpenWiFi" ""

# Network with spaces
safesignal> provision_set_wifi "Guest Network" "welcome123"
```

**Validation:**
- SSID: 1-31 characters
- Password: 0-63 characters (empty = open network)

---

### `provision_set_device`

Configure device metadata (device ID, tenant ID, building ID, room ID).

**Usage:**
```
safesignal> provision_set_device <device_id> <tenant_id> <building_id> <room_id>
```

**Examples:**
```bash
# Configure device metadata
safesignal> provision_set_device button-001 acme bldg-5 rm-201

# Another example
safesignal> provision_set_device esp32-panic-btn-42 tenant123 building-a room-305
```

**Validation:**
- Device ID: 1-31 characters (required)
- Tenant ID: 0-15 characters
- Building ID: 0-15 characters
- Room ID: 0-15 characters

---

### `provision_complete`

Mark the device as provisioned after configuration is complete.

**Usage:**
```
safesignal> provision_complete
```

**Requirements:**
- WiFi SSID must be configured
- Device ID must be configured

**Output:**
```
╔═══════════════════════════════════════════════════════════╗
║   Device Provisioning Complete!                           ║
╚═══════════════════════════════════════════════════════════╝

Device is now provisioned and will use stored credentials.
Reboot the device for changes to take effect.
```

**Note:** After marking as complete, you must reboot the device for normal operation to start.

---

### `provision_reset`

Perform factory reset - erase all provisioning data.

**Usage:**
```
safesignal> provision_reset --confirm
```

**⚠️ Warning:** This will permanently erase:
- WiFi credentials
- Device configuration
- TLS certificates
- Provisioned flag

**Output:**
```
╔═══════════════════════════════════════════════════════════╗
║   Factory Reset Complete                                  ║
╚═══════════════════════════════════════════════════════════╝

All provisioning data has been erased.
Device will require reprovisioning on next boot.
```

---

### `provision_get`

Get a specific provisioning value by key.

**Usage:**
```
safesignal> provision_get <key>
```

**Available Keys:**
- `wifi_ssid` - WiFi network name
- `wifi_pass` - WiFi password (hidden)
- `device_id` - Device identifier
- `tenant_id` - Tenant identifier
- `building_id` - Building identifier
- `room_id` - Room identifier

**Examples:**
```bash
safesignal> provision_get device_id
device_id: button-001

safesignal> provision_get wifi_ssid
wifi_ssid: MyNetwork

safesignal> provision_get wifi_pass
wifi_pass: [HIDDEN]
```

---

## Complete Provisioning Workflow

### Interactive Console Provisioning

1. **Connect to device via serial console**
   ```bash
   idf.py monitor
   ```

2. **Check current status**
   ```
   safesignal> provision_status
   ```

3. **Configure WiFi credentials**
   ```
   safesignal> provision_set_wifi "MyNetwork" "MyPassword123"
   ```

4. **Configure device metadata**
   ```
   safesignal> provision_set_device button-001 acme bldg-5 rm-201
   ```

5. **Verify configuration**
   ```
   safesignal> provision_status
   ```

6. **Mark as complete**
   ```
   safesignal> provision_complete
   ```

7. **Reboot device**
   ```bash
   # Press reset button, or from console:
   Ctrl+] (to exit monitor)
   idf.py flash monitor
   ```

### Python Tool Provisioning

Alternatively, use the automated Python provisioning tool:

```bash
cd firmware/esp32-button/scripts
python3 provision_device.py
```

The tool will:
- Auto-detect serial port
- Prompt for all configuration values
- Validate inputs
- Send commands to device
- Verify provisioning success

See [scripts/provision_device.py](scripts/provision_device.py) for details.

---

## Troubleshooting

### Console Not Responding

**Symptoms:** No prompt, characters not echoing

**Solutions:**
1. Check baud rate is 115200
2. Verify correct serial port (`ls /dev/tty*`)
3. Check USB cable supports data (not just power)
4. Try resetting device (press reset button)
5. Ensure device is in provisioning mode (not yet provisioned)

### Configuration Not Saved

**Symptoms:** Settings lost after reboot

**Solutions:**
1. Verify commands returned success (no error messages)
2. Run `provision_status` to confirm values were saved
3. Ensure you called `provision_complete` after configuration
4. Check NVS partition is not corrupted (see logs)

### Cannot Complete Provisioning

**Symptoms:** `provision_complete` fails with error

**Solutions:**
1. Ensure WiFi SSID is configured: `provision_get wifi_ssid`
2. Ensure Device ID is configured: `provision_get device_id`
3. Check error message for specific missing field

### Device Won't Connect to WiFi

**Symptoms:** WiFi connection fails after provisioning

**Solutions:**
1. Verify SSID is correct: `provision_get wifi_ssid`
2. Check password was entered correctly (try reprovisioning)
3. Ensure network is 2.4GHz (ESP32 doesn't support 5GHz)
4. Check WiFi network is broadcasting SSID (not hidden)
5. Verify network security is WPA2/WPA3 or Open

---

## Security Considerations

### Encrypted Storage

All provisioning data is stored in encrypted NVS:
- Encryption keys derived from eFuse
- Credentials never hardcoded in firmware
- Each device has unique encryption

### Serial Access Control

**Production Security:**
- Disable UART console in production builds
- Use hardware write-protect on provisioning partition
- Physically secure devices during provisioning

**Development Security:**
- Console requires physical access (UART serial)
- Passwords hidden in `provision_status` output
- Sensitive values hidden in `provision_get` output

### Factory Reset Protection

Factory reset requires explicit confirmation:
```
safesignal> provision_reset --confirm
```

This prevents accidental data loss from mistyped commands.

---

## Advanced Usage

### Batch Provisioning Script

For provisioning multiple devices:

```bash
#!/bin/bash
# batch_provision.sh

DEVICES=(
  "button-001:acme:bldg-5:rm-201"
  "button-002:acme:bldg-5:rm-202"
  "button-003:acme:bldg-5:rm-203"
)

for device in "${DEVICES[@]}"; do
  IFS=':' read -r dev_id tenant bldg room <<< "$device"

  echo "Provisioning $dev_id..."
  python3 provision_device.py \
    --device-id "$dev_id" \
    --tenant-id "$tenant" \
    --building-id "$bldg" \
    --room-id "$room" \
    --wifi-ssid "FactoryWiFi" \
    --wifi-pass "FactoryPass123"

  echo "Waiting for next device..."
  read -p "Press Enter when next device is connected..."
done
```

### Custom Configuration Keys

To store additional configuration values:

```c
// In your application code
provision_set_string("custom_key", "custom_value");

// Retrieve later
char value[64];
provision_get_string("custom_key", value, sizeof(value));
```

---

## Related Documentation

- [PROVISIONING.md](PROVISIONING.md) - NVS provisioning system design
- [scripts/provision_device.py](scripts/provision_device.py) - Python provisioning tool
- [main/provisioning.h](main/provisioning.h) - Provisioning API reference
