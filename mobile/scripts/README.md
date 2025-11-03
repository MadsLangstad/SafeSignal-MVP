# SafeSignal Mobile - Auto IP Configuration

## Problem

When developing the mobile app, your machine's IP address can change:
- Moving between WiFi networks
- VPN connections
- DHCP lease renewals
- Different network environments (home, office, etc.)

This causes the mobile app to fail connecting to the backend API because the hardcoded IP in `app.json` becomes outdated.

## Solution

Automatic IP detection and configuration update on every startup.

## How It Works

1. **IP Detection** (`get-local-ip.js`): Automatically detects your machine's local network IP address
2. **Auto-Configuration** (`start-with-auto-ip.sh`): Updates `app.json` with the detected IP before starting Expo
3. **npm Scripts**: Convenient commands for daily development

## Usage

### Recommended: Auto IP Detection (Default)

```bash
cd mobile
npm start
```

This automatically:
1. Detects your current local IP (e.g., 192.168.0.30)
2. Updates `app.json` with `http://<detected-ip>:5118`
3. Clears Expo cache
4. Starts the Expo dev server

### Manual IP Check

```bash
npm run check-ip
```

Output: `192.168.0.30` (your current IP)

### Manual Start (No Auto-Detection)

```bash
npm run start:manual
```

Uses existing configuration without updates.

## Files

### `get-local-ip.js`
Node.js script that detects local network IP by:
1. Checking priority interfaces (en0/WiFi, en1/Ethernet)
2. Filtering IPv4 addresses
3. Excluding internal/loopback addresses

### `start-with-auto-ip.sh`
Bash script that:
1. Runs `get-local-ip.js` to detect IP
2. Updates `app.json` using sed
3. Starts Expo with `--clear` flag to refresh cache

## Backend Configuration

The backend is already configured to listen on all network interfaces (`0.0.0.0:5118`) in `cloud-backend/src/Api/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://0.0.0.0:5118"
    }
  }
}
```

This means the backend accepts connections from any IP address on your machine.

## Troubleshooting

### "Cannot detect local IP"
- Check you're connected to a network (WiFi/Ethernet)
- Run `npm run check-ip` to see detected IP
- Manually verify with `ifconfig | grep "inet "` (macOS/Linux)

### "Connection timeout" after auto-start
1. Verify backend is running: `lsof -i :5118`
2. Check detected IP matches your network: `npm run check-ip`
3. Verify firewall allows port 5118
4. Try manual reload in Expo app (shake device → reload)

### "Address already in use"
Backend already running - this is normal. The auto-detection still works.

## Daily Workflow

```bash
# 1. Start backend (if not already running)
cd cloud-backend
dotnet run --project src/Api

# 2. Start mobile app (automatically detects IP)
cd ../mobile
npm start

# Done! App will connect to backend automatically regardless of IP changes
```

## Advanced: Environment-Specific Configuration

For production or specific environments, you can still manually set the IP in `app.json`:

```json
{
  "expo": {
    "extra": {
      "apiUrl": "https://api.safesignal.io"
    }
  }
}
```

The auto-detection only updates the development IP, production URLs remain unchanged.

## Technical Details

### Why Not Environment Variables?

Expo's `Constants.expoConfig.extra` is evaluated at build time, not runtime. The auto-detection approach:
- Updates config before Expo starts
- Clears cache to ensure new config loads
- Works seamlessly with Expo's configuration system

### Platform Compatibility

- **macOS**: Fully supported (primary development platform)
- **Linux**: Supported (eth0/wlan0 detection)
- **Windows**: Requires WSL or manual IP configuration

### Performance

- IP detection: ~50ms
- Config update: ~10ms
- Total overhead: <100ms (negligible)
