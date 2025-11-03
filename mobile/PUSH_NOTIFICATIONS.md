# Push Notifications Setup Guide

## ⚠️ Important: Expo Go Limitations

**Push notifications do NOT work in Expo Go** for SDK 53+. You need to create a **development build** to test push notifications.

## Two Options for Push Notifications

### Option 1: Development Build (Recommended for Testing)

A development build is like a custom version of Expo Go that includes native code for push notifications.

#### Setup Steps

1. **Install EAS CLI**
   ```bash
   npm install -g eas-cli
   ```

2. **Login to Expo**
   ```bash
   eas login
   ```

3. **Configure Project**
   ```bash
   eas build:configure
   ```
   This creates `eas.json` with build profiles.

4. **Build for Development**

   **iOS:**
   ```bash
   eas build --profile development --platform ios
   ```
   - Download the build file (.tar.gz or .app)
   - Install on simulator: Drag to simulator or use `xcrun simctl install booted app.app`
   - Install on device: Register device UDID first via `eas device:create`

   **Android:**
   ```bash
   eas build --profile development --platform android
   ```
   - Download APK
   - Install: `adb install app.apk`

5. **Run Development Build**
   ```bash
   npx expo start --dev-client
   ```
   Scan QR code with your development build app.

#### Testing Push Notifications

Once development build is running:

1. **Get Push Token**
   - Check app logs for: `Push token registered: ExponentPushToken[...]`
   - Or view in Settings screen

2. **Send Test Notification**
   ```bash
   curl -H "Content-Type: application/json" \
     -X POST https://exp.host/--/api/v2/push/send \
     -d '{
       "to": "ExponentPushToken[YOUR_TOKEN_HERE]",
       "title": "Emergency Alert",
       "body": "Test alert from Building A",
       "sound": "default",
       "priority": "high",
       "data": {
         "alertId": "test-123",
         "buildingId": "building-a",
         "mode": "AUDIBLE"
       }
     }'
   ```

3. **Verify**
   - Notification should appear on device
   - App should handle notification tap
   - Check app logs for notification handling

---

### Option 2: Production Build (For App Store/Play Store)

For production deployment with full push notification support.

#### iOS Setup

1. **Apple Developer Account**
   - Enroll in Apple Developer Program ($99/year)
   - Create App ID: `com.safesignal.mobile`

2. **Push Notification Certificate**
   ```bash
   eas credentials
   ```
   - Select iOS → Push Notifications
   - EAS will guide you through APNs setup

3. **Build for Production**
   ```bash
   eas build --platform ios --profile production
   ```

4. **Submit to App Store**
   ```bash
   eas submit --platform ios
   ```

#### Android Setup

1. **Google Play Console**
   - Create account ($25 one-time)
   - Create app: `com.safesignal.mobile`

2. **Firebase Cloud Messaging**
   - Create Firebase project
   - Add Android app
   - Download `google-services.json`
   - EAS will configure automatically

3. **Build for Production**
   ```bash
   eas build --platform android --profile production
   ```

4. **Submit to Play Store**
   ```bash
   eas submit --platform android
   ```

---

## Current Setup (Expo Go)

The app currently works in Expo Go with these limitations:

✅ **Works:**
- Authentication
- Database (SQLite)
- Alert triggering
- Alert history
- Building/room selection
- Offline sync
- Biometric authentication

❌ **Doesn't Work:**
- Push notifications (need development build)
- Custom native modules
- Background tasks (limited)

---

## Quick Development Build Setup

**Minimal steps to test push notifications:**

```bash
# 1. Install EAS CLI
npm install -g eas-cli

# 2. Login
eas login

# 3. Configure
eas build:configure

# 4. Build for Android (faster than iOS)
eas build --profile development --platform android

# 5. Wait for build (10-20 minutes)
# Download APK and install on device

# 6. Run
npx expo start --dev-client
```

**Total time:** ~30 minutes (first build takes longer)

---

## Troubleshooting

### "Must use physical device for push notifications"
✅ **Expected** - Push notifications require real devices, not simulators/emulators

### "Notification permissions not granted"
```typescript
// The app already handles this in notifications.ts
// Just grant permissions when prompted on device
```

### "expo-notifications not supported in Expo Go"
✅ **Expected** - Use development build instead (see above)

### Testing Without Push Notifications

You can test most features without push notifications:
- Manual alert triggering works
- Alert history works
- Offline sync works
- All core functionality works

Push notifications are only needed for:
- Receiving remote alerts
- Background alert handling

---

## EAS Configuration

Create `eas.json`:

```json
{
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal",
      "ios": {
        "simulator": true
      }
    },
    "preview": {
      "distribution": "internal"
    },
    "production": {
      "autoIncrement": true
    }
  }
}
```

---

## Cost Summary

| Option | Cost | Time | Best For |
|--------|------|------|----------|
| **Expo Go** | Free | Instant | Basic development (no push) |
| **Dev Build** | Free | 30 min | Testing push notifications |
| **iOS Production** | $99/year | 1-2 days | App Store release |
| **Android Production** | $25 once | 1-7 days | Play Store release |

---

## Recommended Workflow

1. **Phase 1: Development (Current)**
   - Use Expo Go for rapid development
   - Test all features except push notifications
   - Perfect: ✅ Works great!

2. **Phase 2: Push Testing**
   - Create development build (1-time setup)
   - Test push notifications on real devices
   - Iterate on notification handling

3. **Phase 3: Beta Testing**
   - Build preview versions
   - Distribute to testers via TestFlight/Play Console
   - Gather feedback

4. **Phase 4: Production**
   - Submit to App Store and Play Store
   - Full push notification support
   - Production deployment

---

## Next Steps

**For now (Expo Go):**
- ✅ Continue development
- ✅ Test all core features
- ✅ Push notifications will be noted as "not available in Expo Go"

**When ready for push testing:**
1. Run `eas build:configure`
2. Build development version
3. Install on device
4. Test push notifications

**For production:**
1. Set up Apple Developer + Google Play accounts
2. Configure push certificates
3. Build production versions
4. Submit to stores

---

## Support

- [Expo Push Notifications](https://docs.expo.dev/push-notifications/overview/)
- [Development Builds](https://docs.expo.dev/develop/development-builds/introduction/)
- [EAS Build](https://docs.expo.dev/build/introduction/)
