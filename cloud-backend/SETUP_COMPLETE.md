# ✅ Cloud Backend Setup Complete

## Test User Credentials

**Email:** `test@example.com`  
**Password:** `Test1234`

## Quick Start

### Start Backend
```bash
cd cloud-backend
./start-with-env.sh
```
Backend will be available at: **http://10.57.74.59:5118**

### Start Mobile App
```bash
cd mobile
npx expo start --clear
```

### Test Login
```bash
curl -X POST http://10.57.74.59:5118/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test1234"}'
```

## What Was Fixed

### ✅ Security (P0 & P1)
- JWT authentication on all endpoints
- BCrypt password hashing (work factor 12)
- Rate limiting (5 login/min)
- Refresh token rotation
- CORS for mobile apps
- Externalized secrets to environment variables

### ✅ Mobile App
- Logout now clears state even if API fails
- IP address updated to 10.57.74.59:5118
- Login validation with email/password trimming
- Background sync only when authenticated

### ✅ Database
- Applied refresh_tokens migration
- Test user seeded and ready
- Password generation tool created (generate-hash.js)

## Tools Created

- `start-with-env.sh` - Start backend with env vars
- `generate-hash.js` - Generate BCrypt password hashes
- `seed-test-user.sql` - SQL script reference

## Change Password

```bash
node generate-hash.js YourNewPassword
# Copy and run the SQL command from output
```

## Documentation

- **API Docs:** http://localhost:5118/swagger
- **Environment Setup:** ENV_SETUP.md
- **Credentials:** edge/CREDENTIALS.md
