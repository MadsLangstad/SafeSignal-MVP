#!/bin/bash
#
# ESP-IDF Setup Script
# Sources ESP-IDF environment and provides build shortcuts
#

set -e

echo "╔═══════════════════════════════════════════════════╗"
echo "║  SafeSignal ESP32-S3 - ESP-IDF Setup              ║"
echo "╚═══════════════════════════════════════════════════╝"
echo ""

# Try common ESP-IDF installation locations
IDF_LOCATIONS=(
    "$HOME/esp/esp-idf"
    "$HOME/.espressif/esp-idf"
    "/opt/esp-idf"
    "$HOME/esp-idf"
)

IDF_FOUND=0

for location in "${IDF_LOCATIONS[@]}"; do
    if [ -f "$location/export.sh" ]; then
        echo "✓ Found ESP-IDF at: $location"
        export IDF_PATH="$location"

        echo "✓ Sourcing ESP-IDF environment..."
        . "$location/export.sh"

        IDF_FOUND=1
        break
    fi
done

if [ $IDF_FOUND -eq 0 ]; then
    echo "✗ ESP-IDF not found in common locations:"
    for location in "${IDF_LOCATIONS[@]}"; do
        echo "  - $location"
    done
    echo ""
    echo "Please install ESP-IDF:"
    echo "  mkdir -p ~/esp"
    echo "  cd ~/esp"
    echo "  git clone --recursive https://github.com/espressif/esp-idf.git"
    echo "  cd esp-idf"
    echo "  ./install.sh esp32s3"
    echo "  . ./export.sh"
    exit 1
fi

echo ""
echo "ESP-IDF environment ready!"
echo "IDF_PATH: $IDF_PATH"
echo ""
echo "Available commands:"
echo "  idf.py build          - Build firmware"
echo "  idf.py flash          - Flash to device"
echo "  idf.py monitor        - Monitor serial output"
echo "  idf.py build flash monitor - Do all three"
echo ""
echo "Run this in your current shell:"
echo "  source setup_idf.sh"
echo ""
