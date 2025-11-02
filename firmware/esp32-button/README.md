# SafeSignal ESP32-S3 Button Firmware

Physical panic button firmware for SafeSignal emergency alerting system.

## Hardware Requirements

- **MCU**: ESP32-S3-DevKitC-1 (or compatible ESP32-S3 board)
- **Flash**: 8MB minimum
- **Button**: GPIO0 (BOOT button on DevKit, or external button)
- **LED**: GPIO2 (onboard LED for status indication)
- **Optional**: ATECC608A secure element (I2C on GPIO21/22)

## Features

✅ **WiFi connectivity** with auto-reconnect
✅ **MQTT client** with mTLS authentication
✅ **Button press detection** with hardware debouncing
✅ **Alert publishing** (QoS 1, at-least-once delivery)
✅ **Status reporting** (RSSI, uptime, memory)
✅ **Heartbeat** for edge gateway monitoring
🔄 **OTA updates** (future - Phase 1)
🔄 **ATECC608A integration** (future - Phase 1)
🔄 **Secure boot** (future - Phase 1)

## Development Environment Setup

### Prerequisites

1. **ESP-IDF** (Espressif IoT Development Framework)
   ```bash
   # Install ESP-IDF v5.x
   mkdir -p ~/esp
   cd ~/esp
   git clone --recursive https://github.com/espressif/esp-idf.git
   cd esp-idf
   ./install.sh esp32s3
   . ./export.sh
   ```

2. **Build tools**
   ```bash
   # macOS
   brew install cmake ninja dfu-util

   # Linux (Ubuntu/Debian)
   sudo apt-get install git wget flex bison gperf python3 python3-pip \
       python3-setuptools cmake ninja-build ccache libffi-dev libssl-dev \
       dfu-util libusb-1.0-0
   ```

### Build and Flash

1. **Setup certificates**
   ```bash
   cd certs
   # Copy certificates from edge gateway
   cp ../../edge/certs/ca/ca.crt ./ca.crt
   cp ../../edge/certs/devices/esp32-test.crt ./client.crt
   cp ../../edge/certs/devices/esp32-test.key ./client.key
   ```

2. **Configure project**
   ```bash
   # From firmware/esp32-button directory
   make menuconfig
   ```

   Key settings to verify:
   - Serial flasher config → Flash size → 8 MB
   - Component config → Wi-Fi → Static/dynamic buffers
   - Component config → mbedTLS → Hardware acceleration

3. **Build firmware**
   ```bash
   make -j$(nproc)
   ```

4. **Flash to device**
   ```bash
   # Connect ESP32-S3 via USB
   # Find port: ls /dev/tty* | grep USB
   make flash ESPPORT=/dev/ttyUSB0
   ```

5. **Monitor serial output**
   ```bash
   make monitor ESPPORT=/dev/ttyUSB0
   ```

   Press `Ctrl+]` to exit monitor.

## Configuration

Edit `include/config.h` to customize:

### Network Configuration
```c
#define WIFI_SSID "SafeSignal-Edge"
#define WIFI_PASS "safesignal-dev"
#define MQTT_BROKER_URI "mqtts://edge-gateway.local:8883"
```

### Device Identity
```c
#define DEVICE_ID "esp32-dev-001"      // Unique per device
#define TENANT_ID "tenant-a"
#define BUILDING_ID "building-a"
#define ROOM_ID "room-1"
```

### Hardware Pins
```c
#define BUTTON_PIN 0                    // GPIO0 (BOOT button)
#define LED_PIN 2                       // GPIO2 (onboard LED)
#define I2C_SDA_PIN 21                  // For ATECC608A
#define I2C_SCL_PIN 22
```

## Testing

### Manual Button Test
1. Flash firmware and open serial monitor
2. Press BOOT button (GPIO0)
3. Observe LED blinking rapidly
4. Check serial output for alert publication
5. Verify MQTT message on edge gateway:
   ```bash
   # On edge gateway
   docker logs safesignal-policy-service | grep "Alert received"
   ```

### Expected Serial Output
```
[MAIN] SafeSignal ESP32-S3 Button v1.0.0-alpha
[WIFI] Connected to AP
[WIFI] Got IP address: 192.168.1.50
[WIFI] Signal strength: -45 dBm
[MQTT] Connected to broker
[READY] Press button to trigger alert

[BUTTON] *** PANIC BUTTON PRESSED ***
[MQTT] Alert published (msg_id=1)
[ALERT] ✓ Alert sent (total: 1)
```

### MQTT Topics

**Published by device:**
- `safesignal/{tenant}/{building}/alerts/trigger` - Alert events (QoS 1)
- `safesignal/{tenant}/{building}/device/status` - Device status (QoS 0)
- `safesignal/{tenant}/{building}/device/heartbeat` - Heartbeat (QoS 0)

