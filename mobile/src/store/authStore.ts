import { create } from 'zustand';
import { feideAuth, bankIDAuth, authService } from '../services/auth';
import { secureStorage } from '../services/secureStorage';
import type { User, SSOSession, AuthProvider } from '../types/auth';

interface AuthState {
  // User state
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;

  // SSO session tracking
  ssoSession: SSOSession | null;

  // Biometric state
  biometricEnabled: boolean;
  biometricType: 'faceId' | 'fingerprint' | 'iris' | null;

  // Auth actions
  login: (email: string, password: string) => Promise<boolean>;
  loginWithFeide: () => Promise<boolean>;
  loginWithBankID: (personalNumber?: string) => Promise<boolean>;
  pollBankIDStatus: () => Promise<boolean>;
  loginWithBiometric: () => Promise<boolean>;
  logout: () => Promise<void>;
  loadUser: () => Promise<void>;

  // Biometric actions
  enableBiometric: () => Promise<boolean>;
  disableBiometric: () => Promise<void>;
  checkBiometricAvailability: () => Promise<void>;

  // SSO session management
  clearError: () => void;
  clearSSOSession: () => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  // Initial state
  user: null,
  isAuthenticated: false,
  isLoading: false,
  error: null,
  ssoSession: null,
  biometricEnabled: false,
  biometricType: null,

  // Email/Password login
  login: async (email, password) => {
    set({ isLoading: true, error: null });

    try {
      const response = await authService.login(email, password);

      if (response.success && response.data) {
        set({
          user: response.data,
          isAuthenticated: true,
          isLoading: false,
          error: null,
        });

        // Wait for tokens to be available
        let retries = 0;
        const maxRetries = 10;
        while (retries < maxRetries) {
          const tokens = await secureStorage.getTokens();
          if (tokens?.accessToken) break;
          retries++;
          if (retries < maxRetries) {
            await new Promise((resolve) => setTimeout(resolve, 50));
          }
        }

        return true;
      }

      set({
        isLoading: false,
        error: response.error || 'Login failed',
      });
      return false;
    } catch (error: any) {
      set({
        isLoading: false,
        error: error.message || 'Login failed',
      });
      return false;
    }
  },

  // Feide SSO login
  loginWithFeide: async () => {
    set({ isLoading: true, error: null });

    try {
      // Initiate Feide OAuth flow
      const session = await feideAuth.initiateAuth();

      if (session.status === 'failed') {
        set({
          isLoading: false,
          error: session.error || 'Feide authentication failed',
        });
        return false;
      }

      if (session.status === 'pending' && session.sessionId) {
        // Exchange code for token via backend
        const result = await feideAuth.exchangeCodeForToken(
          session.sessionId,
          session.feide?.codeVerifier || ''
        );

        if (result.success && result.user) {
          set({
            user: result.user,
            isAuthenticated: true,
            isLoading: false,
            error: null,
            ssoSession: { ...session, status: 'completed' },
          });

          // Wait for tokens
          let retries = 0;
          while (retries < 10) {
            const tokens = await secureStorage.getTokens();
            if (tokens?.accessToken) break;
            retries++;
            await new Promise((resolve) => setTimeout(resolve, 50));
          }

          return true;
        }

        set({
          isLoading: false,
          error: result.error || 'Feide authentication failed',
          ssoSession: { ...session, status: 'failed' },
        });
        return false;
      }

      set({ isLoading: false });
      return false;
    } catch (error: any) {
      set({
        isLoading: false,
        error: error.message || 'Feide authentication failed',
      });
      return false;
    }
  },

  // BankID SSO login
  loginWithBankID: async (personalNumber?: string) => {
    set({ isLoading: true, error: null });

    try {
      // Initiate BankID session
      const session = await bankIDAuth.initiateAuth(personalNumber);

      if (session.status === 'failed') {
        set({
          isLoading: false,
          error: session.error || 'BankID authentication failed',
        });
        return false;
      }

      // Store session for polling
      set({
        ssoSession: session,
      });

      // Start polling (UI will call pollBankIDStatus)
      return true;
    } catch (error: any) {
      set({
        isLoading: false,
        error: error.message || 'BankID authentication failed',
      });
      return false;
    }
  },

