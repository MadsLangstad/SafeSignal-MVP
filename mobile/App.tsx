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
      console.log('Step 1: Initializing database...');
      await database.init();

      console.log('Step 2: Loading user session...');
      await loadUser();

      console.log('Step 3: Initializing notifications...');
      const deviceId = Device.modelId || 'unknown-device';
      await notificationService.initialize(deviceId);

      console.log('Step 4: Setting up background sync...');
      setupBackgroundSync();

      console.log('Step 5: Initialization complete!');
      setIsInitializing(false);
    } catch (error) {
      console.error('Initialization error:', error);
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
