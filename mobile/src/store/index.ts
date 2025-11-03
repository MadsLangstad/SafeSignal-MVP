import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiClient } from '../services/api';
import { authService } from '../services/auth';
import { database } from '../database';
import type {
  User,
  Building,
  Alert,
  AlertMode,
  SyncStatus,
} from '../types';

export type ThemeMode = 'light' | 'dark' | 'system';

interface AppState {
  // Auth
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;

  // Theme
  theme: ThemeMode;

  // Buildings & Topology
  buildings: Building[];
  selectedBuilding: Building | null;
  selectedRoomId: string | null;

  // Alerts
  alerts: Alert[];
  alertsPage: number;
  hasMoreAlerts: boolean;

  // Sync
  syncStatus: SyncStatus;

  // Actions - Auth
  login: (email: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  loadUser: () => Promise<void>;

  // Actions - Buildings
  loadBuildings: () => Promise<void>;
  selectBuilding: (building: Building | null) => void;
  selectRoom: (roomId: string | null) => void;

  // Actions - Alerts
  triggerAlert: (mode: AlertMode) => Promise<Alert | null>;
  loadAlerts: (refresh?: boolean) => Promise<void>;
  loadMoreAlerts: () => Promise<void>;

  // Actions - Sync
  syncData: () => Promise<void>;
  setSyncStatus: (status: Partial<SyncStatus>) => void;

  // Actions - Theme
  setTheme: (theme: ThemeMode) => void;
}

export const useAppStore = create<AppState>(
  persist(
    (set, get) => ({
      // Initial State
      user: null,
      isAuthenticated: false,
      isLoading: false,
      theme: 'system' as ThemeMode,
      buildings: [],
      selectedBuilding: null,
      selectedRoomId: null,
      alerts: [],
      alertsPage: 1,
      hasMoreAlerts: true,
      syncStatus: {
        isSyncing: false,
        pendingActions: 0,
      },

  // Auth Actions
  login: async (email, password) => {
    set({ isLoading: true });
    try {
      const response = await authService.login(email, password);

      if (response.success && response.data) {
        set({
          user: response.data,
          isAuthenticated: true,
          isLoading: false,
        });

        // Initialize and load data
        await database.init();
        await get().loadBuildings();
        await get().loadAlerts(true);

        return true;
      }

      set({ isLoading: false });
      return false;
    } catch (error) {
      console.error('Login error:', error);
      set({ isLoading: false });
      return false;
    }
  },

  logout: async () => {
    set({ isLoading: true });
    try {
      await authService.logout();
      await database.clearAllData();
    } catch (error) {
      console.error('Logout error (non-critical):', error);
      // Continue with state clear even if logout fails
    }

    // ALWAYS clear authentication state, regardless of API success
    set({
      user: null,
      isAuthenticated: false,
      buildings: [],
      selectedBuilding: null,
      selectedRoomId: null,
      alerts: [],
      alertsPage: 1,
      hasMoreAlerts: true,
      isLoading: false,
    });
  },

  loadUser: async () => {
    try {
      const user = await authService.getCurrentUser();

      if (user) {
        console.log('User session found, loading data...');
        set({ user, isAuthenticated: true });
        await database.init();
        await get().loadBuildings();
        await get().loadAlerts();
      } else {
        console.log('No user session found, showing login screen');
        set({ user: null, isAuthenticated: false });
      }
    } catch (error) {
      console.error('Load user error:', error);
      set({ user: null, isAuthenticated: false });
    }
  },

  // Buildings Actions
  loadBuildings: async () => {
    try {
      const response = await apiClient.getBuildings();

      if (response.data) {
        set({ buildings: response.data });

        // Auto-select user's assigned building if available
        const { user } = get();
        if (user?.assignedBuildingId && response.data.length > 0) {
          const assignedBuilding = response.data.find(
            (b) => b.id === user.assignedBuildingId
          );
          if (assignedBuilding) {
            set({
              selectedBuilding: assignedBuilding,
              selectedRoomId: user.assignedRoomId || null,
            });
          }
        }
      }
    } catch (error) {
      console.error('Load buildings error:', error);
    }
  },

  selectBuilding: (building) => {
    set({ selectedBuilding: building, selectedRoomId: null });
  },

  selectRoom: (roomId) => {
    set({ selectedRoomId: roomId });
  },

  // Alerts Actions
  triggerAlert: async (mode) => {
    const { selectedBuilding, selectedRoomId, user } = get();

    if (!selectedBuilding || !selectedRoomId || !user) {
      console.error('Missing required data for alert trigger');
      return null;
    }

    try {
      const response = await apiClient.triggerAlert(
        selectedBuilding.id,
        selectedRoomId,
        mode
      );

      if (response.success || response.data) {
        // Refresh alerts to show the new one
        await get().loadAlerts(true);
        return response.data || null;
      }

      return null;
    } catch (error) {
      console.error('Trigger alert error:', error);
      return null;
    }
  },

  loadAlerts: async (refresh = false) => {
    const { user } = get();

    if (!user?.tenantId) {
      console.error('Cannot load alerts: missing user or tenantId');
      return;
    }

    if (refresh) {
      set({ alertsPage: 1, hasMoreAlerts: true });
    }

    const page = refresh ? 1 : get().alertsPage;

    try {
      const response = await apiClient.getAlerts(user.tenantId, 20, page);

      if (response.data) {
        set((state) => ({
          alerts: refresh ? response.data : [...state.alerts, ...response.data],
          alertsPage: page,
          hasMoreAlerts: response.data.length === 20,
        }));
      }
    } catch (error) {
      console.error('Load alerts error:', error);
    }
  },

  loadMoreAlerts: async () => {
    const { hasMoreAlerts, isLoading } = get();

    if (!hasMoreAlerts || isLoading) {
      return;
    }

    set((state) => ({ alertsPage: state.alertsPage + 1 }));
    await get().loadAlerts();
  },

  // Sync Actions
  syncData: async () => {
    const { isSyncing } = get().syncStatus;
    const { isAuthenticated } = get();

    // Don't sync if not authenticated
    if (!isAuthenticated) {
      console.log('Skipping sync - not authenticated');
      return;
    }

    if (isSyncing) {
      return;
    }

    set({
      syncStatus: {
        ...get().syncStatus,
        isSyncing: true,
        error: undefined,
      },
    });

    try {
      // Sync pending actions first
      await apiClient.syncPendingActions();

      // Then refresh data from server
      await Promise.all([
        get().loadBuildings(),
        get().loadAlerts(true),
      ]);

      const pendingCount = await database.getPendingActionsCount();

      set({
        syncStatus: {
          lastSyncAt: new Date(),
          isSyncing: false,
          pendingActions: pendingCount,
        },
      });
    } catch (error: any) {
      console.error('Sync error:', error);

      set({
        syncStatus: {
          ...get().syncStatus,
          isSyncing: false,
          error: error.message || 'Sync failed',
        },
      });
    }
  },

  setSyncStatus: (status) => {
    set({
      syncStatus: {
        ...get().syncStatus,
        ...status,
      },
    });
  },

  // Theme Actions
  setTheme: (theme) => {
    set({ theme });
  },
}),
    {
      name: 'safesignal-app-storage',
      storage: createJSONStorage(() => AsyncStorage),
      partialize: (state) => ({
        theme: state.theme,
      }),
    }
  )
);

// Helper hook for sync operations
export const useSyncEffect = () => {
  const { syncData } = useAppStore();

  return {
    syncNow: syncData,
  };
};
