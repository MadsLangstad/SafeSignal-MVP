# SafeSignal MVP - Audit-Aligned Production Roadmap

**Document Version**: 2.0 (Revised)
**Created**: 2025-11-03
**Timeline**: **6-8 weeks** (realistic, audit-based)
**Objective**: Achieve production readiness through audit-driven prioritization

---

## Executive Summary

**Revised Approach**: After comprehensive code audits, this roadmap reflects **actual gaps** rather than assumed needs. Timeline extended from 4-6 to **6-8 weeks** to accommodate:

1. **Documentation correction** (Phase 0 - NEW)
2. **Backend security hardening** from audit findings (integrated throughout)
3. **Mobile security fixes** prioritized early (Phase 1b - MOVED)
4. **Firmware security** split into critical (Phase 1c) vs. advanced (deferred)
5. **Realistic testing targets** (critical paths first, 80% as goal)

**Total Estimated Effort**: 200-240 hours with 2-3 engineers

---

## Phase 0: Documentation Alignment (Week 0 - Days 1-2)

**NEW PHASE** - Missed in original roadmap

### Objective
Establish honest baseline before implementation work begins

### Tasks

**Status Documents** (4 hours)
- `MVP_STATUS.md` - Update security claims from "✅" to "⚠️ In Progress"
- `PROJECT_STATUS.md` - Correct mobile app readiness, API versioning status
- Mark ATECC, SPIFFE/SPIRE as "Planned Phase 3+" not "Complete"

**API Documentation** (2 hours)
- `cloud-backend/API_COMPLETE.md` - Change `/api/v1/...` to `/api/[controller]` OR add note "Planned for Phase 2"
- `safesignal-doc/documentation/development/api-documentation.md` - Same correction
- Update curl examples to work with current implementation

**Architecture Documentation** (1 hour)
- `documentation/technical-specification.md` - Mark All Clear as "Phase 1 deliverable"
- `documentation/development/testing-strategy.md` - Update coverage from "≥80% complete" to "Phase 4 target"

**Mobile Documentation** (1 hour)
- `documentation/development/mobile-app-specification.md` - Update screen names to match actual implementation
- Add section on offline-first capabilities (currently undocumented strength)

**Firmware Documentation** (0.5 hours)
- Align `MVP_STATUS.md` security claims with honest `firmware/README.md` development disclosures

**Deliverables**
- [ ] All documentation accurately reflects MVP state
- [ ] No claims that aren't implemented
- [ ] Clear roadmap references for planned features
- [ ] Stakeholder communication prepared

**Acceptance Criteria**
- [ ] Engineer can follow API docs and successfully make requests
- [ ] Status documents match audit findings
- [ ] No contradictions between documents
- [ ] Clear separation of "implemented" vs "planned"

**Effort**: 8-10 hours
**Timeline**: 1-2 days
**Dependencies**: None (start immediately)

---

## Phase 1: Critical Security & Safety (Weeks 1-3)

### Phase 1a: Backend Security Hardening (Week 1)

**NEW** - From backend audit critical findings

**Priority 1: Authentication Security** (6 hours)

**Files to Modify**:
- `cloud-backend/src/Api/Controllers/AuthController.cs` [MODIFY]
- `cloud-backend/src/Core/Application/Services/AuthService.cs` [NEW]
- `cloud-backend/src/Infrastructure/Security/RateLimitingMiddleware.cs` [NEW]

**Tasks**:
1. **Login Brute Force Protection** (4h)
   ```csharp
   // Add rate limiting middleware
   public class RateLimitingMiddleware
   {
       private static readonly ConcurrentDictionary<string, List<DateTime>> LoginAttempts;
       private const int MaxAttemptsPerMinute = 5;

       public async Task InvokeAsync(HttpContext context)
       {
           var ipAddress = context.Connection.RemoteIpAddress?.ToString();
           // Track and limit login attempts
       }
   }
   ```

2. **Password Complexity** (1h)
   ```csharp
   // Update password validation
   public class PasswordPolicy
   {
       public const int MinLength = 12; // Up from 6
       public static bool RequiresUppercase = true;
       public static bool RequiresDigit = true;
       public static bool RequiresSpecialChar = true;
   }
   ```

3. **JWT Configuration Fix** (1h)
   - Remove fallback to weak defaults (cloud-backend/src/Api/Startup.cs:45-72)
   - Require environment variables, fail fast if missing
   - Set minimum key length requirement (256 bits)

**Acceptance Criteria**:
- [ ] Login limited to 5 attempts/minute per IP
- [ ] Passwords must be 12+ chars with complexity
- [ ] JWT secrets required in environment, no defaults
- [ ] Unit tests for rate limiting logic

---

