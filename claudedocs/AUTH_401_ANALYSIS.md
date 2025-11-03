# Authentication 401 Error Analysis

## Executive Summary

The mobile app experiences 401 Unauthorized errors immediately after successful login when attempting to load buildings and alerts. Root cause identified as a **race condition** between token storage and subsequent API calls.

## Error Pattern

```
api.ts:632 API Error: {message: 'Request failed with status code 401', status: 401, method: 'get', urlPath: '/api/buildings'}
api.ts:340 Load alerts error: AxiosError: Request failed with status code 401
api.ts:632 API Error: {message: 'Request failed with status code 401', status: 401, method: 'get', urlPath: '/api/alerts'}
```

**Timing**: Errors occur immediately after login, suggesting tokens are not available when needed.

## Root Cause Analysis

### The Race Condition

**Location**: `mobile/src/store/index.ts:83-104`

```typescript
login: async (email, password) => {
  set({ isLoading: true });
  try {
    const response = await authService.login(email, password);

    if (response.success && response.data) {
      set({
        user: response.data,
        isAuthenticated: true,
        isLoading: false,
      });

      // ⚠️ RACE CONDITION: This runs immediately
      Promise.all([
        get().loadBuildings(),    // ← Tries to read tokens
        get().loadAlerts(true)    // ← Tries to read tokens
      ]).catch((error) => {
        console.error('Data load after login failed (non-blocking):', error);
      });

      return true;
    }
    // ...
  }
}
```

### The Token Flow

1. **Token Save** (`mobile/src/services/api.ts:114-128`):
   ```typescript
   async login(email: string, password: string) {
     const response = await this.client.post('/api/auth/login', { email, password });

     await secureStorage.saveTokens(response.data.tokens);  // ← Async write
     await secureStorage.saveUser(response.data.user);      // ← Async write

     return { success: true, data: response.data };
   }
   ```

2. **Token Read** (`mobile/src/services/api.ts:41-50`):
   ```typescript
   this.client.interceptors.request.use(
     async (config) => {
       const tokens = await secureStorage.getTokens();  // ← Async read
       if (tokens?.accessToken) {
         config.headers.Authorization = `Bearer ${tokens.accessToken}`;
       }
       return config;
     }
   )
   ```

3. **The Problem**:
   - `apiClient.login()` saves tokens and returns
   - `authService.login()` extracts just the user and returns
   - Store's `login()` receives response and **immediately** fires `Promise.all([loadBuildings(), loadAlerts()])`
   - These API calls execute **before SecureStore async writes are guaranteed to complete**
   - Request interceptor tries to read tokens but may get `null` or stale data
   - API calls proceed without Authorization header → 401 Unauthorized

### Why This Happens

**React Native SecureStore Async Behavior**:
- `SecureStore.setItemAsync()` is async but doesn't guarantee immediate availability
- On slower devices or under load, there can be a delay between write completion and read availability
- Multiple rapid async operations can cause timing issues

**Current Flow Timeline**:
```
0ms:  login() called
100ms: apiClient.login() returns (tokens saved to SecureStore)
101ms: authService.login() returns
102ms: store.login() receives response
103ms: Promise.all([loadBuildings(), loadAlerts()]) fires
104ms: Request interceptor tries to read tokens ← MAY NOT BE AVAILABLE YET
105ms: GET /api/buildings with NO Authorization header
106ms: 401 Unauthorized response
```

## Evidence Supporting Root Cause

1. **Timing**: Errors happen immediately after login, not randomly
2. **Consistency**: Multiple 401s in rapid succession (buildings, alerts)
3. **Pattern**: Only affects first API calls after login
4. **No Token Refresh Triggered**: 401 errors don't trigger refresh flow (line 58 check fails)
5. **Auto-Logout Cascade**: Store detects 401 and logs user out (lines 184-194, 279-290)

## Solutions

### Solution 1: Wait for Token Availability (Recommended)

**File**: `mobile/src/store/index.ts:83-114`

Add explicit wait after login before triggering data loads:

