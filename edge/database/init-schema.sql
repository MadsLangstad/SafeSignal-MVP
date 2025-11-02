-- SafeSignal Edge Database Schema
-- SQLite 3.x compatible

-- Alert History
CREATE TABLE IF NOT EXISTS alerts (
    alert_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    building_id TEXT NOT NULL,
    source_room_id TEXT NOT NULL,
    source_device_id TEXT,
    mode TEXT NOT NULL CHECK(mode IN ('SILENT', 'AUDIBLE', 'LOCKDOWN', 'EVACUATION')),
    origin TEXT NOT NULL CHECK(origin IN ('ESP32', 'APP', 'EDGE', 'DRILL')),
    causal_chain_id TEXT,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at DATETIME,
    target_room_count INTEGER,
    status TEXT NOT NULL DEFAULT 'PENDING' CHECK(status IN ('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED')),
    error_message TEXT,
    UNIQUE(alert_id)
);

CREATE INDEX IF NOT EXISTS idx_alerts_tenant_building ON alerts(tenant_id, building_id);
CREATE INDEX IF NOT EXISTS idx_alerts_created_at ON alerts(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_alerts_status ON alerts(status);

-- Building Topology
CREATE TABLE IF NOT EXISTS buildings (
    building_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    building_name TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, building_name)
);

CREATE TABLE IF NOT EXISTS rooms (
    room_id TEXT PRIMARY KEY,
    building_id TEXT NOT NULL,
    room_name TEXT NOT NULL,
    floor_number INTEGER,
    capacity INTEGER,
    has_pa BOOLEAN NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (building_id) REFERENCES buildings(building_id) ON DELETE CASCADE,
    UNIQUE(building_id, room_name)
);

CREATE INDEX IF NOT EXISTS idx_rooms_building ON rooms(building_id);

-- Device Registry
CREATE TABLE IF NOT EXISTS devices (
    device_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    device_type TEXT NOT NULL CHECK(device_type IN ('ESP32', 'APP', 'OTHER')),
    device_name TEXT,
    assigned_room_id TEXT,
    firmware_version TEXT,
    battery_level INTEGER CHECK(battery_level >= 0 AND battery_level <= 100),
    rssi INTEGER,
    last_seen_at DATETIME,
    registered_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK(status IN ('ACTIVE', 'OFFLINE', 'MAINTENANCE', 'DECOMMISSIONED')),
    FOREIGN KEY (assigned_room_id) REFERENCES rooms(room_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_devices_tenant ON devices(tenant_id);
CREATE INDEX IF NOT EXISTS idx_devices_status ON devices(status);
CREATE INDEX IF NOT EXISTS idx_devices_last_seen ON devices(last_seen_at DESC);

-- PA Playback Confirmations
CREATE TABLE IF NOT EXISTS pa_confirmations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    alert_id TEXT NOT NULL,
    room_id TEXT NOT NULL,
    playback_started_at DATETIME,
    playback_completed_at DATETIME,
    success BOOLEAN NOT NULL,
    error_message TEXT,
    latency_ms INTEGER,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (alert_id) REFERENCES alerts(alert_id) ON DELETE CASCADE,
    FOREIGN KEY (room_id) REFERENCES rooms(room_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_pa_confirmations_alert ON pa_confirmations(alert_id);
CREATE INDEX IF NOT EXISTS idx_pa_confirmations_room ON pa_confirmations(room_id);
CREATE INDEX IF NOT EXISTS idx_pa_confirmations_success ON pa_confirmations(success);

-- Drill History
CREATE TABLE IF NOT EXISTS drills (
    drill_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    building_id TEXT NOT NULL,
    initiated_by TEXT,
    scheduled_at DATETIME,
    started_at DATETIME NOT NULL,
    completed_at DATETIME,
    target_room_count INTEGER,
    successful_room_count INTEGER,
    failed_room_count INTEGER,
    p50_latency_ms INTEGER,
    p95_latency_ms INTEGER,
    p99_latency_ms INTEGER,
    status TEXT NOT NULL CHECK(status IN ('SCHEDULED', 'RUNNING', 'COMPLETED', 'FAILED', 'CANCELLED')),
    notes TEXT,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_drills_tenant_building ON drills(tenant_id, building_id);
CREATE INDEX IF NOT EXISTS idx_drills_started_at ON drills(started_at DESC);

-- System Health Metrics (snapshot every minute)
CREATE TABLE IF NOT EXISTS health_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    service_name TEXT NOT NULL,
    status TEXT NOT NULL CHECK(status IN ('HEALTHY', 'DEGRADED', 'DOWN')),
    cpu_percent REAL,
    memory_mb INTEGER,
    response_time_ms INTEGER,
    error_count INTEGER DEFAULT 0,
    metadata TEXT -- JSON for additional metrics
);

CREATE INDEX IF NOT EXISTS idx_health_timestamp ON health_snapshots(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_health_service ON health_snapshots(service_name, timestamp DESC);

-- Insert default topology data (tenant-a with two buildings)
INSERT OR IGNORE INTO buildings (building_id, tenant_id, building_name) VALUES
    ('building-a', 'tenant-a', 'Main Building A'),
    ('building-b', 'tenant-a', 'Annex Building B');

INSERT OR IGNORE INTO rooms (room_id, building_id, room_name, floor_number, capacity, has_pa) VALUES
    -- Building A
    ('room-1', 'building-a', 'Room 1', 1, 30, 1),
    ('room-2', 'building-a', 'Room 2', 1, 25, 1),
    ('room-3', 'building-a', 'Room 3', 2, 28, 1),
    ('room-4', 'building-a', 'Room 4', 2, 32, 1),
    -- Building B
    ('room-101', 'building-b', 'Room 101', 1, 20, 1),
    ('room-102', 'building-b', 'Room 102', 1, 22, 1),
    ('room-103', 'building-b', 'Room 103', 2, 18, 1);

-- Insert sample test devices
INSERT OR IGNORE INTO devices (device_id, tenant_id, device_type, device_name, assigned_room_id, status) VALUES
    ('esp32-test', 'tenant-a', 'ESP32', 'Test ESP32 Button', 'room-1', 'ACTIVE'),
    ('app-test', 'tenant-a', 'APP', 'Test Mobile App', 'room-2', 'ACTIVE');