**Priority 2: Input Validation** (6 hours)

**Files to Create**:
- `cloud-backend/src/Api/Validators/CreateAlertValidator.cs` [NEW]
- `cloud-backend/src/Api/Validators/BuildingValidator.cs` [NEW]
- `cloud-backend/src/Api/Validators/UserRegistrationValidator.cs` [NEW]

**Tasks**:
1. **Install FluentValidation** (0.5h)
   ```bash
   dotnet add package FluentValidation.AspNetCore
   ```

2. **Create Validators** (3h)
   ```csharp
   public class CreateAlertValidator : AbstractValidator<CreateAlertRequest>
   {
       public CreateAlertValidator()
       {
           RuleFor(x => x.DeviceId)
               .NotEmpty()
               .MaximumLength(50)
               .Matches("^[a-zA-Z0-9_-]+$"); // Prevent injection

           RuleFor(x => x.BuildingId)
               .NotEmpty()
               .Must(BeValidGuid);
       }
   }
   ```

3. **Apply to Controllers** (2h)
   - Add `[Validate]` attributes to POST/PUT endpoints
   - Update AlertsController, BuildingsController, UsersController

4. **Tests** (0.5h)

**Acceptance Criteria**:
- [ ] All POST/PUT endpoints have validation
- [ ] Invalid requests return 400 with clear errors
- [ ] SQL injection attempts blocked
- [ ] XSS payloads rejected

---

**Priority 3: Audit Logging** (8 hours)

**Files to Create**:
- `cloud-backend/src/Core/Domain/Models/AuditLog.cs` [NEW]
- `cloud-backend/src/Core/Application/Services/AuditService.cs` [NEW]
- `cloud-backend/src/Api/Middleware/AuditLoggingMiddleware.cs` [NEW]
- `cloud-backend/src/Infrastructure/Data/Migrations/AddAuditLogs.cs` [NEW]

**Database Schema**:
```sql
CREATE TABLE AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NULL,
    Action VARCHAR(100) NOT NULL,
    Resource VARCHAR(100) NOT NULL,
    ResourceId VARCHAR(100) NULL,
    IpAddress VARCHAR(45) NOT NULL,
    UserAgent VARCHAR(500) NULL,
    Timestamp DATETIME NOT NULL,
    OrganizationId UNIQUEIDENTIFIER NOT NULL,
    Details NVARCHAR(MAX) NULL -- JSON
);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
```

**Actions to Log**:
- Login success/failure
- Alert creation/acknowledgment/resolution
- All Clear initiation/approval
- User creation/deletion
- Building creation/modification
- Role changes

**Acceptance Criteria**:
- [ ] All sensitive actions logged
- [ ] Logs include user, IP, timestamp, action
- [ ] Logs queryable for forensics
- [ ] Retention policy documented

---

**Priority 4: Token Blacklist** (6 hours)

**From Audit**: "Token blacklist missing (logout ineffective)"

**Files to Create**:
- `cloud-backend/src/Core/Application/Services/TokenBlacklistService.cs` [NEW]
- `cloud-backend/src/Infrastructure/Data/Redis/RedisTokenBlacklist.cs` [NEW]

**Implementation**:
```csharp
public interface ITokenBlacklistService
{
    Task BlacklistTokenAsync(string token, DateTime expiration);
    Task<bool> IsTokenBlacklistedAsync(string token);
}

// Store in Redis with expiration matching JWT expiration
public class RedisTokenBlacklist : ITokenBlacklistService
{
    public async Task BlacklistTokenAsync(string token, DateTime expiration)
    {
        var ttl = expiration - DateTime.UtcNow;
        await _redis.StringSetAsync($"blacklist:{token}", "1", ttl);
    }
}
```

**Integration**:
- Update AuthController.Logout to blacklist token
- Update JWT middleware to check blacklist on each request

**Acceptance Criteria**:
- [ ] Logout invalidates JWT immediately
- [ ] Blacklisted tokens rejected
- [ ] Expired tokens auto-removed from blacklist
- [ ] Performance impact <10ms per request

---

**Priority 5: Environment-Specific Configuration** (3 hours)

**From Audit**: "Environment config fallback dangerous"

**Files to Modify**:
- `cloud-backend/src/Api/appsettings.Development.json` [MODIFY]
- `cloud-backend/src/Api/appsettings.Production.json` [NEW]
- `cloud-backend/src/Api/appsettings.Staging.json` [NEW]

**Tasks**:
1. **Separate Secrets** (1h)
   - Development: Use weak secrets (clearly marked)
   - Production: Require Azure Key Vault or env vars
   - Never commit production secrets

