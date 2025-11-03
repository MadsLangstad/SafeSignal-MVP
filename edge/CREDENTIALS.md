# SafeSignal MVP - Service Credentials

**⚠️ WARNING**: This file contains default development credentials.
**NEVER use these in production!** See `PRODUCTION_HARDENING.md` for security guidance.

---

## 🔐 Service Credentials

### Monitoring & Dashboards

#### Status Dashboard
- **URL**: http://localhost:5200
- **Authentication**: None (publicly accessible)
- **Purpose**: Real-time alert monitoring and system status

#### Grafana
- **URL**: http://localhost:3000
- **Username**: `admin`
- **Password**: `admin`
- **Notes**: You'll be prompted to change password on first login
- **Dashboards**: SafeSignal Edge Metrics

#### Prometheus
- **URL**: http://localhost:9090
- **Authentication**: None
- **Purpose**: Metrics collection and querying

---

### Infrastructure Services

#### EMQX MQTT Broker
- **Dashboard URL**: http://localhost:18083
- **Username**: `admin`
- **Password**: `public`
- **MQTT Ports**:
  - 1883 (TCP - unencrypted)
  - 8883 (TLS - encrypted with mTLS)
- **Purpose**: Message broker for alert triggers

#### MinIO Object Storage
- **Console URL**: http://localhost:9001
- **API URL**: http://localhost:9000
- **Root Username**: `safesignal-admin`
- **Root Password**: `safesignal-dev-password-change-in-prod`
- **Access Key**: `safesignal-admin`
- **Secret Key**: `safesignal-dev-password-change-in-prod`
- **Bucket**: `safesignal-audio`
- **Purpose**: Audio clip storage

---

### Database Access

#### PostgreSQL (if enabled in future)
- **Host**: localhost
- **Port**: 5432
- **Database**: `safesignal`
- **Username**: `safesignal`
- **Password**: See `.env` file
- **Notes**: Currently using SQLite, PostgreSQL planned for production

#### SQLite (Current)
- **Location**: `/data/safesignal.db` (inside policy-service container)
- **Access**: `docker exec -it safesignal-policy-service sqlite3 /data/safesignal.db`
- **Authentication**: None (file-based)

---

### API Endpoints

#### Policy Service API
- **Base URL**: http://localhost:5100
- **Authentication**: None (MVP - add API key in production)
- **Endpoints**:
  - `GET /health` - No auth
  - `GET /api/stats` - No auth
  - `GET /api/alerts` - No auth
  - `POST /api/alerts` - No auth (⚠️ ADD AUTH IN PRODUCTION)
  - `GET /metrics` - No auth

#### PA Service API
- **Base URL**: http://localhost:5101
- **Authentication**: None (MVP - add API key in production)
- **Endpoints**:
  - `GET /health` - No auth
  - `GET /api/clips` - No auth
  - `GET /api/clips/{clipRef}` - No auth
  - `POST /api/clips/{clipRef}` - No auth (⚠️ ADD AUTH IN PRODUCTION)
  - `GET /metrics` - No auth

#### Cloud Backend API
- **Base URL**: http://localhost:5118
- **API Base Path**: `/api`
- **Authentication**: JWT Bearer tokens with refresh token rotation
- **Swagger UI**: http://localhost:5118/swagger
- **Test User Credentials**:
  - **SuperAdmin** (Full Access):
    - **Email**: `admin@safesignal.com`
    - **Password**: `Admin@12345678!`
    - **Role**: SuperAdmin
  - **Viewer** (Read-Only):
    - **Email**: `testuser@safesignal.com`
    - **Password**: `TestUser123!@#`
    - **Role**: Viewer
  - **Organization**: Test School District (`a216abd0-2c87-4828-8823-48dc8c9f0a8a`)
  - **Notes**: All passwords meet OWASP/NIST requirements (12+ chars, complexity)
