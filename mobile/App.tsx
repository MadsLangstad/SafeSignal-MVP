import React, { useEffect, useState } from 'react';
import { StatusBar } from 'expo-status-bar';
import { View, ActivityIndicator, StyleSheet } from 'react-native';
import * as Device from 'expo-device';
import { RootNavigator } from './src/navigation';
import { useAppStore } from './src/store';
import { database } from './src/database';
import { notificationService } from './src/services/notifications';
import { SYNC_CONFIG } from './src/constants';
import { ThemeProvider } from './src/context/ThemeContext';
import { initI18n } from './src/i18n';
import './global.css';

export default function App() {
  const [isInitializing, setIsInitializing] = useState(true);
  const { loadUser, syncData } = useAppStore();

  useEffect(() => {
    initialize();

    return () => {
      cleanup();
    };
  }, []);

  const initialize = async () => {
    try {
      console.log('Step 1: Initializing i18n...');
      await initI18n();

      console.log('Step 2: Initializing database...');
      await database.init();

      console.log('Step 3: Loading user session...');
      // Use timeout to prevent hanging on slow operations
      await Promise.race([
        loadUser(),
        new Promise((_, reject) =>
          setTimeout(() => reject(new Error('User load timeout')), 5000)
        )
      ]).catch((error) => {
        console.warn('User load timed out or failed (non-critical):', error);
        // Continue initialization even if user load fails
      });

      console.log('Step 4: Initializing notifications...');
      const deviceId = Device.modelId || 'unknown-device';
      await notificationService.initialize(deviceId).catch((error) => {
        console.warn('Notification initialization failed (non-critical):', error);
        // Continue even if notifications fail
      });

      console.log('Step 5: Setting up background sync...');
      setupBackgroundSync();

      console.log('Step 6: Initialization complete!');
      setIsInitializing(false);
    } catch (error) {
      console.error('Initialization error:', error);
      // Always set initializing to false to show UI
      setIsInitializing(false);
    }
  };

  const setupBackgroundSync = () => {
    // Sync periodically - only if authenticated
    const intervalId = setInterval(() => {
      const { isAuthenticated } = useAppStore.getState();
      if (isAuthenticated) {
        syncData();
      }
    }, SYNC_CONFIG.INTERVAL_MS);

    // Store interval ID for cleanup
    (global as any).__syncIntervalId = intervalId;
  };

  const cleanup = () => {
    // Clear sync interval
    const intervalId = (global as any).__syncIntervalId;
    if (intervalId) {
      clearInterval(intervalId);
    }

    // Cleanup notification listeners
    notificationService.cleanup();
  };

  if (isInitializing) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#3B82F6" />
      </View>
    );
  }

  return (
    <ThemeProvider>
      <StatusBar style="auto" />
      <RootNavigator />
    </ThemeProvider>
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
  },
});
