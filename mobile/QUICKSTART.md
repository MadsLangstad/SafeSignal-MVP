# SafeSignal Mobile - Quick Start Guide

## 🚀 5-Minute Setup

### 1. Install Dependencies
```bash
cd mobile
npm install
```

### 2. Configure API Endpoint
Edit `src/constants/index.ts`:
```typescript
export const API_CONFIG = {
  BASE_URL: 'http://192.168.1.100:5000', // Your local IP or cloud URL
};
```

### 3. Start Development Server
```bash
npm start
```

### 4. Run on Device

**Option A: Physical Device (Recommended)**
- Install "Expo Go" app from App Store / Play Store
- Scan QR code from terminal
- App will load on your device

**Option B: iOS Simulator (macOS only)**
```bash
npm run ios
```

**Option C: Android Emulator**
```bash
npm run android
```

---

## 📱 Test Credentials

**Development Login:**
```
Email: test@safesignal.io
Password: TestPassword123
```

---

## ✅ Key Features to Test

1. **Login** → Email/password authentication
2. **Home Screen** → Select building and room
3. **Emergency Button** → Trigger test alert
4. **Alert History** → View past alerts
5. **Settings** → Enable biometric authentication
6. **Offline Mode** → Enable airplane mode, trigger alert, disable airplane mode (should sync)

---

## 🔧 Common Commands

```bash
# Clear cache and restart
npm start --clear

# View logs
npm run ios # iOS device logs appear in terminal
npm run android # Android logs appear in terminal

# Install on physical device
npx expo run:ios --device
npx expo run:android --device
```

---

## 🐛 Troubleshooting

**"Cannot connect to Metro bundler"**
- Ensure phone and computer are on same Wi-Fi
- Try using tunnel mode: `npm start --tunnel`

**"Database not initialized"**
- Clear app data: Delete app and reinstall
- Check console for initialization errors

**Push notifications not working**
- Push notifications require physical devices (not simulators)
- Check notification permissions in device settings
- Verify EAS Project ID in `app.json`

**Build errors**
```bash
# Clear everything and reinstall
rm -rf node_modules
npm install
npm start --clear
```

---

## 📚 Next Steps

- Read [README.md](./README.md) for full documentation
- Read [IMPLEMENTATION.md](./IMPLEMENTATION.md) for architecture details
- Configure cloud backend API (Phase 2)
- Set up push notification credentials (EAS)
- Deploy beta builds for testing

---

## 🆘 Support

For issues or questions:
1. Check the full README.md
2. Check console logs for errors
3. Verify API endpoint is reachable
4. Ensure cloud backend is running

---

**Quick Links:**
- [Full README](./README.md)
- [Implementation Details](./IMPLEMENTATION.md)
- [Expo Documentation](https://docs.expo.dev/)
- [React Native Documentation](https://reactnative.dev/)