- **Key Endpoints**:
  - `POST /api/auth/login` - Login with email/password → returns access token + refresh token
  - `POST /api/auth/refresh` - Refresh access token using refresh token
  - `POST /api/auth/logout` - Revoke refresh token (requires auth)
  - `GET /api/users/me` - Get current user profile (requires auth)
  - `GET /api/organizations` - List organizations (requires auth)
  - `GET /api/buildings` - List buildings (requires auth)
  - `GET /api/devices` - List devices (requires auth)
  - `GET /api/alerts` - List alerts (requires auth)
  - `POST /api/alerts/trigger` - Trigger new alert (requires auth)
  - `POST /api/devices/register` - Register device (requires auth)
- **Security Implementation**:
  - ✅ Password hashing: BCrypt (work factor 12)
  - ✅ JWT signing: HS256 with 512-bit secret key
  - ✅ Access token expiry: 24 hours (1440 minutes)
  - ✅ Refresh token expiry: 7 days with rotation
  - ✅ Rate limiting: 5 login attempts/minute per IP
  - ✅ Authorization: All endpoints require `[Authorize]` attribute
  - ✅ CORS: Configured for Capacitor/Ionic mobile apps
- **Token Usage**:
  - Access token: Use `Authorization: Bearer {access_token}` header
  - Refresh token: Rotates on each refresh (old token revoked)
  - Logout: Revokes refresh token server-side

---

### Certificate-Based Authentication

#### MQTT Client Certificates (mTLS)
- **CA Certificate**: `edge/certs/ca/ca.crt`
- **Test Device Certificate**: `edge/certs/devices/esp32-test.crt`
- **Test Device Key**: `edge/certs/devices/esp32-test.key`
- **App Test Certificate**: `edge/certs/devices/app-test.crt`
- **App Test Key**: `edge/certs/devices/app-test.key`
- **Purpose**: Secure device authentication to EMQX
- **Notes**: Certificates generated by `scripts/seed-certs.sh`

#### EMQX Server Certificate
- **Certificate**: `edge/certs/emqx/server.crt`
- **Key**: `edge/certs/emqx/server.key`
- **Common Name**: `emqx.safesignal.local`
- **Validity**: 365 days from generation

---

## 🔑 Environment Variables (.env)

When using environment variables (recommended for production), create `.env` file from template:

```bash
cp edge/.env.template edge/.env
```

Then update with strong passwords:

```bash
# MinIO Configuration
MINIO_ROOT_USER=safesignal-admin
MINIO_ROOT_PASSWORD=<generate-strong-password>
MINIO_ACCESS_KEY=safesignal-admin
MINIO_SECRET_KEY=<generate-strong-password>

# PostgreSQL Configuration (future)
POSTGRES_USER=safesignal
POSTGRES_PASSWORD=<generate-strong-password>
POSTGRES_DB=safesignal

# PA Service API Key
PA_SERVICE_API_KEY=<generate-random-api-key>
```

**Generate strong passwords**:
```bash
# Generate a strong random password
openssl rand -base64 32

# Generate multiple passwords at once
for i in {1..5}; do openssl rand -base64 32; done
```

---

## 📋 Quick Reference Table

| Service | URL | Username | Password | Notes |
|---------|-----|----------|----------|-------|
| **Status Dashboard** | http://localhost:5200 | - | - | No auth |
| **Grafana** | http://localhost:3000 | `admin` | `admin` | Change on first login |
| **Prometheus** | http://localhost:9090 | - | - | No auth |
| **EMQX Dashboard** | http://localhost:18083 | `admin` | `public` | Default dev creds |
| **MinIO Console** | http://localhost:9001 | `safesignal-admin` | `safesignal-dev-password-change-in-prod` | ⚠️ CHANGE IN PROD |
| **Policy Service** | http://localhost:5100 | - | - | No auth (add API key) |
| **PA Service** | http://localhost:5101 | - | - | No auth (add API key) |
| **Cloud Backend API** | http://localhost:5118 | `admin@safesignal.com` | `Admin@12345678!` | JWT Bearer + refresh (SuperAdmin) |
| **SQLite DB** | Container filesystem | - | - | `docker exec` access |

