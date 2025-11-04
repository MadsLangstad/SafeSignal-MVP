// Core domain types for SafeSignal Mobile App

export type AlertMode = 'SILENT' | 'AUDIBLE' | 'LOCKDOWN' | 'EVACUATION';

export type AlertStatus =
  | 'New'
  | 'Acknowledged'
  | 'PendingClearance'  // First person cleared, awaiting second verification
  | 'Resolved'
  | 'Cancelled';

export interface User {
  id: string;
  email: string;
  name: string;
  tenantId: string;
  assignedBuildingId?: string;
  assignedRoomId?: string;
  phoneNumber?: string;
  createdAt: Date;
}

export interface Tenant {
  id: string;
  name: string;
  region: string;
}

export interface Building {
  id: string;
  tenantId: string;
  name: string;
  address: string;
  rooms: Room[];
  lastSyncedAt?: Date;
}

export interface Room {
  id: string;
  buildingId: string;
  name: string;
  capacity?: number;
  floor?: string;
}

export interface Alert {
  id: string;
  tenantId: string;
  buildingId: string;
  sourceRoomId: string;
  mode: AlertMode;
  status: AlertStatus;
  triggeredBy: string;
  triggeredAt: Date;
  resolvedAt?: Date;
  causalChainId?: string;
  metadata?: Record<string, any>;
  synced: boolean;
  // Two-person clearance fields
  firstClearanceUserId?: string;
  firstClearanceAt?: Date;
  secondClearanceUserId?: string;
  secondClearanceAt?: Date;
  fullyClearedAt?: Date;
}

export interface Location {
  latitude: number;
  longitude: number;
}

export interface AlertClearance {
  id: string;
  alertId: string;
  userId: string;
  userName: string;
  userEmail: string;
  clearanceStep: number; // 1 or 2
  clearedAt: Date;
  notes?: string;
  location?: Location;
  deviceInfo?: string;
}

export interface ClearAlertRequest {
  notes?: string;
  location?: Location;
}

export interface ClearAlertResponse {
  alertId: string;
  status: string;
  message: string;
  clearanceStep: number;
  clearanceId: string;
  clearedBy: string;
  clearedAt: Date;
  requiresSecondClearance: boolean;
  firstClearance?: {
    userId: string;
    userName: string;
    clearedAt: Date;
  };
  secondClearance?: {
    userId: string;
    userName: string;
    clearedAt: Date;
  };
}

export interface Device {
  id: string;
  tenantId: string;
  buildingId?: string;
  roomId?: string;
  type: 'ESP32' | 'MOBILE';
  pushToken?: string;
  platform?: 'ios' | 'android';
  lastSeenAt?: Date;
  batteryLevel?: number;
  rssi?: number;
}

export interface PendingAction {
  id: string;
  type: 'TRIGGER_ALERT' | 'UPDATE_DEVICE' | 'ACKNOWLEDGE_ALERT' | 'RESOLVE_ALERT';
  payload: any;
  createdAt: Date;
  retryCount: number;
  lastError?: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: Date;
}

export interface NotificationPayload {
  alertId: string;
  tenantId: string;
  buildingId: string;
  sourceRoomId: string;
  mode: AlertMode;
  triggeredAt: string;
  message: string;
}

export interface ApiResponse<T = any> {
  data?: T;
  error?: string;
  success: boolean;
}

export interface SyncStatus {
  lastSyncAt?: Date;
  isSyncing: boolean;
  pendingActions: number;
  error?: string;
}
