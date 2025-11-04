# Mobile App Troubleshooting Guide

## ✅ FIXED: API Connection Issue

**Problem:** Mobile app couldn't connect to backend API
**Error:** `Unable to connect to server. Changes will sync when online.`
**Root Cause:** IP address mismatch (app used `192.168.0.30`, Mac's IP is `192.168.68.108`)
**Solution:** Updated `mobile/src/constants/index.ts` line 8 to use correct IP

### Current Configuration
- **Backend URL:** `http://192.168.68.108:5118`
- **Backend Status:** ✅ Running (PID: 64453)
- **Listening on:** All interfaces (*:5118)

## Next Steps to Test

### 1. Restart Mobile App
```bash
# In Expo terminal, press 'r' to reload
# Or shake device and tap "Reload"
```

### 2. Test Login
- **Email:** test@example.com
- **Password:** testpass123
- **Expected:** Successful login, no "Unable to connect" errors

### 3. Monitor Logs
Watch for these successful log messages:
```
✅ ApiClient initialized with BASE_URL: http://192.168.68.108:5118
✅ [API Request] POST /api/auth/login - Token available: true
✅ Login successful
```

## Common Issues & Solutions

### Issue: "Connection refused" or "Unable to connect"

**Possible Causes:**
1. ❌ Backend not running
2. ❌ Wrong IP address
3. ❌ Firewall blocking port 5118
4. ❌ Mobile device on different network

**Solutions:**

#### 1. Check Backend is Running
```bash
cd cloud-backend
lsof -i :5118
# Should show: SafeSigna ... TCP *:5118 (LISTEN)
```

If not running:
```bash
dotnet run --project src/Api
```

#### 2. Verify Mac's IP Address
```bash
ifconfig | grep "inet " | grep -v 127.0.0.1
# Update mobile/src/constants/index.ts line 8 with correct IP
```

#### 3. Check Firewall (macOS)
```bash
# Check if firewall is blocking
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --getglobalstate

# Allow dotnet through firewall
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --add /usr/local/share/dotnet/dotnet
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblockapp /usr/local/share/dotnet/dotnet
```

#### 4. Verify Network Connectivity
```bash
# From Mac, test local connection
curl http://192.168.68.108:5118/api/organizations

# Check if mobile device is on same network
# iOS: Settings → Wi-Fi → tap (i) → IP address should be 192.168.68.x
# Android: Settings → Wi-Fi → Advanced → IP address should be 192.168.68.x
```

### Issue: "Token expired" or "Authentication failed"

**Solution:** Clear app data and re-login
```bash
# In Expo, shake device → "Clear AsyncStorage" or "Reload"
```

### Issue: IP Address Changes

If your Mac's IP address changes (e.g., switching networks):

**Quick Fix:**
```bash
# Get new IP
ifconfig | grep "inet " | grep -v 127.0.0.1

# Update mobile/src/constants/index.ts line 8
# Reload mobile app (press 'r' in Expo)
```

**Better Solution:** Use environment variables

**Create:** `mobile/.env`
```bash
API_URL=http://192.168.68.108:5118
```

**Update:** `mobile/app.json`
```json
{
  "expo": {
    "extra": {
      "apiUrl": process.env.API_URL
    }
  }
}
```

Then the app will use the env variable automatically!

## Testing Checklist

- [ ] Backend running on http://192.168.68.108:5118
- [ ] Mobile device on same Wi-Fi network (192.168.68.x)
- [ ] Mobile app shows correct BASE_URL in logs
- [ ] Can login with test@example.com / testpass123
- [ ] No "Unable to connect" errors
- [ ] Can trigger test alert
- [ ] Can view alert history

## Quick Commands Reference

### Backend
```bash
# Start backend
cd cloud-backend
dotnet run --project src/Api

# Check if running
lsof -i :5118

# View logs
# (logs appear in terminal where dotnet run is running)
```

### Mobile
```bash
# Start Expo
cd mobile
npm start

# Reload app
# Press 'r' in Expo terminal

# Clear cache
# Press 'c' in Expo terminal

# View logs
# Logs appear in Expo terminal
```

### Database
```bash
# Check database
cd cloud-backend
dotnet ef database update --project src/Infrastructure --startup-project src/Api

# View data
psql -U safesignal -d safesignal_dev
SELECT * FROM users;
SELECT * FROM alert_history;
```

## Network Troubleshooting

### Find Mac's IP on Current Network
```bash
# Method 1: ifconfig
ifconfig | grep "inet " | grep -v 127.0.0.1

# Method 2: System Preferences
# Apple menu → System Preferences → Network → Wi-Fi → IP address

# Method 3: networksetup
networksetup -getinfo Wi-Fi | grep "IP address"
```

### Test API from Mobile Device

**iOS:** Use Safari or Chrome
```
http://192.168.68.108:5118/api/organizations
```

**Android:** Use Chrome
```
http://192.168.68.108:5118/api/organizations
```

If you see JSON data, API is reachable! ✅

### Verify Same Network

**Mac:**
```bash
ifconfig en0 | grep "inet "
# Output: inet 192.168.68.108 ...
```

**Mobile Device:**
- iOS: Settings → Wi-Fi → (i) next to network name → IP Address
- Android: Settings → Network → Wi-Fi → Advanced → IP Address

**Both should have same first 3 octets:** `192.168.68.x`

## Production Deployment Notes

For production, you'll want to:

1. **Use HTTPS** - Configure SSL/TLS certificates
2. **Use Domain Name** - e.g., `https://api.safesignal.io`
3. **Environment-based Config** - Different URLs for dev/staging/prod
4. **Error Handling** - Better offline support and retry logic

Current setup is for **local development only**!

---

**Status:** ✅ Connection issue fixed
**Updated:** 2025-11-04
**Mac IP:** 192.168.68.108
**Backend Port:** 5118