  // Poll BankID status
  pollBankIDStatus: async () => {
    const { ssoSession } = get();

    if (!ssoSession || ssoSession.provider !== 'bankid') {
      return false;
    }

    try {
      const updatedSession = await bankIDAuth.pollStatus(
        ssoSession.sessionId,
        (session) => {
          set({ ssoSession: session });
        }
      );

      if (updatedSession.status === 'completed') {
        // Complete authentication
        const result = await bankIDAuth.completeAuth(ssoSession.sessionId);

        if (result.success && result.user) {
          set({
            user: result.user,
            isAuthenticated: true,
            isLoading: false,
            error: null,
            ssoSession: updatedSession,
          });

          // Wait for tokens
          let retries = 0;
          while (retries < 10) {
            const tokens = await secureStorage.getTokens();
            if (tokens?.accessToken) break;
            retries++;
            await new Promise((resolve) => setTimeout(resolve, 50));
          }

          return true;
        }

        set({
          isLoading: false,
          error: result.error || 'BankID authentication failed',
          ssoSession: { ...updatedSession, status: 'failed' },
        });
        return false;
      } else if (updatedSession.status === 'failed') {
        set({
          isLoading: false,
          error: updatedSession.error || 'BankID authentication failed',
          ssoSession: updatedSession,
        });
        return false;
      }

      // Still pending
      set({ ssoSession: updatedSession });
      return false;
    } catch (error: any) {
      set({
        isLoading: false,
        error: error.message || 'BankID polling failed',
      });
      return false;
    }
  },

  // Biometric login
  loginWithBiometric: async () => {
    set({ isLoading: true, error: null });

    try {
      const response = await authService.authenticateWithBiometric();

      if (response.success) {
        // Biometric success - load saved user
        const user = await authService.getCurrentUser();

        if (user) {
          set({
            user,
            isAuthenticated: true,
            isLoading: false,
            error: null,
          });
          return true;
        }
      }

      set({
        isLoading: false,
        error: response.error || 'Biometric authentication failed',
      });
      return false;
    } catch (error: any) {
      set({
        isLoading: false,
        error: error.message || 'Biometric authentication failed',
      });
      return false;
    }
  },

  // Logout
  logout: async () => {
    set({ isLoading: true });

    try {
      // Cancel any active SSO session
      const { ssoSession } = get();
      if (ssoSession?.provider === 'bankid' && ssoSession.sessionId) {
        await bankIDAuth.cancelAuth(ssoSession.sessionId);
      }

      await authService.logout();
    } catch (error) {
      console.error('Logout error (non-critical):', error);
    }

    // Always clear state
    set({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      error: null,
      ssoSession: null,
    });
  },

  // Load persisted user
  loadUser: async () => {
    try {
      const user = await authService.getCurrentUser();

      if (user) {
        set({ user, isAuthenticated: true });
      } else {
        set({ user: null, isAuthenticated: false });
      }
    } catch (error) {
      console.error('Load user error:', error);
      set({ user: null, isAuthenticated: false });
    }
  },

  // Biometric management
  enableBiometric: async () => {
    const response = await authService.enableBiometric();

    if (response.success) {
      set({ biometricEnabled: true });
      return true;
    }

    set({ error: response.error || 'Failed to enable biometric' });
    return false;
  },

  disableBiometric: async () => {
    await authService.disableBiometric();
    set({ biometricEnabled: false });
  },

  checkBiometricAvailability: async () => {
    const { available, type } = await authService.checkBiometricAvailability();
    const enabled = await authService.isBiometricEnabled();

    const typeMap: Record<number, 'faceId' | 'fingerprint' | 'iris'> = {
      1: 'fingerprint',
      2: 'faceId',
      3: 'iris',
    };

    set({
      biometricEnabled: enabled,
      biometricType: type ? typeMap[type] || null : null,
    });
  },

  // Utility actions
  clearError: () => set({ error: null }),

  clearSSOSession: () => set({ ssoSession: null }),
}));
