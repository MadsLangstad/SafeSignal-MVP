# JWT Token Debugging

## Token Preview
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc...
```

This is a valid JWT format (3 parts separated by dots).

## What to Check on Backend

**Please check your backend console output for:**

1. **JWT Configuration** (should appear on startup):
   ```
   JWT Configuration: Issuer=SafeSignal.Cloud.Api, Audience=SafeSignal.Mobile.App, SecretKeyLength=...
   ```

2. **Authentication Failed Messages** (when 401 occurs):
   ```
   JWT Authentication failed: [error message here]
   ```

3. **JWT Challenge Messages**:
   ```
   JWT Challenge: [error], [description]
   ```

## Common Issues to Look For

### 1. Secret Key Mismatch
- Backend using different secret key than token was signed with
- Environment variable `JWT_SECRET_KEY` not set, falling back to config

### 2. Issuer/Audience Mismatch
- Token signed with different Issuer/Audience than backend expects
- Backend expects: `Issuer=SafeSignal.Cloud.Api, Audience=SafeSignal.Mobile.App`

### 3. Token Expiration
- Token expired (but logs show expires in 1 hour, so this shouldn't be the issue)

### 4. Claims Missing
- Token missing required claims like `organizationId`

## Next Steps

**Please run this command in your backend directory and share output:**

```bash
cd /Users/mads/dev/repos/collab/safeSignal/safeSignal-mvp/cloud-backend/src/Api
dotnet run
```

Then attempt login from mobile app and share:
1. Backend startup logs (JWT Configuration line)
2. Backend error logs when 401 occurs

This will tell us exactly what's failing in JWT validation.
