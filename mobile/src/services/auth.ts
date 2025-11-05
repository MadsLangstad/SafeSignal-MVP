import * as LocalAuthentication from 'expo-local-authentication';
import i18n from '../i18n';
import { apiClient } from './api';
import { secureStorage } from './secureStorage';
import { ERROR_MESSAGES } from '../constants';
import type { User, ApiResponse } from '../types';

class AuthService {
  async login(email: string, password: string): Promise<ApiResponse<User>> {
    const response = await apiClient.login(email, password);

    if (response.success && response.data) {
      return { success: true, data: response.data.user };
    }

    return { success: false, error: response.error };
  }

  async logout(): Promise<ApiResponse> {
    const response = await apiClient.logout();
    await secureStorage.clearAll();
    return response;
  }

  async getCurrentUser(): Promise<User | null> {
    return await secureStorage.getUser();
  }

  async checkBiometricAvailability(): Promise<{
    available: boolean;
    type: LocalAuthentication.AuthenticationType | null;
  }> {
    const compatible = await LocalAuthentication.hasHardwareAsync();
    if (!compatible) {
      return { available: false, type: null };
    }

    const enrolled = await LocalAuthentication.isEnrolledAsync();
    if (!enrolled) {
      return { available: false, type: null };
    }

    const types = await LocalAuthentication.supportedAuthenticationTypesAsync();
    const type = types[0] ?? null;

    return { available: true, type };
  }

  async authenticateWithBiometric(): Promise<ApiResponse<boolean>> {
    const { available } = await this.checkBiometricAvailability();

    if (!available) {
      return {
        success: false,
        error: ERROR_MESSAGES.BIOMETRIC_UNAVAILABLE,
      };
    }

    const isBiometricEnabled = await secureStorage.isBiometricEnabled();
    if (!isBiometricEnabled) {
      return {
        success: false,
        error: 'Biometric authentication is not enabled. Please log in with your password.',
      };
    }

    try {
      const result = await LocalAuthentication.authenticateAsync({
        promptMessage: 'Authenticate to access SafeSignal',
        fallbackLabel: 'Use password',
        cancelLabel: 'Cancel',
        disableDeviceFallback: false,
      });

      if (result.success) {
        return { success: true, data: true };
      }

      return {
        success: false,
        error: 'Authentication failed',
      };
    } catch (error: any) {
      return {
        success: false,
        error: error.message || 'Biometric authentication error',
      };
    }
  }

  async enableBiometric(): Promise<ApiResponse> {
    const { available } = await this.checkBiometricAvailability();

    if (!available) {
      return {
        success: false,
        error: ERROR_MESSAGES.BIOMETRIC_UNAVAILABLE,
      };
    }

    // Verify user can authenticate
    const result = await LocalAuthentication.authenticateAsync({
      promptMessage: 'Verify your identity to enable biometric authentication',
    });

    if (!result.success) {
      return {
        success: false,
        error: 'Failed to verify identity',
      };
    }

    await secureStorage.setBiometricEnabled(true);
    return { success: true };
  }

  async disableBiometric(): Promise<void> {
    await secureStorage.setBiometricEnabled(false);
  }

  async isBiometricEnabled(): Promise<boolean> {
    return await secureStorage.isBiometricEnabled();
  }

  getBiometricTypeLabel(type: LocalAuthentication.AuthenticationType | null): string {
    switch (type) {
      case LocalAuthentication.AuthenticationType.FACIAL_RECOGNITION:
        return i18n.t('settings.security.biometricTypes.faceId');
      case LocalAuthentication.AuthenticationType.FINGERPRINT:
        return i18n.t('settings.security.biometricTypes.fingerprint');
      case LocalAuthentication.AuthenticationType.IRIS:
        return i18n.t('settings.security.biometricTypes.iris');
      default:
        return i18n.t('settings.security.biometricTypes.biometric');
    }
  }
}

export const authService = new AuthService();
