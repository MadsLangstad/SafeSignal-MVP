#!/bin/bash

# SafeSignal Mobile - Auto IP Detection Startup Script
# This script automatically detects your local IP and updates the mobile app configuration

set -e

echo "🔍 Detecting local network IP address..."

# Get the IP address from our Node.js script
LOCAL_IP=$(node "$(dirname "$0")/get-local-ip.js")

if [ -z "$LOCAL_IP" ] || [ "$LOCAL_IP" = "localhost" ]; then
    echo "⚠️  Warning: Could not detect local IP, using localhost"
    LOCAL_IP="localhost"
fi

echo "✅ Detected IP: $LOCAL_IP"

# Update app.json with the detected IP
echo "📝 Updating app.json..."
BACKEND_URL="http://${LOCAL_IP}:5118"

# Use sed to update app.json (macOS compatible)
sed -i '' "s|\"apiUrl\": \"http://[^\"]*\"|\"apiUrl\": \"${BACKEND_URL}\"|g" "$(dirname "$0")/../app.json"

echo "✅ Configuration updated: ${BACKEND_URL}"

# Start Expo with cache cleared
echo "🚀 Starting Expo dev server with cleared cache..."
cd "$(dirname "$0")/.."
npx expo start --clear

echo "✨ Mobile app started with auto-detected IP: $LOCAL_IP"
