# SSO Authentication Implementation Guide

## Overview

The SafeSignal mobile app now supports multiple authentication methods:
- **Email/Password** - Traditional username/password authentication
- **Feide** - Norwegian educational identity provider (OAuth/OIDC)
- **BankID** - Norwegian national electronic identification
- **Biometric** - Face ID / Fingerprint (after initial login)

## Architecture

### Services (`src/services/auth/`)

#### Feide Service (`feide.ts`)
- Implements OAuth 2.0 / OpenID Connect flow
- Uses PKCE (Proof Key for Code Exchange) for security
- Leverages `expo-auth-session` for web-based OAuth flow
- Returns authorization code for backend token exchange

**Flow:**
1. Generate PKCE challenge and state
2. Open browser with Feide authorization URL
3. User authenticates with Feide
4. Feide redirects back with authorization code
5. Send code to backend for token exchange
6. Backend validates, creates/finds user, returns JWT

#### BankID Service (`bankid.ts`)
- Implements BankID authentication with QR code and polling
- Supports both mobile app launch and QR code scanning
- Polls backend for authentication status every 2 seconds
- Provides user-friendly hint messages in Norwegian

**Flow:**
1. Initiate BankID session via backend
2. Backend returns session ID, QR code, and auto-start token
3. Mobile: Launch BankID app with auto-start token
4. Web: Display QR code for scanning
5. Poll backend for status updates
6. On completion, exchange session for JWT

### Store (`src/store/authStore.ts`)

Enhanced Zustand store with SSO session tracking:
- Manages authentication state across all providers
- Tracks active SSO sessions (Feide/BankID)
- Handles polling for BankID
- Provides unified error handling

### UI Components

#### Enhanced Login Screen (`LoginScreen.enhanced.tsx`)
- SSO provider buttons (Feide, BankID)
- Traditional email/password form
- Biometric authentication option
- Responsive design for all screen sizes

#### BankID Auth Screen (`BankIDAuthScreen.tsx`)
- QR code display for web
- Mobile app launch for devices
- Real-time status updates with Norwegian hints
- Polling management with cleanup

## Configuration

### Environment Variables

Create `.env` file (copy from `.env.example`):

```bash
# API URL
EXPO_PUBLIC_API_URL=http://localhost:5118

# App scheme for deep linking
EXPO_PUBLIC_APP_SCHEME=safesignal

# Feide OAuth
EXPO_PUBLIC_FEIDE_CLIENT_ID=your_client_id
EXPO_PUBLIC_FEIDE_DISCOVERY_URL=https://auth.dataporten.no/.well-known/openid-configuration
```

### App Scheme (Deep Linking)

Already configured in `app.json`:
```json
{
  "expo": {
    "scheme": "safesignal"
  }
}
```

This enables callbacks:
- `safesignal://auth/feide/callback`
- `safesignal://auth/bankid/callback`

## Installation

### 1. Install Dependencies

```bash
cd mobile
npm install
```

New dependencies added:
- `expo-auth-session` - OAuth/OIDC flows
- `expo-web-browser` - SSO web views
- `expo-crypto` - PKCE generation
- `react-native-qrcode-svg` - QR code display

### 2. Configure Environment

```bash
cp .env.example .env
# Edit .env with your configuration
```

### 3. Configure Feide (if using)

1. Register app at https://dashboard.feide.no
2. Add redirect URI: `safesignal://auth/feide/callback`
3. Request scopes: `openid`, `profile`, `email`, `eduPersonPrincipalName`
4. Copy client ID to `.env`

### 4. Configure Backend

Ensure backend implements:
- `POST /auth/feide/callback` - Exchange Feide code for token
- `POST /auth/bankid/initiate` - Start BankID session
- `GET /auth/bankid/status/:sessionId` - Poll BankID status
- `POST /auth/bankid/complete` - Complete BankID authentication
- `POST /auth/bankid/cancel/:sessionId` - Cancel BankID session

## Testing

### Testing Plan

#### 1. Feide Authentication