2. **Error Detail Control** (1h)
   ```json
   // appsettings.Production.json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Warning"
       }
     },
     "ErrorHandling": {
       "IncludeStackTrace": false,
       "IncludeExceptionDetails": false
     }
   }
   ```

3. **Validation** (1h)
   - Fail fast if production config missing
   - Add config validation on startup

**Acceptance Criteria**:
- [ ] Development/Production configs separated
- [ ] Production never exposes stack traces
- [ ] Missing production secrets cause startup failure
- [ ] Secrets loaded from Key Vault or env vars

---

**Phase 1a Summary**:
- **Effort**: 29 hours
- **Timeline**: Week 1 (5-6 working days with 1 backend engineer)
- **Deliverables**: Backend passes critical security audit criteria

---

### Phase 1b: Mobile Security Hardening (Week 1-2)

**MOVED FROM PHASE 5** - Critical audit findings

**Priority 1: Error Boundary** (2 hours)

**From Audit**: "No error boundary - app crashes on render errors"

**Files to Create**:
- `mobile/src/components/ErrorBoundary.tsx` [NEW]

**Implementation**:
```typescript
export class ErrorBoundary extends React.Component<Props, State> {
  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Log to Sentry
    Sentry.captureException(error, { extra: errorInfo });
    this.setState({ hasError: true, error });
  }

  render() {
    if (this.state.hasError) {
      return <ErrorFallbackScreen error={this.state.error} />;
    }
    return this.props.children;
  }
}
```

**Integration**:
- Wrap App root in ErrorBoundary
- Add ErrorFallbackScreen with reset button

**Acceptance Criteria**:
- [ ] Render errors don't crash app
- [ ] User sees friendly error message
- [ ] Errors logged to crash reporting
- [ ] User can reset and retry

---

**Priority 2: Certificate Pinning** (4 hours)

**From Audit**: "No certificate pinning - MITM vulnerability"

**Files to Modify**:
- `mobile/src/services/api.ts` [MODIFY]
- `mobile/app.json` [MODIFY]

**Implementation**:
```typescript
import { Platform } from 'react-native';
import NetInfo from '@react-native-community/netinfo';

// Add SSL pinning
const api = axios.create({
  baseURL: API_URL,
  httpsAgent: Platform.OS === 'ios'
    ? new https.Agent({
        ca: [SERVER_CERT],
        rejectUnauthorized: true,
      })
    : undefined,
});

// For Android, use react-native-ssl-pinning
```

**Tasks**:
1. Install `react-native-ssl-pinning` (0.5h)
2. Configure pins for production server (1h)
3. Add fallback for development (0.5h)
4. Test MITM attack resistance (1h)
5. Document certificate rotation procedure (1h)

**Acceptance Criteria**:
- [ ] Invalid certificates rejected
- [ ] MITM proxy tools blocked
- [ ] Development env still works
- [ ] Certificate rotation documented

---

**Priority 3: Crash Reporting** (2 hours)

**From Audit**: "No crash reporting - blind to production issues"

**Files to Modify**:
- `mobile/App.tsx` [MODIFY]
- `mobile/package.json` [MODIFY]

**Implementation**:
```bash
npm install @sentry/react-native
npx @sentry/wizard -i reactNative
```

```typescript
Sentry.init({
  dsn: SENTRY_DSN,
  environment: __DEV__ ? 'development' : 'production',
  enableAutoSessionTracking: true,
  sessionTrackingIntervalMillis: 10000,
  tracesSampleRate: 0.2,
});
```

**Acceptance Criteria**:
- [ ] Crashes reported to Sentry
- [ ] User context included (anonymized)
- [ ] Breadcrumbs for debugging
- [ ] Source maps uploaded for stack traces

---

**Priority 4: Fix Biometric Login** (3 hours)

**From Audit**: "Incomplete biometric login - feature doesn't work"

**Files to Modify**:
- `mobile/src/services/biometrics.ts` [FIX]
- `mobile/src/screens/LoginScreen.tsx` [MODIFY]

**Tasks**:
1. Debug existing biometric code (1h)
2. Fix authentication flow (1h)
3. Add fallback to password (0.5h)
4. Test on iOS and Android (0.5h)

**Acceptance Criteria**:
- [ ] Biometric login works on iOS
- [ ] Biometric login works on Android
- [ ] Fallback to password functional
- [ ] User can enable/disable biometrics

---

**Priority 5: Logging Hygiene** (2 hours)

**From Audit**: "console.log of sensitive data (MOBILE_AUDIT_ISSUES.md:11)"

**Files to Audit/Fix**:
- Search for `console.log` in all mobile files
- Remove or sanitize sensitive data logging
- Replace with conditional debug logging