**Subscribed (future):**
- `safesignal/{tenant}/{building}/device/command` - OTA, config updates

## Alert Payload

```json
{
  "alertId": "ESP32-esp32-dev-001-123456",
  "deviceId": "esp32-dev-001",
  "tenantId": "tenant-a",
  "buildingId": "building-a",
  "sourceRoomId": "room-1",
  "mode": 1,
  "origin": "ESP32",
  "timestamp": 123456,
  "version": "1.0.0-alpha"
}
```

**Alert Modes:**
- `0` - SILENT (no local audio)
- `1` - AUDIBLE (standard alarm)
- `2` - LOCKDOWN (security lockdown)
- `3` - EVACUATION (emergency evacuation)

## Project Structure

```
esp32-button/
├── Makefile                    # Main build file
├── partitions.csv              # Flash partition table
├── sdkconfig.defaults          # Default configuration
├── README.md                   # This file
│
├── main/                       # Application code
│   ├── component.mk            # Component build config
│   ├── main.c                  # Application entry point
│   ├── wifi.c / wifi.h         # WiFi connection management
│   ├── mqtt.c / mqtt.h         # MQTT client with mTLS
│   └── button.c / button.h     # Button interrupt handling
│
├── include/                    # Header files
│   └── config.h                # Configuration constants
│
└── certs/                      # TLS certificates (not committed)
    ├── ca.crt                  # CA certificate
    ├── client.crt              # Device certificate
    └── client.key              # Device private key
```

## Troubleshooting

### WiFi Connection Fails
```
[WIFI] Disconnected, reconnecting...
```
- Check SSID/password in `config.h`
- Verify WiFi AP is running (2.4GHz required for ESP32)
- Check signal strength (should be > -70 dBm)

### MQTT Connection Fails
```
[MQTT] Connection failed, rc=-1
```
- Verify edge gateway is running: `docker ps | grep emqx`
- Check MQTT broker URI in `config.h`
- Ensure certificates are valid and not expired:
  ```bash
  openssl x509 -in certs/client.crt -noout -enddate
  ```
- Test with mosquitto_pub:
  ```bash
  mosquitto_pub -h edge-gateway.local -p 8883 --cafile certs/ca.crt \
    --cert certs/client.crt --key certs/client.key -t test -m "hello"
  ```

### Button Not Responding
- Check GPIO0 is not shorted or damaged
- Verify button interrupt is attached (check serial log)
- Test with manual wire connection: short GPIO0 to GND briefly

### Build Errors
```
fatal error: mqtt_client.h: No such file or directory
```
- Ensure ESP-IDF is sourced: `. ~/esp/esp-idf/export.sh`
- Clean and rebuild: `make clean && make`

### Flash Errors
```
A fatal error occurred: Failed to connect to ESP32
```
- Hold BOOT button while flashing
- Try different USB cable/port
- Check device permissions: `sudo chmod 666 /dev/ttyUSB0`

## Performance Metrics

| Metric | Target | Typical |
|--------|--------|---------|
| Alert latency (button → MQTT) | <100ms | 45-80ms |
| WiFi reconnect time | <10s | 3-5s |
| MQTT reconnect time | <5s | 1-2s |
| Power consumption (active) | <500mA | 150-300mA |
| Power consumption (idle) | <100mA | 50-80mA |

## Security Considerations

### Development (Current)
- ⚠️ Hardcoded WiFi credentials
- ⚠️ Certificates embedded in firmware binary
- ⚠️ No secure boot or flash encryption
- ⚠️ TLS verification can be disabled for testing

### Production Requirements (Phase 5)
- ✅ Secure boot enabled (prevents unsigned firmware)
- ✅ Flash encryption enabled (protects firmware and data)
- ✅ Private key stored in ATECC608A (not in flash)
- ✅ Certificate provisioned per-device during factory programming
- ✅ SPIFFE/SPIRE for certificate rotation
- ✅ WiFiManager for secure credential input (no hardcoding)

## Next Steps (Phase 1 Completion)

- [ ] Add ATECC608A secure element support
- [ ] Implement OTA firmware updates with signature verification
- [ ] Enable secure boot and flash encryption
- [ ] Add battery monitoring (if external power supply)
- [ ] Implement deep sleep mode for battery operation
- [ ] Create hardware BOM and assembly instructions
- [ ] Design custom PCB (move away from DevKit)

## License

Proprietary - SafeSignal Emergency Systems

## Support

For issues or questions:
- Check logs: `make monitor`
- Review implementation plan: `../../docs/IMPLEMENTATION-PLAN.md`
- Edge gateway docs: `../../edge/README.md`
