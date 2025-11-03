# SafeSignal Mobile App

React Native + Expo mobile application for the SafeSignal emergency alert system.

## Features

- ✅ **Authentication**: Email/password login with biometric unlock support (Face ID/Touch ID/Fingerprint)
- ✅ **Emergency Alerts**: One-tap alert triggering with multiple alert modes (Silent, Audible, Lockdown, Evacuation)
- ✅ **Offline-First**: SQLite local storage with automatic sync when online
- ⚠️ **Push Notifications**: Real-time alerts via APNs (iOS) and FCM (Android) - *Requires development build, not available in Expo Go*
- ✅ **Alert History**: View past alerts with sync status indicators
- ✅ **Building/Room Selection**: Dynamic topology management
- ✅ **Background Sync**: Automatic 30-second sync for pending actions
- ✅ **Network Resilience**: Queue operations when offline, replay when connected

> **Note**: Push notifications require a [development build](./PUSH_NOTIFICATIONS.md). All other features work in Expo Go.

## Architecture

```
/mobile
  /src
    /constants      # Configuration and constants
    /database       # SQLite offline storage
    /navigation     # React Navigation setup
    /screens        # UI screens
    /services       # API, Auth, Notifications, Storage
    /store          # Zustand state management
    /types          # TypeScript type definitions
```

## Prerequisites

- Node.js 18+ and npm/yarn
- Expo CLI: `npm install -g expo-cli`
- iOS: Xcode 14+ and iOS Simulator (macOS only)
- Android: Android Studio and Android emulator

## Installation

```bash
# Install dependencies
npm install

# Start development server
npm start

# Run on iOS
npm run ios

# Run on Android
npm run android

# Run on web (limited functionality)
npm run web
```

## Configuration

### API Endpoint

Edit `src/constants/index.ts`:

```typescript
export const API_CONFIG = {
  BASE_URL: 'http://localhost:5000', // Development
  // BASE_URL: 'https://api.safesignal.io', // Production
};
```

### Push Notifications

1. Create an Expo project: `npx expo login && npx expo init`
2. Get EAS Project ID from `app.json`
3. Configure push notification credentials:
   - iOS: Set up APNs in Apple Developer Portal
   - Android: Set up FCM in Firebase Console

## Key Technologies

- **React Native + Expo**: Cross-platform mobile framework
- **TypeScript**: Type-safe development
- **Zustand**: Lightweight state management
- **React Navigation**: Navigation solution
- **expo-sqlite**: Offline storage
- **expo-notifications**: Push notifications
- **expo-secure-store**: Secure credential storage
- **expo-local-authentication**: Biometric authentication
- **axios**: HTTP client with offline queue

## Database Schema

**Local SQLite tables:**
- `buildings` - Building topology (synced from cloud)
- `rooms` - Room configuration per building
- `alerts` - Alert history with sync status
- `devices` - Registered device information
- `pending_actions` - Offline operation queue

## Offline-First Strategy

1. **Read Operations**: Always serve from local cache first, refresh from API
2. **Write Operations**: Queue in `pending_actions` if offline
3. **Background Sync**: Every 30 seconds, replay pending actions
4. **Conflict Resolution**: Last-write-wins with server as authority

## Security Features

- ✅ JWT token authentication with automatic refresh
- ✅ Secure credential storage (Keychain/Keystore)
- ✅ Biometric authentication (Face ID/Touch ID/Fingerprint)
- ✅ TLS/HTTPS for all API communication
- ✅ No sensitive data in logs

## Development Workflow

### Running on Physical Devices

**iOS:**
```bash
# Build development app
npx expo run:ios --device

# Or use Expo Go app
npx expo start
# Scan QR code with camera
```

**Android:**
```bash
# Build development app
npx expo run:android --device

# Or use Expo Go app
npx expo start
# Scan QR code with Expo Go app
```

### Testing Push Notifications

Push notifications require physical devices (not simulators).

```bash
# Send test notification via Expo Push API
curl -H "Content-Type: application/json" \
  -X POST https://exp.host/--/api/v2/push/send \
  -d '{
    "to": "ExponentPushToken[YOUR_TOKEN]",
    "title": "Test Alert",
    "body": "Emergency alert in Building A",
    "data": { "alertId": "test-123" }
  }'
```

### Building for Production

```bash
# Build for iOS
npx expo build:ios

# Build for Android
npx expo build:android

# Or use EAS Build (recommended)
npx eas build --platform ios
npx eas build --platform android
```

## Environment Variables

Create `.env` file:

```bash
API_URL=http://localhost:5000
EAS_PROJECT_ID=your-eas-project-id
```

## Troubleshooting

### Database Errors

```bash
# Clear app data (development only)
# iOS: Delete app and reinstall
# Android: Settings → Apps → SafeSignal → Clear Data
```

### Push Notification Issues

1. Ensure physical device (not simulator/emulator)
2. Check permissions: Settings → SafeSignal → Notifications
3. Verify EAS Project ID in `app.json`
4. Check push token registration in logs

### Build Errors

```bash
# Clear cache
rm -rf node_modules
npm install

# Clear Metro bundler cache
npx expo start --clear

# Clear watchman cache (macOS)
watchman watch-del-all
```

## Performance Optimization

- **Images**: Use optimized images, consider lazy loading
- **Lists**: Use `FlatList` with `getItemLayout` for large lists
- **Database**: Index frequently queried columns
- **Network**: Batch API calls, implement request debouncing
- **Memory**: Cleanup listeners and intervals on unmount

## App Store Submission

### iOS (App Store)

1. Configure `app.json` with bundle identifier
2. Build with `eas build --platform ios`
3. Upload to App Store Connect
4. Submit for review (typically 1-3 days)

### Android (Google Play)

1. Configure `app.json` with package name
2. Build with `eas build --platform android`
3. Upload to Google Play Console
4. Submit for review (typically 1-7 days)

## License

Proprietary - SafeSignal Emergency Alert System
For authorized personnel only

## Support

For issues or questions, contact the development team or file an issue in the repository.