---

## 🔒 Production Security Checklist

Before deploying to production, ensure you:

### ✅ Authentication
- [ ] Change all default passwords
- [ ] Generate unique passwords for each service
- [ ] Store credentials in environment variables or secrets manager
- [ ] Add API key authentication to all API endpoints
- [ ] Enable authentication for Prometheus and Grafana
- [ ] Configure LDAP/SSO for Grafana if needed

### ✅ MinIO Security
- [ ] Enable SSL/TLS for MinIO (see `PRODUCTION_HARDENING.md`)
- [ ] Change root credentials to strong random passwords
- [ ] Create separate access keys for different services
- [ ] Configure bucket policies for least-privilege access
- [ ] Enable versioning and replication if needed

### ✅ MQTT Security
- [ ] Verify mTLS is enforced (disable port 1883 in production)
- [ ] Rotate certificates before expiry
- [ ] Implement ACLs for topic-level access control
- [ ] Change EMQX dashboard password
- [ ] Disable public access to EMQX dashboard

### ✅ Database Security
- [ ] Migrate from SQLite to PostgreSQL for production
- [ ] Use strong database password
- [ ] Restrict database network access
- [ ] Enable SSL/TLS for database connections
- [ ] Implement regular backups
- [ ] Configure read-only replicas if needed

### ✅ API Security (Cloud Backend)
- [x] JWT authentication with refresh token rotation
- [x] BCrypt password hashing (work factor 12)
- [x] Rate limiting (5 login attempts/minute per IP)
- [x] Authorization on all protected endpoints
- [x] CORS configured for mobile apps
- [x] Password policy: 12+ characters with complexity (OWASP/NIST compliant)
- [ ] Implement organization data isolation (multi-tenancy)
- [ ] Enable HTTPS/TLS for all API endpoints
- [ ] Add FluentValidation for all request DTOs
- [ ] Add audit logging for sensitive operations
- [ ] Implement role-based access control (RBAC)

### ✅ General Security
- [ ] Never commit credentials to version control
- [ ] Use `.env` files (gitignored)
- [ ] Implement secrets rotation policy
- [ ] Set up monitoring for failed authentication attempts
- [ ] Configure firewalls and network policies
- [ ] Enable audit logging across all services

---

## 📚 Related Documentation

- **Production Hardening**: See `PRODUCTION_HARDENING.md` for detailed security improvements
- **Environment Variables**: See `.env.template` for configuration template
- **Certificate Management**: Run `scripts/seed-certs.sh` to regenerate certificates
- **Service URLs**: Run `scripts/show-services.sh` for live service status

---

## ⚠️ Security Warnings

### Current MVP Limitations (DO NOT USE IN PRODUCTION AS-IS)
1. **No API Authentication** - All API endpoints are publicly accessible
2. **Default Passwords** - Services use well-known default credentials
3. **No SSL for MinIO** - Object storage uses unencrypted HTTP
4. **SQLite Database** - Not suitable for high-availability production
5. **No Rate Limiting** - APIs vulnerable to abuse
6. **Public Prometheus** - Metrics endpoint exposes system information
7. **Weak EMQX Password** - Default `admin/public` is publicly known

### Immediate Actions Required Before Production
1. Change ALL default passwords to strong random values
2. Enable SSL/TLS for all services (MinIO, APIs, etc.)
3. Implement API key authentication for all POST endpoints
4. Migrate to PostgreSQL with proper access controls
5. Restrict network access using firewalls/security groups
6. Enable audit logging and monitoring
7. Implement secrets management (AWS Secrets Manager, HashiCorp Vault, etc.)

---

**Last Updated**: 2025-11-01
**Environment**: Development/MVP
**Status**: ⚠️ NOT PRODUCTION-READY - See security checklist above

**For Production Deployment**: Follow the complete security hardening guide in `PRODUCTION_HARDENING.md`