**Prerequisites:**
- Feide test account (from https://feide.no)
- Staging Feide client configured

**Test Cases:**
```
1. Happy Path
   - Tap "Sign in with Feide"
   - Browser opens to Feide login
   - Enter valid Feide credentials
   - Approve consent screen
   - Redirect back to app
   - User logged in successfully

2. User Cancellation
   - Tap "Sign in with Feide"
   - Tap "Cancel" in browser
   - Return to login screen
   - Error message displayed

3. Invalid Credentials
   - Tap "Sign in with Feide"
   - Enter invalid credentials
   - See Feide error
   - Return to login screen

4. Token Exchange Failure
   - Mock backend failure
   - Verify error handling
```

**Validation:**
```bash
# Decode JWT to verify claims
./scripts/decode-token.sh <token>

# Should contain:
{
  "sub": "feide-user-id",
  "email": "user@institution.no",
  "eduPersonPrincipalName": "user@institution.no",
  "organizationId": "..."
}
```

#### 2. BankID Authentication

**Prerequisites:**
- BankID test account (test.bankid.no)
- BankID test app installed on device
- Backend configured with test certificates

**Test Cases:**
```
1. Mobile Flow (Auto-launch)
   - Tap "Sign in with BankID"
   - BankID app launches automatically
   - Enter test code in BankID app
   - App polls and completes auth
   - User logged in successfully

2. QR Code Flow (Web/Simulator)
   - Tap "Sign in with BankID"
   - QR code displayed
   - Scan with BankID app on phone
   - Enter code in BankID app
   - App polls and completes auth
   - User logged in successfully

3. Timeout
   - Initiate BankID
   - Wait without completing (3 min)
   - Session expires
   - Error message shown
   - Option to retry

4. User Cancellation
   - Initiate BankID
   - Cancel in BankID app
   - Poll detects cancellation
   - Return to login with message

5. Polling Cleanup
   - Initiate BankID
   - Press back button
   - Verify polling stops
   - No memory leaks
```

**Validation:**
```bash
# Check BankID session
curl http://localhost:5118/api/auth/bankid/status/SESSION_ID

# Should return:
{
  "status": "pending|complete|failed|expired",
  "hintCode": "...",
  "completionData": { ... }
}
```

#### 3. Integration Tests

```
1. Session Persistence
   - Login with Feide
   - Close app (don't kill)
   - Reopen app
   - User still logged in

2. Token Refresh
   - Login with any SSO
   - Wait for token expiry
   - Make API call
   - Token refreshed automatically

3. Logout
   - Login with SSO
   - Tap Logout
   - Session cleared
   - Redirected to login screen

4. Multiple Auth Methods
   - Login with Feide
   - Logout
   - Login with BankID
   - Logout
   - Login with email/password
   - All methods work independently
```

### Manual Testing Scripts

```bash
# Test Feide flow
./scripts/test-sso-feide.sh

# Test BankID flow
./scripts/test-sso-bankid.sh

# Verify token claims
./scripts/verify-jwt-token.sh
```

### Automated Testing

```typescript
// Example test
describe('Feide Authentication', () => {
  it('should complete OAuth flow', async () => {
    const { result } = renderHook(() => useAuthStore());

    await act(async () => {
      await result.current.loginWithFeide();
    });

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user).toBeDefined();
  });
});
```

## Security Considerations

### Feide
- ✅ PKCE prevents authorization code interception
- ✅ State parameter prevents CSRF attacks
- ✅ Token exchange happens server-side only
- ✅ Tokens stored in secure storage (Keychain/Keystore)

### BankID
- ✅ All sensitive operations on backend
- ✅ Session IDs are single-use
- ✅ Polling timeout prevents resource exhaustion
- ✅ Signature verification happens server-side
- ✅ Personal number (if used) never stored on device

### General
- ✅ Deep link validation
- ✅ TLS for all API communication
- ✅ No credentials in source code
- ✅ Environment-specific configuration
- ✅ Automatic session expiry

## Troubleshooting

### Feide Issues

**Problem:** Browser doesn't redirect back to app
**Solution:** Verify `app.json` scheme matches redirect URI

**Problem:** "Invalid redirect URI" from Feide
**Solution:** Check Feide dashboard configuration

**Problem:** Token exchange fails
**Solution:** Verify backend `/auth/feide/callback` endpoint

### BankID Issues

**Problem:** BankID app doesn't launch
**Solution:**
- Check BankID app is installed
- Verify auto-start token is valid
- On iOS, check URL scheme is registered

**Problem:** QR code not working
**Solution:**
- Verify QR data format
- Check network connectivity
- Ensure backend session is valid

**Problem:** Polling never completes
**Solution:**
- Check backend status endpoint
- Verify session ID is correct
- Review backend logs for errors

### General Issues

**Problem:** Deep linking not working
**Solution:**
```bash
# iOS
npx uri-scheme open safesignal://auth/test --ios

# Android
npx uri-scheme open safesignal://auth/test --android
```

**Problem:** Environment variables not loading
**Solution:**
- Restart Metro bundler
- Clear cache: `npx expo start --clear`
- Verify `.env` file exists

## Next Steps

1. **Backend Implementation**
   - Implement Feide callback endpoint
   - Implement BankID integration
   - Set up staging environments

2. **Testing**
   - Complete manual testing with staging providers
   - Set up automated E2E tests
   - Load testing for polling endpoints

3. **Production Prep**
   - Register production Feide client
   - Obtain production BankID certificates
   - Configure production environment variables
   - Security audit

4. **Monitoring**
   - Add analytics for auth flows
   - Monitor SSO success/failure rates
   - Track polling performance
   - Alert on high failure rates

## Support

For issues or questions:
- **Feide:** https://www.feide.no/hjelp
- **BankID:** https://www.bankid.no/support
- **SafeSignal:** Contact your administrator