```typescript
login: async (email, password) => {
  set({ isLoading: true });
  try {
    const response = await authService.login(email, password);

    if (response.success && response.data) {
      set({
        user: response.data,
        isAuthenticated: true,
        isLoading: false,
      });

      // ✅ FIX: Wait a tick to ensure tokens are available
      await new Promise(resolve => setTimeout(resolve, 100));

      // OR verify tokens are actually available:
      const tokens = await secureStorage.getTokens();
      if (!tokens?.accessToken) {
        throw new Error('Token storage failed');
      }

      // Now safely load data
      Promise.all([
        get().loadBuildings(),
        get().loadAlerts(true)
      ]).catch((error) => {
        console.error('Data load after login failed (non-blocking):', error);
      });

      return true;
    }
    // ...
  }
}
```

**Pros**:
- Minimal code change
- Guarantees tokens are available
- Low risk

**Cons**:
- Adds slight delay to login flow
- Not the cleanest solution architecturally

### Solution 2: Return Tokens from Auth Service

**File**: `mobile/src/services/auth.ts:8-16`

Modify authService to return tokens alongside user:

```typescript
async login(email: string, password: string): Promise<ApiResponse<{ user: User; tokens: AuthTokens }>> {
  const response = await apiClient.login(email, password);

  if (response.success && response.data) {
    return {
      success: true,
      data: {
        user: response.data.user,
        tokens: response.data.tokens  // Include tokens
      }
    };
  }

  return { success: false, error: response.error };
}
```

**File**: `mobile/src/store/index.ts:83-114`

```typescript
login: async (email, password) => {
  set({ isLoading: true });
  try {
    const response = await authService.login(email, password);

    if (response.success && response.data) {
      // Verify tokens are in response
      if (!response.data.tokens?.accessToken) {
        throw new Error('No access token received');
      }

      set({
        user: response.data.user,
        isAuthenticated: true,
        isLoading: false,
      });

      // Tokens are guaranteed to exist in SecureStore by this point
      Promise.all([
        get().loadBuildings(),
        get().loadAlerts(true)
      ]).catch((error) => {
        console.error('Data load after login failed (non-blocking):', error);
      });

      return true;
    }
    // ...
  }
}
```

**Pros**:
- Cleaner architecture
- Type-safe token availability
- No artificial delays

**Cons**:
- Requires changes in multiple files
- Changes API contract

### Solution 3: Defer Data Loading to Screen Mount

**File**: `mobile/src/store/index.ts:83-114`

Remove automatic data loading from login:

```typescript
login: async (email, password) => {
  set({ isLoading: true });
  try {
    const response = await authService.login(email, password);

    if (response.success && response.data) {
      set({
        user: response.data,
        isAuthenticated: true,
        isLoading: false,
      });

      // ✅ Don't load data here - let screens handle it
      return true;
    }
    // ...
  }
}
```

**File**: `mobile/src/screens/HomeScreen.tsx` (and others)

Add explicit data loading in useEffect:

```typescript
useEffect(() => {
  if (isAuthenticated) {
    loadBuildings();
    loadAlerts(true);
  }
}, [isAuthenticated]);
```

**Pros**:
- Separation of concerns
- Screens control their own data loading
- Avoids race conditions entirely

**Cons**:
- Requires changes in multiple screens
- Slightly more complex screen logic

## Recommended Implementation Plan

**Phase 1: Quick Fix (Immediate)**
- Implement Solution 1 (timeout/verification)
- Deploy and verify fix resolves 401 errors
- Estimated time: 15 minutes

**Phase 2: Proper Architecture (Next Sprint)**
- Implement Solution 3 (screen-based loading)
- Refactor data loading to be screen-driven
- Add proper loading states
- Estimated time: 2-3 hours

## Testing Checklist

- [ ] Login with valid credentials
- [ ] Verify no 401 errors in console after login
- [ ] Verify buildings load successfully
- [ ] Verify alerts load successfully
- [ ] Test on slower devices (authentication delay)
- [ ] Test with network throttling
- [ ] Test token refresh flow
- [ ] Test logout and re-login
- [ ] Test biometric login flow

## Related Files

- `mobile/src/store/index.ts:83-114` - Login action
- `mobile/src/services/api.ts:114-128` - API client login
- `mobile/src/services/api.ts:41-50` - Request interceptor
- `mobile/src/services/auth.ts:8-16` - Auth service login
- `mobile/src/services/secureStorage.ts:6-37` - Token storage
- `mobile/src/store/index.ts:162-195` - loadBuildings (401 handler)
- `mobile/src/store/index.ts:255-291` - loadAlerts (401 handler)

## References

- Expo SecureStore docs: https://docs.expo.dev/versions/latest/sdk/securestore/
- React Native async storage patterns
- JWT token handling best practices