**Implementation**:
```typescript
// Create debug logger
const logger = {
  debug: (msg: string, data?: any) => {
    if (__DEV__) {
      console.log(`[DEBUG] ${msg}`, data);
    }
  },
  // Never log tokens, passwords, or PII
};
```

**Acceptance Criteria**:
- [ ] No tokens/passwords in logs
- [ ] Production logging disabled
- [ ] Debug logs conditional on __DEV__
- [ ] Audit trail clean

---

**Phase 1b Summary**:
- **Effort**: 13 hours
- **Timeline**: Week 1-2 (2-3 days with 1 mobile engineer, parallel to backend work)
- **Deliverables**: Mobile app passes critical security audit

---

### Phase 1c: Firmware Critical Security (Week 2)

**SPLIT FROM PHASE 2** - Critical only, defer advanced features

**Priority 1: Replace Hardcoded Credentials** (8 hours)

**From Audit**: "CRITICAL - Hardcoded WiFi credentials in firmware"

**Files to Modify**:
- `firmware/esp32-button/include/config.h` [MODIFY]
- `firmware/esp32-button/main/wifi.c` [MODIFY]
- `firmware/esp32-button/main/provisioning.c` [NEW]

**Implementation**:
```c
// Remove from config.h:
// #define WIFI_SSID "SafeSignal-Edge"
// #define WIFI_PASS "safesignal-dev"

// Replace with NVS storage:
esp_err_t wifi_init_from_nvs(void) {
    nvs_handle_t nvs;
    esp_err_t ret = nvs_open("wifi_config", NVS_READONLY, &nvs);
    if (ret == ESP_OK) {
        size_t ssid_len, pass_len;
        nvs_get_str(nvs, "ssid", NULL, &ssid_len);
        nvs_get_str(nvs, "password", NULL, &pass_len);
        // Load encrypted credentials
    }
}
```

**Tasks**:
1. Implement NVS credential storage (2h)
2. Create factory provisioning mode (3h)
   - Button held during boot enters provisioning
   - BLE or AP mode for credential entry
   - Credentials encrypted before NVS storage
3. Update deployment docs (1h)
4. Test provisioning flow (2h)

**Acceptance Criteria**:
- [ ] No credentials in source code
- [ ] Credentials stored encrypted in NVS
- [ ] Provisioning mode functional
- [ ] Credentials can be changed without reflash

---

**Priority 2: Provision Real Certificates** (4 hours)

**From Audit**: "Placeholder certificates - TLS won't work"

**Files to Modify**:
- `firmware/esp32-button/certs/` [UPDATE ALL]
- `scripts/provision-device-cert.sh` [NEW]

**Tasks**:
1. Generate CA certificate for device fleet (1h)
2. Create per-device certificate script (1h)
3. Update firmware to load real certs (1h)
4. Test TLS connection to MQTT broker (1h)

**Implementation**:
```bash
# scripts/provision-device-cert.sh
#!/bin/bash
DEVICE_ID=$1
openssl genrsa -out device_${DEVICE_ID}.key 2048
openssl req -new -key device_${DEVICE_ID}.key -out device_${DEVICE_ID}.csr
openssl x509 -req -in device_${DEVICE_ID}.csr -CA ca.crt -CAkey ca.key \
  -out device_${DEVICE_ID}.crt -days 365
```

**Acceptance Criteria**:
- [ ] Valid CA certificate generated
- [ ] Per-device certificates signed by CA
- [ ] TLS handshake succeeds
- [ ] MQTT connection working

---

**Priority 3: Enable Secure Boot** (6 hours)

**From Audit**: "No secure boot - firmware extractable"

**Tasks**:
1. Generate secure boot signing key (1h)
2. Configure ESP32 secure boot V2 (2h)
3. Flash secure bootloader (1h)
4. Test boot with signature verification (1h)
5. Document key management (1h)

**Commands**:
```bash
# Generate secure boot key
espsecure.py generate_signing_key secure_boot_key.pem

# Configure secure boot in sdkconfig
CONFIG_SECURE_BOOT=y
CONFIG_SECURE_BOOT_V2_ENABLED=y

# Build and flash
idf.py build
esptool.py --port /dev/ttyUSB0 write_flash 0x0 bootloader.bin
```

**Acceptance Criteria**:
- [ ] Secure boot enabled
- [ ] Unsigned firmware rejected
- [ ] Signing key stored securely
- [ ] Rollback protection enabled

---

**Priority 4: Enable Flash Encryption** (8 hours)

**From Audit**: "No flash encryption - credentials extractable"

**Tasks**:
1. Generate flash encryption key (1h)
2. Configure flash encryption in sdkconfig (2h)
3. Encrypt NVS partition (2h)
4. Flash encrypted firmware (1h)
5. Test credential protection (1h)
6. Document recovery procedures (1h)

