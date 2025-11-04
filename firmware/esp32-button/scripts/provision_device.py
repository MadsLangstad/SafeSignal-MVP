#!/usr/bin/env python3
"""
SafeSignal ESP32 Device Provisioning Tool

Provisions ESP32 devices with WiFi credentials and device configuration.
Uses serial console interface to write credentials to NVS.

Usage:
    python provision_device.py --port /dev/ttyUSB0 \
        --wifi-ssid "SafeSignal-Edge" \
        --wifi-pass "password123" \
        --device-id "esp32-prod-001" \
        --tenant "tenant-a" \
        --building "building-a" \
        --room "room-1"
"""

import argparse
import sys
import time
import serial

def send_command(ser, command, wait_for_response=True):
    """Send command to ESP32 and wait for response"""
    print(f"→ Sending: {command}")
    ser.write((command + '\n').encode())
    ser.flush()

    if wait_for_response:
        time.sleep(0.5)
        while ser.in_waiting:
            response = ser.readline().decode('utf-8', errors='ignore').strip()
            if response:
                print(f"← {response}")

def provision_device(port, wifi_ssid, wifi_pass, device_id, tenant_id, building_id, room_id):
    """Provision device with credentials via serial console"""

    print("=" * 60)
    print("SafeSignal ESP32 Device Provisioning")
    print("=" * 60)
    print(f"Port: {port}")
    print(f"WiFi SSID: {wifi_ssid}")
    print(f"Device ID: {device_id}")
    print(f"Tenant: {tenant_id}")
    print(f"Building: {building_id}")
    print(f"Room: {room_id}")
    print("=" * 60)

    try:
        # Open serial connection
        print(f"\nOpening serial port {port}...")
        ser = serial.Serial(port, 115200, timeout=1)
        time.sleep(2)  # Wait for connection

        print("Serial port opened successfully")
        print("\nNOTE: This tool requires firmware with provisioning console commands")
        print("      Build with: idf.py build flash monitor")
        print()

        # Send provisioning commands
        print("Sending provisioning commands...")
        print()

        # Set WiFi credentials
        send_command(ser, f"provision_set_wifi {wifi_ssid} {wifi_pass}")

        # Set device configuration
        send_command(ser, f"provision_set_device {device_id} {tenant_id} {building_id} {room_id}")

        # Mark as provisioned
        send_command(ser, "provision_complete")

        # Verify provisioning
        print("\nVerifying provisioning...")
        send_command(ser, "provision_status")

        print("\n" + "=" * 60)
        print("✓ Provisioning complete!")
        print("=" * 60)
        print("\nNext steps:")
        print("  1. Reset the device: provision reset")
        print("  2. Device will connect to WiFi and MQTT")
        print("  3. Monitor logs: idf.py monitor")
        print()

        ser.close()
        return 0

    except serial.SerialException as e:
        print(f"\n✗ Error: Could not open serial port {port}")
        print(f"  {str(e)}")
        print("\nTroubleshooting:")
        print("  - Check device is connected")
        print("  - Verify port name (ls /dev/tty*)")
        print("  - Check permissions (sudo chmod 666 /dev/ttyUSB0)")
        return 1
    except KeyboardInterrupt:
        print("\n\n✗ Provisioning cancelled by user")
        return 1
    except Exception as e:
        print(f"\n✗ Error during provisioning: {str(e)}")
        return 1

def main():
    parser = argparse.ArgumentParser(
        description='Provision SafeSignal ESP32 device with credentials',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Basic provisioning
  python provision_device.py --port /dev/ttyUSB0 \\
      --wifi-ssid "SafeSignal-Edge" \\
      --wifi-pass "password123" \\
      --device-id "esp32-prod-001" \\
      --tenant "tenant-a" \\
      --building "building-a" \\
      --room "room-1"

  # macOS
  python provision_device.py --port /dev/cu.usbserial-0001 \\
      --wifi-ssid "MyNetwork" --wifi-pass "secret" \\
      --device-id "esp32-office-001" --tenant "acme" \\
      --building "hq" --room "lobby"
"""
    )

    parser.add_argument('--port', required=True,
                        help='Serial port (e.g., /dev/ttyUSB0, COM3)')
    parser.add_argument('--wifi-ssid', required=True,
                        help='WiFi SSID')
    parser.add_argument('--wifi-pass', required=True,
                        help='WiFi password')
    parser.add_argument('--device-id', required=True,
                        help='Unique device ID (e.g., esp32-prod-001)')
    parser.add_argument('--tenant', required=True,
                        help='Tenant ID (e.g., tenant-a)')
    parser.add_argument('--building', required=True,
                        help='Building ID (e.g., building-a)')
    parser.add_argument('--room', required=True,
                        help='Room ID (e.g., room-1)')

    args = parser.parse_args()

    # Validate inputs
    if len(args.wifi_ssid) > 32:
        print("✗ Error: WiFi SSID must be ≤32 characters")
        return 1

    if len(args.wifi_pass) > 64:
        print("✗ Error: WiFi password must be ≤64 characters")
        return 1

    if len(args.device_id) > 32:
        print("✗ Error: Device ID must be ≤32 characters")
        return 1

    # Run provisioning
    return provision_device(
        args.port,
        args.wifi_ssid,
        args.wifi_pass,
        args.device_id,
        args.tenant,
        args.building,
        args.room
    )

if __name__ == '__main__':
    sys.exit(main())
