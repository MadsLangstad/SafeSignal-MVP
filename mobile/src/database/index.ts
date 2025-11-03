import * as SQLite from 'expo-sqlite';
import { DB_CONFIG } from '../constants';
import type {
  Alert,
  Building,
  Room,
  Device,
  PendingAction,
} from '../types';

class Database {
  private db: SQLite.SQLiteDatabase | null = null;

  async init(): Promise<void> {
    try {
      this.db = await SQLite.openDatabaseAsync(DB_CONFIG.NAME);
      await this.createTables();
      console.log('Database initialized successfully');
    } catch (error) {
      console.error('Failed to initialize database:', error);
      throw error;
    }
  }

  private async createTables(): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    const sql = `
      -- Buildings table
      CREATE TABLE IF NOT EXISTS buildings (
        id TEXT PRIMARY KEY,
        tenant_id TEXT NOT NULL,
        name TEXT NOT NULL,
        address TEXT,
        last_synced_at INTEGER,
        created_at INTEGER DEFAULT (strftime('%s', 'now'))
      );

      -- Rooms table
      CREATE TABLE IF NOT EXISTS rooms (
        id TEXT PRIMARY KEY,
        building_id TEXT NOT NULL,
        name TEXT NOT NULL,
        capacity INTEGER,
        floor TEXT,
        FOREIGN KEY (building_id) REFERENCES buildings(id) ON DELETE CASCADE
      );

      -- Alerts table
      CREATE TABLE IF NOT EXISTS alerts (
        id TEXT PRIMARY KEY,
        tenant_id TEXT NOT NULL,
        building_id TEXT NOT NULL,
        source_room_id TEXT NOT NULL,
        mode TEXT NOT NULL,
        status TEXT NOT NULL,
        triggered_by TEXT NOT NULL,
        triggered_at INTEGER NOT NULL,
        cleared_at INTEGER,
        causal_chain_id TEXT,
        metadata TEXT,
        synced INTEGER DEFAULT 0,
        created_at INTEGER DEFAULT (strftime('%s', 'now'))
      );

      -- Devices table
      CREATE TABLE IF NOT EXISTS devices (
        id TEXT PRIMARY KEY,
        tenant_id TEXT NOT NULL,
        building_id TEXT,
        room_id TEXT,
        type TEXT NOT NULL,
        push_token TEXT,
        platform TEXT,
        last_seen_at INTEGER,
        battery_level REAL,
        rssi INTEGER,
        updated_at INTEGER DEFAULT (strftime('%s', 'now'))
      );

      -- Pending actions queue
      CREATE TABLE IF NOT EXISTS pending_actions (
        id TEXT PRIMARY KEY,
        type TEXT NOT NULL,
        payload TEXT NOT NULL,
        created_at INTEGER DEFAULT (strftime('%s', 'now')),
        retry_count INTEGER DEFAULT 0,
        last_error TEXT
      );

      -- Create indexes for performance
      CREATE INDEX IF NOT EXISTS idx_alerts_tenant_building ON alerts(tenant_id, building_id);
      CREATE INDEX IF NOT EXISTS idx_alerts_triggered_at ON alerts(triggered_at DESC);
      CREATE INDEX IF NOT EXISTS idx_alerts_synced ON alerts(synced);
      CREATE INDEX IF NOT EXISTS idx_rooms_building ON rooms(building_id);
      CREATE INDEX IF NOT EXISTS idx_pending_actions_created ON pending_actions(created_at);
    `;