**Configuration**:
```
CONFIG_SECURE_FLASH_ENC_ENABLED=y
CONFIG_SECURE_FLASH_ENCRYPTION_MODE_RELEASE=y
CONFIG_SECURE_FLASH_REQUIRE_ENCRYPTED_APP_IMAGES=y
```

**Acceptance Criteria**:
- [ ] Flash encryption enabled
- [ ] NVS credentials encrypted
- [ ] Physical extraction blocked
- [ ] Recovery procedure documented

---

**DEFER to Post-MVP**:
- ❌ ATECC608A integration (16-24h) - Complex hardware dependency
- ❌ SPIFFE/SPIRE (40-60h) - Advanced feature, not MVP-critical
- ❌ OTA updates (12-16h) - Can be manual for MVP

**Rationale**: Firmware security audit shows strong reliability (9/10) but development-grade security. Phase 1c addresses **critical** security gaps (hardcoded creds, no encryption) while deferring **advanced** features (hardware crypto, automated rotation) to later phases when core system is stable.

**Phase 1c Summary**:
- **Effort**: 26 hours
- **Timeline**: Week 2 (4-5 days with 1 firmware engineer)
- **Deliverables**: Firmware moves from development-grade to production-acceptable security

---

**Phase 1 Total Summary**:
- **Backend Security**: 29 hours
- **Mobile Security**: 13 hours
- **Firmware Security**: 26 hours
- **Total Effort**: 68 hours
- **Timeline**: Weeks 1-3 (can run in parallel with 3 engineers)
- **Deliverables**: All **critical** security audit findings resolved

---

## Phase 2: All Clear Safety Feature (Weeks 3-4)

**UNCHANGED from original** - Correctly prioritized

### 2.1 Backend Implementation (16 hours)

**Database Schema** (2 hours)
```sql
ALTER TABLE Alerts ADD COLUMN AllClearStatus VARCHAR(50) DEFAULT 'not_initiated';
ALTER TABLE Alerts ADD COLUMN AllClearInitiatedBy UNIQUEIDENTIFIER NULL;
ALTER TABLE Alerts ADD COLUMN AllClearInitiatedAt DATETIME NULL;

CREATE TABLE AllClearApprovals (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    AlertId UNIQUEIDENTIFIER NOT NULL,
    ApprovedBy UNIQUEIDENTIFIER NOT NULL,
    ApprovedAt DATETIME NOT NULL,
    ApproverRole VARCHAR(100) NOT NULL,
    FOREIGN KEY (AlertId) REFERENCES Alerts(Id),
    FOREIGN KEY (ApprovedBy) REFERENCES Users(Id)
);
```

**API Endpoints** (6 hours)
- `POST /api/alerts/{id}/all-clear/initiate`
- `POST /api/alerts/{id}/all-clear/approve`
- `GET /api/alerts/{id}/all-clear/status`
- `DELETE /api/alerts/{id}/all-clear/cancel`

**Business Logic** (6 hours)
- Two distinct users required
- Role separation enforced
- 5-minute timeout window
- Audit logging integration
- Unit tests (≥90% coverage)

**Acceptance Criteria**:
- [ ] Dual approval enforced
- [ ] Timeout mechanism working
- [ ] Full audit trail
- [ ] Tests passing

---

### 2.2 Mobile UI (12 hours)

**Screens**:
- `AllClearInitiateScreen.tsx` (4h)
- `AllClearApproveScreen.tsx` (4h)
- `AllClearStatus.tsx` component (2h)
- Navigation integration (1h)
- Tests (1h)

**Features**:
- Push notifications for approval requests
- Real-time status updates
- Role-based visibility
- Countdown timer
- Clear error handling

**Acceptance Criteria**:
- [ ] Intuitive two-step workflow
- [ ] Real-time updates working
- [ ] Push notifications functional
- [ ] WCAG AA compliant

---

**Phase 2 Summary**:
- **Effort**: 28 hours
- **Timeline**: Weeks 3-4 (4-5 days with backend + mobile engineer)
- **Deliverables**: Safety-critical All Clear feature complete

---

## Phase 3: API Versioning & Documentation (Week 4)

**COMBINED** - API migration + docs update

### 3.1 Backend API Versioning (4 hours)

**Option A: Implement /api/v1/...** (4h)
```csharp
services.AddApiVersioning(options => {
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
});
```

**Option B: Update Documentation** (2h)
- Change all docs to show `/api/[controller]`
- Add note: "v1 versioning planned for Phase 6"

**Recommendation**: **Option B** for MVP, defer versioning to post-production

---

### 3.2 Documentation Updates (4 hours)

**Files to Update**:
- `cloud-backend/API_COMPLETE.md`
- `safesignal-doc/documentation/development/api-documentation.md`
- All curl examples
- Postman collection

