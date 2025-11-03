# SafeSignal Cloud Backend

**Status**: ✅ MVP Complete | **Version**: 1.0 | **Date**: 2025-11-02

Multi-tenant cloud backend for SafeSignal emergency alert system. Built with .NET 9, PostgreSQL, and Docker.

---

## Quick Start

### 1. Start Infrastructure

```bash
docker-compose up -d
```

### 2. Run API

```bash
cd src/Api
dotnet run
```

### 3. Test API

```bash
./test_api.sh
```

### 4. View Swagger UI

Open: http://localhost:5118/swagger

---

## What's Included

- **18 REST API Endpoints** - Organizations, Devices, Alerts
- **11 Database Tables** - PostgreSQL with complete schema
- **Repository Pattern** - Clean architecture with EF Core
- **Docker Infrastructure** - PostgreSQL, Redis, Adminer
- **Comprehensive Docs** - 4 detailed documentation files
- **Automated Tests** - Complete API test script

---

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/v1/organizations` | Create organization |
| `GET /api/v1/organizations` | List organizations |
| `POST /api/v1/devices` | Register device |
| `GET /api/v1/devices` | List devices |
| `POST /api/v1/alerts` | Create alert |
| `GET /api/v1/alerts` | List alerts |
| `PUT /api/v1/alerts/{id}/acknowledge` | Acknowledge alert |
| `PUT /api/v1/alerts/{id}/resolve` | Resolve alert |

[See API_COMPLETE.md for full endpoint list]

---

## Documentation

| Document | Purpose |
|----------|---------|
| **ARCHITECTURE.md** | System design and database schema |
| **API_COMPLETE.md** | API documentation with examples |
| **IMPLEMENTATION_SUMMARY.md** | Complete implementation details |
| **SETUP_COMPLETE.md** | Setup guide and configuration |

---

## Technology Stack

- **.NET 9** - Web API framework
- **PostgreSQL 16** - Primary database
- **Entity Framework Core 9** - ORM
- **Redis 7** - Caching (ready for Phase 2)
- **Swagger/OpenAPI** - API documentation
- **Docker Compose** - Infrastructure

---

## Project Structure

```
cloud-backend/
├── docker-compose.yml          # Infrastructure
├── test_api.sh                 # API test script
├── src/
│   ├── Api/                    # Controllers, DTOs
│   ├── Core/                   # Entities, Interfaces
│   └── Infrastructure/         # DbContext, Repositories
└── tests/                      # Test project
```

---

## Development

### Build

```bash
dotnet build SafeSignal.Cloud.sln
```

### Migrations

```bash
# Create migration
cd src/Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Api

# Apply migration
dotnet ef database update --startup-project ../Api
```

### Database Access

**Adminer**: http://localhost:8080
- System: PostgreSQL
- Server: postgres
- Username: postgres
- Password: postgres
- Database: safesignal

---

## Status

✅ **MVP Complete**
- All core endpoints functional
- Database schema created
- Docker infrastructure running
- Documentation complete
- ⚠️ Test coverage: 2% (placeholder only)

⚠️ **Testing Status (Post-Audit 2025-11-03)**
- **Test Coverage**: 2% (not 80% as claimed)
- **Test Suite**: Placeholder only, not comprehensive
- **Integration Tests**: Not implemented
- **E2E Tests**: Not implemented

**Testing Roadmap** (Phase 4 - 70h):
- Unit tests for services, repositories, controllers (50h)
- Integration tests with test database (15h)
- E2E tests with Playwright (5h)
- Target: ≥70% coverage minimum (80% goal)

See `claudedocs/REVISED_ROADMAP.md` for production testing plan.

✅ **Actually Complete**
- JWT authentication (IMPLEMENTED)
- Site/Building/Room APIs (IMPLEMENTED)
- User management (IMPLEMENTED)
- Role-based access control (IMPLEMENTED)

⏳ **Remaining Gaps** (See REVISED_ROADMAP.md)
- Phase 1a: Security hardening (29h)
- Phase 2: All Clear endpoints (28h)
- Phase 3: API versioning (4-8h)
- Phase 4: Comprehensive testing (70h)

---

## Quick Links

- **Swagger UI**: http://localhost:5118/swagger
- **Adminer**: http://localhost:8080
- **API Base**: http://localhost:5118/api/v1

---

**Built**: 2025-11-02 | **License**: Private | **Framework**: .NET 9
