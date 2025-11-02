# SafeSignal MVP

## Quick Start
```bash
cd scripts && ./seed-certs.sh          # Generate certs (first time only)
cd ../edge && docker-compose up -d     # Start stack
cd ../scripts && ./show-services.sh    # View all service URLs
cd ../scripts && ./test-mvp.sh         # Run tests
open http://localhost:5200             # Open status dashboard
```

## Status Dashboard 📊
**New!** Web UI at http://localhost:5200 showing:
- Real-time system status
- Alert history (last 20)
- Database statistics
- Topology info (buildings, rooms, devices)
- Auto-refreshes every 30 seconds

## Common Commands
```bash
# Start/stop
docker-compose up -d
docker-compose down

# View all service URLs and status
./show-services.sh

# View dashboards
open http://localhost:5200     # Status Dashboard
open http://localhost:3000     # Grafana (admin/admin)
open http://localhost:9090     # Prometheus
open http://localhost:18083    # EMQX Dashboard (admin/public)
open http://localhost:9001     # MinIO Console

# Logs
docker-compose logs -f
docker logs safesignal-policy-service --tail 50
docker logs safesignal-pa-service --tail 50

# Test
./send-test-trigger.sh tenant-a building-a room-1
./measure-latency.sh 10
./test-mvp.sh                  # Full test suite

# Check alert history via API
curl http://localhost:5100/api/alerts | jq
curl http://localhost:5100/api/stats | jq

# Audio clip management
curl http://localhost:5101/api/clips | jq
docker exec safesignal-minio mc ls /data/audio-clips/

# Rebuild after changes
docker-compose up -d --build policy-service
docker-compose up -d --build pa-service

# Metrics
curl http://localhost:5100/metrics | grep alerts_processed_total
curl http://localhost:5101/metrics | grep pa_commands_total
```

## Ports
- Status Dashboard: 5200 (web UI - **start here!**)
- EMQX: 8883 (mTLS), 18083 (dashboard: admin/public)
- Policy: 5100 (metrics + health + API)
- PA: 5101 (metrics + health)
- Prometheus: 9090
- Grafana: 3000 (admin/admin)

## API Endpoints
**Policy Service (5100):**
- `GET /api/stats` - Database statistics
- `GET /api/alerts?limit=20` - Recent alerts
- `GET /api/alerts/{id}` - Single alert details
- `GET /metrics` - Prometheus metrics
- `GET /health` - Health check

## Database
SQLite database at `/data/safesignal.db` with:
- **Alerts**: Complete history with status tracking
- **Topology**: Buildings and rooms (editable via SQL)
- **Devices**: Registered ESP32/app devices
- **PA Confirmations**: Playback success tracking

## Critical Invariants
⚠️ **Source room MUST be excluded** - Check `AlertStateMachine.cs:268-270`
- Verify: `docker logs safesignal-policy-service | grep "SOURCE ROOM EXCLUDED"`

## Topology
**Database-driven** (with hardcoded fallback):
- `building-a`: room-1, room-2, room-3, room-4
- `building-b`: room-101, room-102, room-103
- Edit via: `docker exec -it safesignal-policy-service sqlite3 /data/safesignal.db`

## Troubleshooting
```bash
# Full reset
docker-compose down -v && rm -rf certs/
cd ../scripts && ./seed-certs.sh
cd ../edge && docker-compose up -d

# Check certs expiry
openssl x509 -in edge/certs/emqx/server.crt -enddate -noout

# MQTT debug
docker exec -it safesignal-emqx emqx ctl clients list
mosquitto_sub -h localhost -p 8883 --cafile edge/certs/ca/ca.crt \
  --cert edge/certs/devices/app-test.crt --key edge/certs/devices/app-test.key -t '#' -v
```

## Documentation

### 📚 Implementation Planning
- **[Implementation Plan](docs/IMPLEMENTATION-PLAN.md)** - Complete 8-phase roadmap from MVP to production (65-70% remaining)
- **[Implementation Summary](docs/IMPLEMENTATION-SUMMARY.md)** - Quick reference guide with timelines and resource requirements
- **[Complete System Architecture](docs/ARCHITECTURE-COMPLETE-SYSTEM.md)** - Full architecture diagrams and data flows

### 🏗️ MVP Status
**Current Completion: 30-35%**
- ✅ Edge infrastructure (EMQX, Policy Service, PA Service, SQLite)
- ✅ Basic observability (Prometheus + Grafana)
- ✅ Status dashboard and metrics
- ❌ ESP32 firmware and hardware (Phase 1 - 6 weeks)
- ❌ Cloud backend (.NET 9 APIs, PostgreSQL, Kafka) (Phase 2 - 8 weeks)
- ❌ Mobile application (React Native) (Phase 4 - 10 weeks)
- ❌ Production security (SPIFFE/SPIRE, Vault) (Phase 5 - 8 weeks)
- ❌ Communications (SMS, voice, push) (Phase 6 - 4 weeks)
- ❌ Certifications (ISO/CE/FCC) (Phase 8 - 8 weeks)

**Estimated Timeline to Production**: 8-10 months with 11-14 person team

### 📖 Edge Documentation
- **[Runbook](docs/edge/RUNBOOK-Local-E2E.md)** - Operational procedures
- **[Test Plan](docs/edge/TESTPLAN-Latency.md)** - Latency testing
- **[Decision Record](docs/edge/DECISION-RECORD.md)** - Architectural decisions
- **[Changelog](docs/edge/CHANGELOG-MVP.md)** - MVP development history

## Structure
```
edge/          # Services + docker-compose.yml
scripts/       # seed-certs.sh, test-mvp.sh, send-test-trigger.sh, measure-latency.sh
docs/          # Implementation planning and system architecture
docs/edge/     # RUNBOOK, TESTPLAN, SEQUENCE, DECISION-RECORD, CHANGELOG
```
