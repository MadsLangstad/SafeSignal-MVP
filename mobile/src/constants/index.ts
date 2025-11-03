import Constants from 'expo-constants';

// API Configuration
export const API_CONFIG = {
  BASE_URL:
    Constants.expoConfig?.extra?.apiUrl ||
    (__DEV__
      ? 'http://10.57.74.59:5118'
      : 'https://api.safesignal.io'),
  TIMEOUT: 30000,
  RETRY_ATTEMPTS: 3,
  RETRY_DELAY: 1000,
};

// Database Configuration
export const DB_CONFIG = {
  NAME: 'safesignal.db',
  VERSION: 1,
  CACHE_EXPIRY_HOURS: 24,
  MAX_PENDING_ACTIONS: 1000,
};

// Sync Configuration
export const SYNC_CONFIG = {
  INTERVAL_MS: 30000, // 30 seconds
  BATCH_SIZE: 50,
  MAX_QUEUE_SIZE: 10000,
};

// Alert Configuration
export const ALERT_CONFIG = {
  CONFIRMATION_TIMEOUT_MS: 5000,
  SOUND_DURATION_MS: 3000,
  VIBRATION_PATTERN: [0, 500, 200, 500] as const,
};

// Notification Configuration
export const NOTIFICATION_CONFIG = {
  CHANNEL_ID: 'safesignal-alerts',
  CHANNEL_NAME: 'SafeSignal Alerts',
  CHANNEL_DESCRIPTION: 'Emergency alerts from SafeSignal',
  PRIORITY: 'high' as const,
};

// Auth Configuration
export const AUTH_CONFIG = {
  TOKEN_KEY: 'auth_tokens',
  USER_KEY: 'current_user',
  BIOMETRIC_KEY: 'biometric_enabled',
  TOKEN_REFRESH_THRESHOLD_MS: 300000, // 5 minutes before expiry
};

// UI Constants
export const UI_CONSTANTS = {
  EMERGENCY_BUTTON_SIZE: 200,
  ALERT_HISTORY_PAGE_SIZE: 20,
  MAX_RETRIES_DISPLAY: 3,
};

// Alert Mode Configurations
export const ALERT_MODES = {
  SILENT: {
    label: 'Silent Alert',
    description: 'Notify staff without audible alarm',
    color: '#FFA500',
    icon: 'bell-off',
  },
  AUDIBLE: {
    label: 'Audible Alert',
    description: 'Sound alarm in all rooms',
    color: '#FF6B6B',
    icon: 'bell',
  },
  LOCKDOWN: {
    label: 'Lockdown',
    description: 'Initiate building lockdown',
    color: '#DC2626',
    icon: 'lock',
  },
  EVACUATION: {
    label: 'Evacuation',
    description: 'Evacuate building immediately',
    color: '#8B0000',
    icon: 'exit',
  },
};

// Error Messages
export const ERROR_MESSAGES = {
  NETWORK_ERROR: 'Unable to connect to server. Changes will sync when online.',
  AUTH_FAILED: 'Authentication failed. Please log in again.',
  PERMISSION_DENIED: 'Permission denied. Please enable required permissions.',
  BIOMETRIC_UNAVAILABLE: 'Biometric authentication is not available on this device.',
  ALERT_TRIGGER_FAILED: 'Failed to trigger alert. Please try again.',
  SYNC_FAILED: 'Synchronization failed. Will retry automatically.',
  DATABASE_ERROR: 'Database error occurred. Please restart the app.',
};

// Success Messages
export const SUCCESS_MESSAGES = {
  ALERT_TRIGGERED: 'Emergency alert triggered successfully',
  PROFILE_UPDATED: 'Profile updated successfully',
  SYNC_COMPLETED: 'Synchronization completed',
  LOGGED_OUT: 'Logged out successfully',
};

// Validation Rules
export const VALIDATION = {
  EMAIL_REGEX: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
  PASSWORD_MIN_LENGTH: 8,
  PHONE_REGEX: /^\+?[1-9]\d{1,14}$/,
};