**Acceptance Criteria**:
- [ ] All examples work as shown
- [ ] No documentation-implementation mismatch
- [ ] Migration guide if versioning implemented

---

**Phase 3 Summary**:
- **Effort**: 4-8 hours (depending on option chosen)
- **Timeline**: Week 4 (1-2 days)
- **Deliverables**: API docs match implementation

---

## Phase 4: Testing Infrastructure (Weeks 5-6)

**REVISED** - Realistic targets, critical paths first

### 4.1 Backend Unit Tests (30 hours)

**Target**: **70% coverage minimum, 80% goal**

**Critical Path Tests** (20 hours):
- AllClearService (full coverage required)
- AlertService (critical paths)
- AuthService (security-critical)
- Controllers (happy path + error cases)

**Repository Tests** (5 hours):
- Alert queries
- Multi-tenant isolation

**Test Infrastructure** (5 hours):
- TestDbContextFactory
- JwtTokenGenerator
- MockDataBuilder

**Acceptance Criteria**:
- [ ] ≥70% coverage achieved
- [ ] All critical paths tested
- [ ] CI pipeline running tests
- [ ] Coverage reports generated

---

### 4.2 Integration Tests (16 hours)

**Critical Flows**:
- Complete alert flow (button → cloud → mobile)
- All Clear workflow with database
- Multi-tenant isolation
- Authentication flows

**Acceptance Criteria**:
- [ ] E2E integration tests passing
- [ ] Real database interactions tested
- [ ] CI pipeline running integration tests

---

### 4.3 E2E Tests (16 hours)

**Using Playwright MCP**:
- Login → view buildings → alert → acknowledge (4h)
- All Clear two-person workflow (6h)
- Alert history and settings (4h)
- Accessibility validation (2h)

**Acceptance Criteria**:
- [ ] Critical user journeys tested
- [ ] Screenshot regression tests
- [ ] WCAG validation passing

---

### 4.4 Hardware-in-Loop (8 hours)

**Basic HIL Suite**:
- Button press → alert delivery (4h)
- Network failure recovery (2h)
- Device authentication (2h)

**Defer Advanced Testing**:
- ❌ Chaos engineering (defer to Phase 7)
- ❌ Penetration testing (defer to Phase 7)
- ❌ Load testing (defer to Phase 7)

**Acceptance Criteria**:
- [ ] HIL test harness functional
- [ ] Critical hardware paths validated
- [ ] Runnable in CI

---

**Phase 4 Summary**:
- **Effort**: 70 hours
- **Timeline**: Weeks 5-6 (12-14 days with 1-2 QA + 1 backend engineer)
- **Deliverables**: ≥70% test coverage, critical paths validated

---

## Phase 5: Mobile App Completion (Week 6-7)

**REFOCUSED** - Store readiness only, security already done in Phase 1b

### 5.1 Screen Alignment (4 hours)

**From Audit**: "Screen names don't match spec"

**Tasks**:
- Rename screens to match specification (2h)
- Update navigation (1h)
- Verify all functionality preserved (1h)

**Acceptance Criteria**:
- [ ] Screen names match mobile-app-specification.md
- [ ] No functionality lost
- [ ] Navigation working

---

### 5.2 App Store Preparation (8 hours)

**Assets** (4h):
- App icons (all sizes)
- Launch/splash screens
- Store screenshots
- Feature graphics

**Legal** (2h):
- Privacy policy
- Terms of service

**Configuration** (2h):
- Update app.json metadata
- Configure code signing
- Set up CI/CD for builds

**Acceptance Criteria**:
- [ ] All store assets ready
- [ ] Legal docs finalized
- [ ] Test build submitted to TestFlight/Internal Testing

---

**Phase 5 Summary**:
- **Effort**: 12 hours
- **Timeline**: Week 6-7 (2-3 days with 1 mobile engineer)
- **Deliverables**: App store submission-ready

---

## Phase 6: Production Deployment (Week 7-8)

**NEW PHASE** - Deployment readiness

### 6.1 CI/CD Pipeline (8 hours)

**Backend**:
- GitHub Actions for build/test/deploy
- Azure DevOps pipelines
- Automated testing on PR
- Deploy to staging/production

**Mobile**:
- EAS Build for iOS/Android
- Automated build on tag
- TestFlight/Internal Testing distribution

**Firmware**:
- Automated firmware build
- Secure signing in CI
- OTA update package generation (if time permits)

**Acceptance Criteria**:
- [ ] CI running on every commit
- [ ] Tests block broken builds
- [ ] Automated deployment to staging
- [ ] Production deploy requires approval

---

### 6.2 Monitoring & Alerting (6 hours)

