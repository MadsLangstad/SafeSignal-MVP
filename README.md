# SafeSignal MVP

## Quick Start
```bash
cd scripts && ./seed-certs.sh          # Generate certs (first time only)
cd ../edge && docker-compose up -d     # Start stack
cd ../scripts && ./test-mvp.sh         # Run tests
./measure-latency.sh 50                # Measure P95 latency
```

## Common Commands
```bash
# Start/stop
docker-compose up -d
docker-compose down

# Logs
docker-compose logs -f
docker logs safesignal-policy-service --tail 50
docker logs safesignal-pa-service --tail 50

# Test
./send-test-trigger.sh tenant-a building-a room-1
./measure-latency.sh 10

# Rebuild after changes
docker-compose up -d --build policy-service
docker-compose up -d --build pa-service

# Metrics
curl http://localhost:5100/metrics | grep alerts_processed_total
curl http://localhost:5101/metrics | grep pa_commands_total
```

## Ports
- EMQX: 8883 (mTLS), 18083 (dashboard: admin/public)
- Policy: 5100 (metrics + health)
- PA: 5101 (metrics + health)
- Prometheus: 9090
- Grafana: 3000 (admin/admin)

## Critical Invariants
⚠️ **Source room MUST be excluded** - Check `AlertStateMachine.cs:136-145`
- Verify: `docker logs safesignal-policy-service | grep "SOURCE ROOM EXCLUDED"`

## Topology (MVP hardcoded)
- `building-a`: room-1, room-2, room-3, room-4
- `building-b`: room-101, room-102, room-103

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

## Structure
```
edge/          # Services + docker-compose.yml
scripts/       # seed-certs.sh, test-mvp.sh, send-test-trigger.sh, measure-latency.sh
docs/edge/     # RUNBOOK, TESTPLAN, SEQUENCE, DECISION-RECORD, CHANGELOG
```
