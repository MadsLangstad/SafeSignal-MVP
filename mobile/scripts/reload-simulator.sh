#!/bin/bash

# SafeSignal Mobile - Quick Simulator Reload Script
# Reloads the app in iOS Simulator using AppleScript

echo "🔄 Reloading SafeSignal app in iOS Simulator..."

# Send Command+R to iOS Simulator to reload
osascript <<EOF
tell application "Simulator"
    activate
end tell

tell application "System Events"
    keystroke "r" using command down
end tell
EOF

echo "✅ Reload command sent to simulator"
echo "💡 If that didn't work, try manually:"
echo "   1. Click on Simulator window"
echo "   2. Press Cmd+R to reload"
echo "   3. Or press Cmd+Ctrl+Z to shake, then tap 'Reload'"