**Application Insights** (Azure):
- Backend API monitoring
- Performance metrics
- Error tracking
- Custom alerts

**Mobile**:
- Sentry error tracking (already in Phase 1b)
- Analytics integration

**Firmware**:
- Device health monitoring
- MQTT message monitoring

**Acceptance Criteria**:
- [ ] Application Insights configured
- [ ] Critical alerts defined
- [ ] Dashboard for operations team
- [ ] Incident response runbook

---

### 6.3 Production Runbooks (4 hours)

**Documentation**:
- Deployment procedure
- Rollback procedure
- Incident response
- Device provisioning
- Certificate rotation

**Acceptance Criteria**:
- [ ] Runbooks tested in staging
- [ ] Operations team trained
- [ ] Emergency contacts documented

---

**Phase 6 Summary**:
- **Effort**: 18 hours
- **Timeline**: Week 7-8 (3-4 days with DevOps engineer)
- **Deliverables**: Production deployment ready

---

## Deferred to Post-MVP (Phase 7+)

**Advanced Firmware Security**:
- ATECC608A hardware crypto (16-24h)
- SPIFFE/SPIRE cert rotation (40-60h)
- OTA firmware updates (12-16h)

**Advanced Testing**:
- Chaos engineering (8-12h)
- Professional penetration testing (external vendor)
- Load testing (8-12h)

**API Enhancements**:
- Full API versioning migration (6-8h)
- GraphQL endpoint (if needed)
- WebSocket for real-time (if needed beyond All Clear)

**Mobile Features**:
- Advanced analytics
- Offline mode enhancements
- Additional customization

**Rationale**: These features are valuable but not MVP-blocking. Deliver core safety and security first, enhance iteratively based on user feedback.

---

## Timeline & Resource Allocation

### Week 0 (Days 1-2): Documentation Alignment
**Team**: 1 technical writer or engineer
**Effort**: 8-10 hours
**Deliverables**: Honest documentation baseline

### Weeks 1-3: Critical Security & Safety
**Team**: 1 backend, 1 mobile, 1 firmware engineer (parallel work)
**Effort**: 68 hours total (23h per engineer)
**Deliverables**: All critical audit findings resolved

### Weeks 3-4: All Clear Feature
**Team**: 1 backend, 1 mobile engineer
**Effort**: 28 hours
**Deliverables**: Safety-critical All Clear complete

### Week 4: API & Docs
**Team**: 1 backend engineer
**Effort**: 4-8 hours
**Deliverables**: API docs match implementation

### Weeks 5-6: Testing
**Team**: 1-2 QA engineers, 1 backend engineer
**Effort**: 70 hours
**Deliverables**: ≥70% test coverage

### Weeks 6-7: Mobile Store Prep
**Team**: 1 mobile engineer
**Effort**: 12 hours
**Deliverables**: App store ready

### Weeks 7-8: Production Readiness
**Team**: 1 DevOps engineer
**Effort**: 18 hours
**Deliverables**: Deployment pipeline + monitoring

**Total Timeline**: **6-8 weeks**
**Total Effort**: **208-218 hours**
**Team Size**: 2-3 engineers + 1-2 QA + 1 DevOps (can overlap)

---

## Risk Management

### High-Risk Items (Revised)

**1. Timeline Pressure**
- **Risk**: Stakeholders expect 4-6 weeks based on original estimate
- **Mitigation**: Phase 0 sets honest expectations with audit evidence
- **Contingency**: Defer Phase 6 (deployment automation) if needed

**2. Security Hardening Scope**
- **Risk**: Backend security tasks may uncover additional issues
- **Mitigation**: Limit to audit-identified items only, document rest for Phase 7
- **Contingency**: Defer lower-priority items (soft delete, some logging)

**3. All Clear UX Complexity**
- **Risk**: Two-person workflow may confuse users
- **Mitigation**: User testing with mockups before implementation
- **Contingency**: Add tutorial/walkthrough

**4. Test Coverage Target**
- **Risk**: 80% coverage may be difficult to achieve
- **Mitigation**: Focus on critical paths, accept 70% minimum
- **Contingency**: Document uncovered code as "low-risk"

---

## Success Criteria

### Phase 0 Success
- [ ] Documentation matches implementation
- [ ] No contradictory claims
- [ ] Stakeholders aware of honest timeline

### Phase 1 Success (Critical Security)
- [ ] Backend passes all critical audit findings
- [ ] Mobile has error boundary, cert pinning, crash reporting
- [ ] Firmware has encrypted credentials, real certs, secure boot

### Phase 2 Success (All Clear)
- [ ] Two-person approval workflow functional
- [ ] Audit trail complete
- [ ] Mobile UI intuitive