    await this.db.execAsync(sql);
  }

  // Buildings
  async saveBuildings(buildings: Building[]): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.withTransactionAsync(async () => {
      const now = Math.floor(Date.now() / 1000);

      for (const building of buildings) {
        await this.db!.runAsync(
          `INSERT OR REPLACE INTO buildings (id, tenant_id, name, address, last_synced_at)
           VALUES (?, ?, ?, ?, ?)`,
          [building.id, building.tenantId, building.name, building.address, now]
        );

        // Save associated rooms
        for (const room of building.rooms) {
          await this.db!.runAsync(
            `INSERT OR REPLACE INTO rooms (id, building_id, name, capacity, floor)
             VALUES (?, ?, ?, ?, ?)`,
            [room.id, room.buildingId, room.name, room.capacity ?? null, room.floor ?? null]
          );
        }
      }
    });
  }

  async getBuildings(): Promise<Building[]> {
    if (!this.db) throw new Error('Database not initialized');

    const buildings = await this.db.getAllAsync<any>(
      'SELECT * FROM buildings ORDER BY name'
    );

    const buildingsWithRooms: Building[] = [];

    for (const building of buildings) {
      const rooms = await this.db.getAllAsync<any>(
        'SELECT * FROM rooms WHERE building_id = ? ORDER BY name',
        [building.id]
      );

      buildingsWithRooms.push({
        id: building.id,
        tenantId: building.tenant_id,
        name: building.name,
        address: building.address,
        lastSyncedAt: building.last_synced_at ? new Date(building.last_synced_at * 1000) : undefined,
        rooms: rooms.map((r) => ({
          id: r.id,
          buildingId: r.building_id,
          name: r.name,
          capacity: r.capacity,
          floor: r.floor,
        })),
      });
    }

    return buildingsWithRooms;
  }

  async getBuildingById(buildingId: string): Promise<Building | null> {
    if (!this.db) throw new Error('Database not initialized');

    const building = await this.db.getFirstAsync<any>(
      'SELECT * FROM buildings WHERE id = ?',
      [buildingId]
    );

    if (!building) return null;

    const rooms = await this.db.getAllAsync<any>(
      'SELECT * FROM rooms WHERE building_id = ? ORDER BY name',
      [building.id]
    );

    return {
      id: building.id,
      tenantId: building.tenant_id,
      name: building.name,
      address: building.address,
      lastSyncedAt: building.last_synced_at ? new Date(building.last_synced_at * 1000) : undefined,
      rooms: rooms.map((r) => ({
        id: r.id,
        buildingId: r.building_id,
        name: r.name,
        capacity: r.capacity,
        floor: r.floor,
      })),
    };
  }

  // Alerts
  async saveAlert(alert: Alert): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      `INSERT OR REPLACE INTO alerts
       (id, tenant_id, building_id, source_room_id, mode, status, triggered_by,
        triggered_at, cleared_at, causal_chain_id, metadata, synced)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      [
        alert.id,
        alert.tenantId,
        alert.buildingId,
        alert.sourceRoomId,
        alert.mode,
        alert.status,
        alert.triggeredBy,
        Math.floor(alert.triggeredAt.getTime() / 1000),
        alert.clearedAt ? Math.floor(alert.clearedAt.getTime() / 1000) : null,
        alert.causalChainId ?? null,
        alert.metadata ? JSON.stringify(alert.metadata) : null,
        alert.synced ? 1 : 0,
      ]
    );
  }

  async getAlerts(limit: number = 20, offset: number = 0): Promise<Alert[]> {
    if (!this.db) throw new Error('Database not initialized');

    const alerts = await this.db.getAllAsync<any>(
      'SELECT * FROM alerts ORDER BY triggered_at DESC LIMIT ? OFFSET ?',
      [limit, offset]
    );

    return alerts.map(this.mapAlertFromDb);
  }

  async getUnsyncedAlerts(): Promise<Alert[]> {
    if (!this.db) throw new Error('Database not initialized');

    const alerts = await this.db.getAllAsync<any>(
      'SELECT * FROM alerts WHERE synced = 0 ORDER BY triggered_at'
    );

    return alerts.map(this.mapAlertFromDb);
  }

  async markAlertSynced(alertId: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      'UPDATE alerts SET synced = 1 WHERE id = ?',
      [alertId]
    );
  }

  private mapAlertFromDb(row: any): Alert {
    return {
      id: row.id,
      tenantId: row.tenant_id,
      buildingId: row.building_id,
      sourceRoomId: row.source_room_id,
      mode: row.mode,
      status: row.status,
      triggeredBy: row.triggered_by,
      triggeredAt: new Date(row.triggered_at * 1000),
      clearedAt: row.cleared_at ? new Date(row.cleared_at * 1000) : undefined,
      causalChainId: row.causal_chain_id,
      metadata: row.metadata ? JSON.parse(row.metadata) : undefined,
      synced: row.synced === 1,
    };
  }

  // Pending Actions Queue
  async addPendingAction(action: PendingAction): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      `INSERT INTO pending_actions (id, type, payload, created_at, retry_count, last_error)
       VALUES (?, ?, ?, ?, ?, ?)`,
      [
        action.id,
        action.type,
        JSON.stringify(action.payload),
        Math.floor(action.createdAt.getTime() / 1000),
        action.retryCount,
        action.lastError ?? null,
      ]
    );
  }

  async getPendingActions(limit: number = 50): Promise<PendingAction[]> {
    if (!this.db) throw new Error('Database not initialized');

    const actions = await this.db.getAllAsync<any>(
      'SELECT * FROM pending_actions ORDER BY created_at LIMIT ?',
      [limit]
    );

    return actions.map((row) => ({
      id: row.id,
      type: row.type,
      payload: JSON.parse(row.payload),
      createdAt: new Date(row.created_at * 1000),
      retryCount: row.retry_count,
      lastError: row.last_error,
    }));
  }

  async updatePendingAction(actionId: string, retryCount: number, error?: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      'UPDATE pending_actions SET retry_count = ?, last_error = ? WHERE id = ?',
      [retryCount, error ?? null, actionId]
    );
  }

  async updatePendingActionPayload(actionId: string, payload: any): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      'UPDATE pending_actions SET payload = ? WHERE id = ?',
      [JSON.stringify(payload), actionId]
    );
  }

  async deleteAlert(alertId: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      'DELETE FROM alerts WHERE id = ?',
      [alertId]
    );
  }

  async deletePendingAction(actionId: string): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.runAsync(
      'DELETE FROM pending_actions WHERE id = ?',
      [actionId]
    );
  }

  async getPendingActionsCount(): Promise<number> {
    if (!this.db) throw new Error('Database not initialized');

    const result = await this.db.getFirstAsync<{ count: number }>(
      'SELECT COUNT(*) as count FROM pending_actions'
    );

    return result?.count ?? 0;
  }

  // Device Management
  async saveDevice(device: Device): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    const now = Math.floor(Date.now() / 1000);

    await this.db.runAsync(
      `INSERT OR REPLACE INTO devices
       (id, tenant_id, building_id, room_id, type, push_token, platform,
        last_seen_at, battery_level, rssi, updated_at)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      [
        device.id,
        device.tenantId,
        device.buildingId ?? null,
        device.roomId ?? null,
        device.type,
        device.pushToken ?? null,
        device.platform ?? null,
        device.lastSeenAt ? Math.floor(device.lastSeenAt.getTime() / 1000) : null,
        device.batteryLevel ?? null,
        device.rssi ?? null,
        now,
      ]
    );
  }

  // Utility
  async clearAllData(): Promise<void> {
    if (!this.db) throw new Error('Database not initialized');

    await this.db.withTransactionAsync(async () => {
      await this.db!.runAsync('DELETE FROM buildings');
      await this.db!.runAsync('DELETE FROM rooms');
      await this.db!.runAsync('DELETE FROM alerts');
      await this.db!.runAsync('DELETE FROM devices');
      await this.db!.runAsync('DELETE FROM pending_actions');
    });
  }

  async close(): Promise<void> {
    if (this.db) {
      await this.db.closeAsync();
      this.db = null;
    }
  }
}

export const database = new Database();
