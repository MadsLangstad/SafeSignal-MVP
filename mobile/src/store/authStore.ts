import { create } from 'zustand';
import { feideAuth, bankIDAuth } from '../services/auth';
import { authService } from '../services/auth';
import { secureStorage } from '../services/secureStorage';
import type { User, SSOSession, AuthProvider } from '../types/auth';

interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  ssoSession: SSOSession | null;
  biometricEnabled: boolean;
  biometricType: 'faceId' | 'fingerprint' | 'iris' | null;

  login: (email: string, password: string) => Promise<boolean>;
  loginWithFeide: () => Promise<boolean>;
  loginWithBankID: (personalNumber?: string) => Promise<boolean>;
  pollBankIDStatus: () => Promise<boolean>;
  loginWithBiometric: () => Promise<boolean>;
  logout: () => Promise<void>;
  loadUser: () => Promise<void>;
  enableBiometric: () => Promise<boolean>;
  disableBiometric: () => Promise<void>;
  checkBiometricAvailability: () => Promise<void>;
  clearError: () => void;
  clearSSOSession: () => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  isAuthenticated: false,
  isLoading: false,
  error: null,
  ssoSession: null,
  biometricEnabled: false,
  biometricType: null,

  login: async (email, password) => {
    set({ isLoading: true, error: null });
    try {
      const response = await authService.login(email, password);
      if (response.success && response.data) {
        set({
          user: response.data,
          isAuthenticated: true,
          isLoading: false,
        });
        return true;
      }
      set({ isLoading: false, error: response.error || 'Login failed' });
      return false;
    } catch (error: any) {
      set({ isLoading: false, error: error.message || 'Login failed' });
      return false;
    }
  },

  loginWithFeide: async () => {
    set({ isLoading: true, error: null });
    try {
      const session = await feideAuth.initiateAuth();
      if (session.status === 'failed') {
        set({ isLoading: false, error: session.error || 'Feide authentication failed' });
        return false;
      }

      if (session.status === 'pending' && session.sessionId) {
        // Store session and reset loading - token exchange will happen in backend callback
        set({ ssoSession: session, isLoading: false });

        const result = await feideAuth.exchangeCodeForToken(
          session.sessionId,
          session.feide?.codeVerifier || ''
        );

        if (result.success && result.user && result.tokens) {
          // Persist tokens to secure storage
          await secureStorage.saveTokens({
            accessToken: result.tokens.accessToken,
            refreshToken: result.tokens.refreshToken,
            expiresAt: new Date(result.tokens.expiresAt),
          });
          await secureStorage.saveUser(result.user);

          set({
            user: result.user,
            isAuthenticated: true,
            ssoSession: { ...session, status: 'completed' },
          });
          return true;
        }

        set({
          error: result.error || 'Feide authentication failed',
          ssoSession: null,
        });
        return false;
      }

      set({ isLoading: false });
      return false;
    } catch (error: any) {
      set({ isLoading: false, error: error.message || 'Feide authentication failed' });
      return false;
    }
  },

  loginWithBankID: async (personalNumber?: string) => {
    set({ isLoading: true, error: null });
    try {
      const session = await bankIDAuth.initiateAuth(personalNumber);
      if (session.status === 'failed') {
        set({ isLoading: false, error: session.error || 'BankID authentication failed' });
        return false;
      }

      set({ ssoSession: session, isLoading: false });
      return true;
    } catch (error: any) {
      set({ isLoading: false, error: error.message || 'BankID authentication failed' });
      return false;
    }
  },

  pollBankIDStatus: async () => {
    const { ssoSession } = get();
    if (!ssoSession || ssoSession.provider !== 'bankid') return false;

    try {
      const updatedSession = await bankIDAuth.pollStatus(
        ssoSession.sessionId,
        (session: SSOSession) => set({ ssoSession: session }),
        ssoSession.bankid?.qrCodeData,
        ssoSession.bankid?.autoStartToken
      );

      if (updatedSession.status === 'completed') {
        const result = await bankIDAuth.completeAuth(ssoSession.sessionId);
        if (result.success && result.user && result.tokens) {
          // Persist tokens to secure storage
          await secureStorage.saveTokens({
            accessToken: result.tokens.accessToken,
            refreshToken: result.tokens.refreshToken,
            expiresAt: new Date(result.tokens.expiresAt),
          });
          await secureStorage.saveUser(result.user);

          set({
            user: result.user,
            isAuthenticated: true,
            isLoading: false,
            ssoSession: updatedSession,
          });
          return true;
        }

        set({
          isLoading: false,
          error: result.error || 'BankID authentication failed',
        });
        return false;
      } else if (updatedSession.status === 'failed') {
        set({
          isLoading: false,
          error: updatedSession.error || 'BankID authentication failed',
        });
        return false;
      }

      set({ ssoSession: updatedSession });
      return false;
    } catch (error: any) {
      set({ isLoading: false, error: error.message || 'BankID polling failed' });
      return false;
    }
  },

  loginWithBiometric: async () => {
    set({ isLoading: true, error: null });
    try {
      const response = await authService.authenticateWithBiometric();
      if (response.success) {
        const user = await authService.getCurrentUser();
        if (user) {
          set({ user, isAuthenticated: true, isLoading: false });
          return true;
        }
      }

      set({ isLoading: false, error: response.error || 'Biometric authentication failed' });
      return false;
    } catch (error: any) {
      set({ isLoading: false, error: error.message || 'Biometric authentication failed' });
      return false;
    }
  },

  logout: async () => {
    set({ isLoading: true });
    try {
      const { ssoSession } = get();
      if (ssoSession?.provider === 'bankid' && ssoSession.sessionId) {
        await bankIDAuth.cancelAuth(ssoSession.sessionId);
      }
      await authService.logout();
    } catch (error) {
      console.error('Logout error:', error);
    }

    set({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      error: null,
      ssoSession: null,
    });
  },

  loadUser: async () => {
    try {
      const user = await authService.getCurrentUser();
      set({ user: user || null, isAuthenticated: !!user });
    } catch (error) {
      set({ user: null, isAuthenticated: false });
    }
  },

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

  clearError: () => set({ error: null }),
  clearSSOSession: () => set({ ssoSession: null }),
}));
