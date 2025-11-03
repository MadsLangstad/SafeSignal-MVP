# Authentication 401 - Real Root Cause

## TL;DR

**The password "rootadmin" fails backend validation** (only 9 chars, needs 12+ chars + complexity).

Login is failing with HTTP 400, but mobile app shows old tokens from previous sessions causing 401 errors.

## The Real Issue

### Password Validation Requirements
Backend enforces OWASP/NIST password policy (`PasswordValidator.cs:46`):

- ✅ Minimum **12 characters**
- ✅ At least one **uppercase** letter
- ✅ At least one **lowercase** letter
- ✅ At least one **digit**
- ✅ At least one **special character** (!@#$%^&*(),.?":{}|<>)
- ✅ No **sequential characters** (123, abc, etc.)
- ✅ Not a **common password**

### Current Password
- Password: `rootadmin`
- Length: **9 characters**
- Status: **FAILS validation**

### What We Observed
```
Mobile logs: Token available, Authorization header set → 401 errors
Backend logs: Authorization failed. DenyAnonymousAuthorizationRequirement
Login API: HTTP 400 Bad Request - "Password must be at least 12 characters"
```

## Solutions

### Option 1: Use Compliant Password (Recommended)

Update your test password to meet requirements:

```
Admin@12345678!
RootAdmin2024!@
SuperAdmin123!@#
```

### Option 2: Temporarily Disable Validation (Development Only)

Add to `appsettings.Development.json`:

```json
"PasswordPolicy": {
  "MinimumLength": 8,
  "RequireUppercase": false,
  "RequireLowercase": false,
  "RequireDigit": false,
  "RequireSpecialCharacter": false
}
```

### Option 3: Update Existing User Password

Run migration or SQL to update the password hash for admin user.

## Testing

After using a compliant password:

```bash
# Test login
curl -X POST http://localhost:5118/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@safesignal.com","password":"Admin@12345678!"}'

# Should return HTTP 200 with tokens
```

## What Was the Race Condition?

The race condition fix in `mobile/src/store/index.ts` is **still valid** - it prevents tokens from being read before they're written. However, in this case:

1. Login was **failing** (HTTP 400) due to password validation
2. Mobile app had **old tokens** in SecureStore from previous successful login
3. Those old tokens caused the 401 errors we saw

The race condition would have caused issues once login succeeds, so the fix should stay.

## Files Involved

- `cloud-backend/src/Api/Services/PasswordValidator.cs` - Password validation logic
- `cloud-backend/src/Api/Validators/AuthValidators.cs` - FluentValidation rules
- `cloud-backend/src/Api/appsettings.Development.json` - Config overrides
- `mobile/src/store/index.ts` - Login flow (race condition fix is still useful)

## Next Steps

1. ✅ Use compliant password: `Admin@12345678!`
2. ✅ Clear mobile app SecureStore (uninstall/reinstall app)
3. ✅ Login with new password
4. ✅ Verify no 401 errors

## Lessons Learned

- Always check **backend logs** for validation errors
- HTTP 400 vs HTTP 401 are different failure modes
- Old cached tokens can mask the real issue
- Password validators are good for production security!
