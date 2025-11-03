import axios, { AxiosInstance, AxiosError } from 'axios';
import { Platform } from 'react-native';
import { API_CONFIG, ERROR_MESSAGES } from '../constants';
import { database } from '../database';
import { secureStorage } from './secureStorage';
import type {
  ApiResponse,
  AuthTokens,
  User,
  Building,
  Alert,
  AlertMode,
  AlertStatus,
  Device,
  PendingAction,
} from '../types';

class ApiClient {
  private client: AxiosInstance;
  private isRefreshing = false;
  private failedQueue: Array<{
    resolve: (value: any) => void;
    reject: (reason: any) => void;
  }> = [];

  constructor() {
    console.log('ApiClient initialized with BASE_URL:', API_CONFIG.BASE_URL);
    this.client = axios.create({
      baseURL: API_CONFIG.BASE_URL,
      timeout: API_CONFIG.TIMEOUT,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.setupInterceptors();
  }

  private setupInterceptors(): void {
    // Request interceptor - Add auth token
    this.client.interceptors.request.use(
      async (config) => {
        const tokens = await secureStorage.getTokens();
        if (tokens?.accessToken) {
          config.headers.Authorization = `Bearer ${tokens.accessToken}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor - Handle token refresh
    this.client.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        const originalRequest = error.config as any;

        if (error.response?.status === 401 && !originalRequest._retry) {
          if (this.isRefreshing) {
            return new Promise((resolve, reject) => {
              this.failedQueue.push({ resolve, reject });
            })
              .then((token) => {
                originalRequest.headers.Authorization = `Bearer ${token}`;
                return this.client(originalRequest);
              })
              .catch((err) => Promise.reject(err));
          }

          originalRequest._retry = true;
          this.isRefreshing = true;

          try {
            const tokens = await secureStorage.getTokens();
            if (!tokens?.refreshToken) {
              throw new Error('No refresh token available');
            }

            const newTokens = await this.refreshToken(tokens.refreshToken);
            await secureStorage.saveTokens(newTokens);

            this.failedQueue.forEach((promise) => {
              promise.resolve(newTokens.accessToken);
            });
            this.failedQueue = [];

            originalRequest.headers.Authorization = `Bearer ${newTokens.accessToken}`;
            return this.client(originalRequest);
          } catch (refreshError) {
            this.failedQueue.forEach((promise) => {
              promise.reject(refreshError);
            });
            this.failedQueue = [];

            await secureStorage.clearTokens();
            throw refreshError;
          } finally {
            this.isRefreshing = false;
          }
        }

        return Promise.reject(error);
      }
    );
  }

  // Authentication
  async login(email: string, password: string): Promise<ApiResponse<{ user: User; tokens: AuthTokens }>> {
    try {
      const response = await this.client.post<{ user: User; tokens: AuthTokens }>(
        '/api/auth/login',
        { email, password }
      );

      await secureStorage.saveTokens(response.data.tokens);
      await secureStorage.saveUser(response.data.user);

      return { success: true, data: response.data };
    } catch (error) {
      return this.handleError(error);
    }
  }

  async refreshToken(refreshToken: string): Promise<AuthTokens> {
    // Get current access token for the refresh request
    const tokens = await secureStorage.getTokens();
    const accessToken = tokens?.accessToken || '';

    const response = await this.client.post<AuthTokens>('/api/auth/refresh', {
      accessToken,
      refreshToken,
    });
    return response.data;
  }

  async logout(): Promise<ApiResponse> {
    try {
      // Try to logout on server (best effort - revoke refresh token)
      await this.client.post('/api/auth/logout', {
        refreshToken: (await secureStorage.getTokens())?.refreshToken || '',
      });
    } catch (error) {
      // Log the error but don't fail the logout
      console.warn('Server logout failed (non-critical):', error);
    }

    // ALWAYS clear local state regardless of server response
    try {
      await secureStorage.clearTokens();
      await database.clearAllData();
      return { success: true };
    } catch (error) {
      console.error('Failed to clear local data:', error);
      return this.handleError(error);
    }
  }

  // Buildings & Topology
  async getBuildings(): Promise<ApiResponse<Building[]>> {
    try {
      const response = await this.client.get<Building[]>('/api/buildings');
      await database.saveBuildings(response.data);
      return { success: true, data: response.data };
    } catch (error) {
      // Return cached data if offline
      if (this.isNetworkError(error)) {
        const cachedBuildings = await database.getBuildings();
        return {
          success: false,
          data: cachedBuildings,
          error: ERROR_MESSAGES.NETWORK_ERROR,
        };
      }
      return this.handleError(error);
    }
  }

  async getBuildingById(buildingId: string): Promise<ApiResponse<Building>> {
    try {
      const response = await this.client.get<Building>(`/api/buildings/${buildingId}`);
      await database.saveBuildings([response.data]);
      return { success: true, data: response.data };
    } catch (error) {
      if (this.isNetworkError(error)) {
        const cachedBuilding = await database.getBuildingById(buildingId);
        if (cachedBuilding) {
          return {
            success: false,
            data: cachedBuilding,
            error: ERROR_MESSAGES.NETWORK_ERROR,
          };
        }
      }
      return this.handleError(error);
    }
  }

  // Alerts
  async triggerAlert(
    buildingId: string,
    sourceRoomId: string,
    mode: AlertMode
  ): Promise<ApiResponse<Alert>> {
    const user = await secureStorage.getUser();
    if (!user) {
      return { success: false, error: ERROR_MESSAGES.AUTH_FAILED };
    }

    const alert: Alert = {
      id: this.generateId(),
      tenantId: user.tenantId,
      buildingId,
      sourceRoomId,
      mode,
      status: 'PENDING',
      triggeredBy: user.id,
      triggeredAt: new Date(),
      synced: false,
    };

    try {
      const response = await this.client.post<any>('/api/alerts/trigger', {
        buildingId,
        roomId: sourceRoomId,
        mode,
      });

      // Map backend response to mobile Alert type
      const backendAlert = response.data;
      alert.id = backendAlert.id;
      alert.status = this.mapBackendStatus(backendAlert.status);
      alert.triggeredAt = new Date(backendAlert.triggeredAt);
      alert.synced = true;

      await database.saveAlert(alert);
      return { success: true, data: alert };
    } catch (error) {
      if (this.isNetworkError(error)) {
        // Queue for later sync - store the local alertId so we can mark it as synced later
        await database.saveAlert(alert);
        await this.addToPendingQueue({
          id: this.generateId(),
          type: 'TRIGGER_ALERT',
          payload: {
            buildingId,
            roomId: sourceRoomId,
            mode,
            metadata: alert.metadata,
            localAlertId: alert.id  // Store local alert ID for marking as synced
          },
          createdAt: new Date(),
          retryCount: 0,
        });

        return {
          success: false,
          data: alert,
          error: ERROR_MESSAGES.NETWORK_ERROR,
        };
      }
      return this.handleError(error);
    }
  }

  async resolveAlert(alertId: string): Promise<ApiResponse<Alert>> {
    try {
      const response = await this.client.put<any>(`/api/alerts/${alertId}/resolve`);

      // Map backend response to mobile Alert type
      const backendAlert = response.data;
      const alert: Alert = {
        id: backendAlert.id,
        tenantId: backendAlert.organizationId,
        buildingId: backendAlert.buildingId || '',
        sourceRoomId: backendAlert.roomId || '',
        mode: this.mapAlertTypeToMode(backendAlert.alertType),
        status: this.mapBackendStatus(backendAlert.status),
        triggeredBy: backendAlert.deviceId || 'UNKNOWN',
        triggeredAt: new Date(backendAlert.triggeredAt),
        clearedAt: backendAlert.resolvedAt ? new Date(backendAlert.resolvedAt) : undefined,
        metadata: backendAlert.metadata,
        synced: true,
      };

      await database.saveAlert(alert);
      return { success: true, data: alert };
    } catch (error) {
      if (this.isNetworkError(error)) {
        // Queue for later sync
        await this.addToPendingQueue({
          id: this.generateId(),
          type: 'RESOLVE_ALERT',
          payload: { alertId },
          createdAt: new Date(),
          retryCount: 0,
        });

        return {
          success: false,
          error: ERROR_MESSAGES.NETWORK_ERROR,
        };
      }
      return this.handleError(error);
    }
  }

  async getAlerts(organizationId: string, pageSize: number = 20, page: number = 1): Promise<ApiResponse<Alert[]>> {
    try {
      const response = await this.client.get<any[]>('/api/alerts', {
        params: { organizationId, pageSize, page },
      });

      // Map backend AlertResponse to mobile Alert type
      const alerts: Alert[] = (response.data || []).map((backendAlert: any) => ({
        id: backendAlert.id,
        tenantId: backendAlert.organizationId,
        buildingId: backendAlert.buildingId || '',
        sourceRoomId: backendAlert.roomId || '',
        mode: this.mapAlertTypeToMode(backendAlert.alertType),
        status: this.mapBackendStatus(backendAlert.status),
        triggeredBy: backendAlert.deviceId || 'UNKNOWN',
        triggeredAt: new Date(backendAlert.triggeredAt),
        clearedAt: backendAlert.resolvedAt ? new Date(backendAlert.resolvedAt) : undefined,
        metadata: backendAlert.metadata,
        synced: true,
      }));

      for (const alert of alerts) {
        await database.saveAlert(alert);
      }

      return { success: true, data: alerts };
    } catch (error) {
      console.error('Load alerts error:', error);
      if (this.isNetworkError(error)) {
        const offset = (page - 1) * pageSize;
        const cachedAlerts = await database.getAlerts(pageSize, offset);
        return {
          success: false,
          data: cachedAlerts,
          error: ERROR_MESSAGES.NETWORK_ERROR,
        };
      }
      return this.handleError(error);
    }
  }

  private mapAlertTypeToMode(alertType: string): AlertMode {
    // Map backend alert types to mobile alert modes
    switch (alertType?.toLowerCase()) {
      case 'silent':
        return 'SILENT';
      case 'lockdown':
        return 'LOCKDOWN';
      case 'evacuation':
        return 'EVACUATION';
      default:
        return 'AUDIBLE';
    }
  }

  private mapBackendStatus(status: number): AlertStatus {
    // Map backend numeric status to mobile string status
    // Backend: 0=New, 1=Acknowledged, 2=Resolved, 3=Cancelled
    switch (status) {
      case 0:
        return 'TRIGGERED';
      case 1:
        return 'PENDING';
      case 2:
        return 'COMPLETED';
      case 3:
        return 'FAILED'; // Cancelled
      default:
        return 'TRIGGERED';
    }
  }

  async acknowledgeAlert(alertId: string): Promise<ApiResponse> {
    try {
      await this.client.put(`/api/alerts/${alertId}/acknowledge`);
      return { success: true };
    } catch (error) {
      if (this.isNetworkError(error)) {
        await this.addToPendingQueue({
          id: this.generateId(),
          type: 'ACKNOWLEDGE_ALERT',
          payload: { alertId },
          createdAt: new Date(),
          retryCount: 0,
        });
        return { success: false, error: ERROR_MESSAGES.NETWORK_ERROR };
      }
      return this.handleError(error);
    }
  }

  // Device Management
  async registerDevice(device: Partial<Device>): Promise<ApiResponse<Device>> {
    try {
      const response = await this.client.post<Device>('/api/devices/register', device);
      await database.saveDevice(response.data);
      return { success: true, data: response.data };
    } catch (error) {
      return this.handleError(error);
    }
  }

  async updateDevicePushToken(deviceId: string, pushToken: string): Promise<ApiResponse> {
    try {
      await this.client.post('/api/users/me/push-token', {
        token: pushToken,
        platform: Platform.OS  // "ios" or "android"
      });
      return { success: true };
    } catch (error) {
      if (this.isNetworkError(error)) {
        await this.addToPendingQueue({
          id: this.generateId(),
          type: 'UPDATE_DEVICE',
          payload: { deviceId, pushToken, platform: Platform.OS },
          createdAt: new Date(),
          retryCount: 0,
        });
        return { success: false, error: ERROR_MESSAGES.NETWORK_ERROR };
      }
      return this.handleError(error);
    }
  }

  // User Profile
  async getProfile(): Promise<ApiResponse<User>> {
    try {
      const response = await this.client.get<User>('/api/users/me');
      await secureStorage.saveUser(response.data);
      return { success: true, data: response.data };
    } catch (error) {
      if (this.isNetworkError(error)) {
        const cachedUser = await secureStorage.getUser();
        if (cachedUser) {
          return {
            success: false,
            data: cachedUser,
            error: ERROR_MESSAGES.NETWORK_ERROR,
          };
        }
      }
      return this.handleError(error);
    }
  }

  async updateProfile(updates: Partial<User>): Promise<ApiResponse<User>> {
    try {
      const response = await this.client.put<User>('/api/users/me', updates);
      await secureStorage.saveUser(response.data);
      return { success: true, data: response.data };
    } catch (error) {
      return this.handleError(error);
    }
  }

  // Sync Operations
  async syncPendingActions(): Promise<void> {
    const pendingActions = await database.getPendingActions();

    for (const action of pendingActions) {
      try {
        await this.executePendingAction(action);

        // Mark alert as synced if this is an alert-related action
        if (action.type === 'TRIGGER_ALERT') {
          // Use the stored localAlertId to mark the alert as synced
          const localAlertId = action.payload.localAlertId;
          if (localAlertId) {
            await database.markAlertSynced(localAlertId);
          }
        } else if (['ACKNOWLEDGE_ALERT', 'RESOLVE_ALERT'].includes(action.type)) {
          const alertId = action.payload.alertId;
          if (alertId) {
            await database.markAlertSynced(alertId);
          }
        }

        await database.deletePendingAction(action.id);
      } catch (error) {
        const newRetryCount = action.retryCount + 1;
        if (newRetryCount >= API_CONFIG.RETRY_ATTEMPTS) {
          console.error(`Max retries reached for action ${action.id}`, error);
          await database.deletePendingAction(action.id);
        } else {
          await database.updatePendingAction(
            action.id,
            newRetryCount,
            this.getErrorMessage(error)
          );
        }
      }
    }

    // Note: Unsynced alerts are already handled by the pending queue above
    // which properly handles TRIGGER_ALERT, ACKNOWLEDGE_ALERT, and RESOLVE_ALERT actions
  }

  private async executePendingAction(action: PendingAction): Promise<void> {
    switch (action.type) {
      case 'TRIGGER_ALERT':
        await this.client.post('/api/alerts/trigger', {
          buildingId: action.payload.buildingId,
          roomId: action.payload.roomId,
          mode: action.payload.mode,
          metadata: action.payload.metadata,
        });
        break;

      case 'ACKNOWLEDGE_ALERT':
        await this.client.put(`/api/alerts/${action.payload.alertId}/acknowledge`);
        break;

      case 'RESOLVE_ALERT':
        await this.client.put(`/api/alerts/${action.payload.alertId}/resolve`);
        break;

      case 'UPDATE_DEVICE':
        await this.client.post('/api/users/me/push-token', {
          token: action.payload.pushToken,
          platform: action.payload.platform || Platform.OS
        });
        break;

      default:
        throw new Error(`Unknown action type: ${action.type}`);
    }
  }

  private async addToPendingQueue(action: PendingAction): Promise<void> {
    await database.addPendingAction(action);
  }

  // Utility Methods
  private generateId(): string {
    return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  private isNetworkError(error: any): boolean {
    return (
      !error.response ||
      error.code === 'ECONNABORTED' ||
      error.code === 'ERR_NETWORK' ||
      error.message === 'Network Error'
    );
  }

  private getErrorMessage(error: any): string {
    if (this.isNetworkError(error)) {
      return ERROR_MESSAGES.NETWORK_ERROR;
    }

    if (axios.isAxiosError(error)) {
      return error.response?.data?.message || error.message;
    }

    return error.message || 'An unknown error occurred';
  }

  private handleError(error: any): ApiResponse {
    const message = this.getErrorMessage(error);

    // Log only safe, non-sensitive information
    const safeLogData = {
      message,
      status: error.response?.status,
      method: error.config?.method,
      // Only log URL path without query parameters that might contain sensitive data
      urlPath: error.config?.url?.split('?')[0],
      timestamp: new Date().toISOString(),
    };

    console.error('API Error:', safeLogData);
    return { success: false, error: message };
  }
}

export const apiClient = new ApiClient();