### Phase 3 Success (API/Docs)
- [ ] API documentation accurate
- [ ] All curl examples work

### Phase 4 Success (Testing)
- [ ] ≥70% backend coverage
- [ ] Integration tests passing
- [ ] E2E tests for critical flows
- [ ] HIL tests functional

### Phase 5 Success (Mobile Store)
- [ ] All store assets created
- [ ] Legal docs approved
- [ ] Test builds submitted

### Phase 6 Success (Production)
- [ ] CI/CD pipeline functional
- [ ] Monitoring configured
- [ ] Runbooks documented
- [ ] **PRODUCTION DEPLOYMENT READY**

---

## Effort Summary by Category

| Category | Original Estimate | Revised Estimate | Difference |
|----------|------------------|------------------|------------|
| Documentation | 0h (missing) | 10h | +10h |
| Backend Security | 0h (missing) | 29h | +29h |
| Mobile Security | 0h (Phase 5) | 13h (Phase 1b) | Moved earlier |
| Firmware Security | 24h (ATECC+SPIRE) | 26h (critical only) | +2h, deferred 56h |
| All Clear | 28h | 28h | No change |
| API Versioning | 6h | 4-8h | Reduced (option B) |
| Testing | 80h | 70h | Reduced (70% target) |
| Mobile Screens | 12h | 12h | No change |
| Production Deployment | 0h (missing) | 18h | +18h |
| **Total** | **154-204h** | **208-218h** | **More realistic** |

**Timeline**: 4-6 weeks → **6-8 weeks** (realistic with proper phasing)

---

## Alignment with Audit Findings

### Backend Audit Critical Items ✅
- [x] Brute force protection → Phase 1a
- [x] Password policy → Phase 1a
- [x] Input validation → Phase 1a
- [x] Audit logging → Phase 1a
- [x] Token blacklist → Phase 1a
- [x] Environment config → Phase 1a
- [x] All Clear feature → Phase 2

### Mobile Audit Critical Items ✅
- [x] Error boundary → Phase 1b
- [x] Certificate pinning → Phase 1b
- [x] Crash reporting → Phase 1b
- [x] Biometric fix → Phase 1b
- [x] Logging hygiene → Phase 1b
- [x] Screen alignment → Phase 5
- [x] Store assets → Phase 5

### Firmware Audit Critical Items ✅
- [x] Hardcoded credentials → Phase 1c
- [x] Real certificates → Phase 1c
- [x] Secure boot → Phase 1c
- [x] Flash encryption → Phase 1c
- [ ] ATECC integration → Deferred to Phase 7
- [ ] SPIFFE/SPIRE → Deferred to Phase 7

---

## Next Steps

### Immediate (This Week)

1. **Stakeholder Meeting** (1 hour)
   - Present audit findings
   - Share revised 6-8 week timeline
   - Get approval for Phase 0 docs update

2. **Phase 0 Kickoff** (Days 1-2)
   - Update all documentation to reflect reality
   - Prepare honest status communications
   - Set realistic expectations

3. **Team Planning** (2 hours)
   - Confirm engineer availability
   - Assign phases to team members
   - Set up sprint structure

### Week 1 Kickoff

1. **Phase 1a: Backend Security** begins
2. **Phase 1b: Mobile Security** begins (parallel)
3. Daily standups established
4. Sprint tracking in Jira/GitHub Projects

### Ongoing

1. Weekly progress reviews
2. Bi-weekly stakeholder demos
3. Risk register updates
4. Continuous documentation updates

---

## Conclusion

This revised roadmap is **audit-aligned, realistic, and production-focused**:

✅ **Phase 0 added**: 1-2 day docs update (missing in original)
✅ **Timeline extended**: 4-6 weeks → 6-8 weeks (honest estimate)
✅ **Backend security**: All critical audit items in Phase 1a
✅ **Mobile security**: Moved to Phase 1b (was buried in Phase 5)
✅ **Firmware security**: Split critical (Phase 1c) vs. advanced (deferred)
✅ **Testing realistic**: 70% minimum, 80% goal (not unrealistic 100%)
✅ **Production deployment**: New Phase 6 for CI/CD, monitoring
✅ **Deferred advanced features**: ATECC, SPIFFE/SPIRE, chaos testing to Phase 7+

**Result**: A roadmap that delivers **production-ready SafeSignal** in 6-8 weeks with proper security hardening, honest documentation, and critical safety features.

---

**Document Control**
**Version**: 2.0 (Audit-Aligned Revision)
**Author**: Claude (SuperClaude Framework)
**Supersedes**: IMPLEMENTATION_ROADMAP.md v1.0
**Approved**: [Pending stakeholder review]
**Next Review**: After Phase 0 completion
