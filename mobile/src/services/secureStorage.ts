import * as SecureStore from 'expo-secure-store';
import { AUTH_CONFIG } from '../constants';
import type { AuthTokens, User } from '../types';

class SecureStorage {
  async saveTokens(tokens: AuthTokens): Promise<void> {
    try {
      await SecureStore.setItemAsync(
        AUTH_CONFIG.TOKEN_KEY,
        JSON.stringify(tokens),
        {
          requireAuthentication: false, // Don't require auth on every access (too disruptive)
          // SecureStore is already encrypted, protected by device passcode/biometric at OS level
          keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY
        }
      );
    } catch (error) {
      console.error('Failed to save tokens:', error);
      throw error;
    }
  }

  async getTokens(): Promise<AuthTokens | null> {
    try {
      const tokensJson = await SecureStore.getItemAsync(AUTH_CONFIG.TOKEN_KEY);
      if (!tokensJson) return null;

      const tokens = JSON.parse(tokensJson);
      return {
        ...tokens,
        expiresAt: new Date(tokens.expiresAt),
      };
    } catch (error) {
      console.error('Failed to get tokens:', error);
      return null;
    }
  }

  async clearTokens(): Promise<void> {
    try {
      await SecureStore.deleteItemAsync(AUTH_CONFIG.TOKEN_KEY);
    } catch (error) {
      console.error('Failed to clear tokens:', error);
    }
  }

  async saveUser(user: User): Promise<void> {
    try {
      await SecureStore.setItemAsync(
        AUTH_CONFIG.USER_KEY,
        JSON.stringify(user),
        {
          requireAuthentication: false, // Don't require auth on every access
          keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY
        }
      );
    } catch (error) {
      console.error('Failed to save user:', error);
      throw error;
    }
  }

  async getUser(): Promise<User | null> {
    try {
      const userJson = await SecureStore.getItemAsync(AUTH_CONFIG.USER_KEY);
      if (!userJson) return null;

      const user = JSON.parse(userJson);
      return {
        ...user,
        createdAt: new Date(user.createdAt),
      };
    } catch (error) {
      console.error('Failed to get user:', error);
      return null;
    }
  }

  async clearUser(): Promise<void> {
    try {
      await SecureStore.deleteItemAsync(AUTH_CONFIG.USER_KEY);
    } catch (error) {
      console.error('Failed to clear user:', error);
    }
  }

  async setBiometricEnabled(enabled: boolean): Promise<void> {
    try {
      await SecureStore.setItemAsync(
        AUTH_CONFIG.BIOMETRIC_KEY,
        enabled ? '1' : '0',
        {
          requireAuthentication: false,
          keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY
        }
      );
    } catch (error) {
      console.error('Failed to set biometric preference:', error);
    }
  }

  async isBiometricEnabled(): Promise<boolean> {
    try {
      const value = await SecureStore.getItemAsync(AUTH_CONFIG.BIOMETRIC_KEY);
      return value === '1';
    } catch (error) {
      console.error('Failed to check biometric preference:', error);
      return false;
    }
  }

  async clearAll(): Promise<void> {
    await this.clearTokens();
    await this.clearUser();
  }
}

export const secureStorage = new SecureStorage();
