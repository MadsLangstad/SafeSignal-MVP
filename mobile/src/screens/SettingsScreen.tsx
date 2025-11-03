import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  Switch,
  Alert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { authService } from '../services/auth';
import { SUCCESS_MESSAGES } from '../constants';
import type { ThemeMode } from '../store';

export default function SettingsScreen() {
  const { user, logout, syncData, syncStatus } = useAppStore();
  const { theme, setTheme, colorScheme, isDark } = useTheme();

  const [biometricAvailable, setBiometricAvailable] = useState(false);
  const [biometricEnabled, setBiometricEnabled] = useState(false);
  const [biometricType, setBiometricType] = useState<string>('Biometric');
  const [requireAuthForAlerts, setRequireAuthForAlerts] = useState(true);
  const [autoLockEnabled, setAutoLockEnabled] = useState(true);

  useEffect(() => {
    checkBiometric();
  }, []);

  const checkBiometric = async () => {
    const { available, type } = await authService.checkBiometricAvailability();
    setBiometricAvailable(available);

    if (available) {
      const enabled = await authService.isBiometricEnabled();
      setBiometricEnabled(enabled);
      setBiometricType(authService.getBiometricTypeLabel(type));
    }
  };

  const handleBiometricToggle = async (value: boolean) => {
    if (value) {
      const result = await authService.enableBiometric();
      if (result.success) {
        setBiometricEnabled(true);
        Alert.alert('Success', `${biometricType} authentication enabled`);
      } else {
        Alert.alert('Error', result.error || 'Failed to enable biometric');
      }
    } else {
      await authService.disableBiometric();
      setBiometricEnabled(false);
      Alert.alert('Success', `${biometricType} authentication disabled`);
    }
  };

  const handleLogout = () => {
    Alert.alert(
      'Confirm Logout',
      'Are you sure you want to log out?',
      [
        {
          text: 'Cancel',
          style: 'cancel',
        },
        {
          text: 'Logout',
          style: 'destructive',
          onPress: async () => {
            await logout();
            Alert.alert('Success', SUCCESS_MESSAGES.LOGGED_OUT);
          },
        },
      ]
    );
  };

  const handleSync = async () => {
    await syncData();
    Alert.alert('Success', 'Data synchronized successfully');
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`} edges={['top']}>
      <ScrollView>
        {/* Header */}
        <View className={`p-5 ${
          isDark ? '' : 'bg-white'
        }`}>
          <Text className={`text-3xl font-bold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
            Settings
          </Text>
        </View>

        {/* User Profile Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            Profile
          </Text>

          <View className="flex-row items-center px-5 py-4">
            <View className={`w-[70px] h-[70px] rounded-full items-center justify-center mr-4 ${
              isDark ? 'bg-primary/20' : 'bg-primary-light'
            }`}>
              <Ionicons name="person" size={40} color="#3B82F6" />
            </View>
            <View className="flex-1">
              <Text className={`text-xl font-semibold mb-1 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {user?.name}
              </Text>
              <Text className={`text-sm mb-0.5 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                {user?.email}
              </Text>
              {user?.phoneNumber && (
                <Text className={`text-sm ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {user.phoneNumber}
                </Text>
              )}
            </View>
          </View>
        </View>

        {/* Security Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            Security
          </Text>

          {biometricAvailable && (
            <View className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}>
              <View className="flex-row items-center flex-1">
                <Ionicons name="finger-print" size={24} color="#3B82F6" />
                <View className="ml-4 flex-1">
                  <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                    {biometricType}
                  </Text>
                  <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                    Use {biometricType.toLowerCase()} to unlock
                  </Text>
                </View>
              </View>
              <Switch
                value={biometricEnabled}
                onValueChange={handleBiometricToggle}
                trackColor={{ false: '#ccc', true: '#3B82F6' }}
                thumbColor="#fff"
              />
            </View>
          )}

          <View className={`flex-row items-center justify-between px-5 py-4 border-b ${
            isDark ? 'border-gray-800' : 'border-gray-100'
          }`}>
            <View className="flex-row items-center flex-1">
              <Ionicons name="shield-checkmark" size={24} color="#3B82F6" />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  Require Authentication for Alerts
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Prevent accidental emergency triggers
                </Text>
              </View>
            </View>
            <Switch
              value={requireAuthForAlerts}
              onValueChange={setRequireAuthForAlerts}
              trackColor={{ false: '#ccc', true: '#3B82F6' }}
              thumbColor="#fff"
            />
          </View>

          <View className={`flex-row items-center justify-between px-5 py-4 ${
            isDark ? '' : ''
          }`}>
            <View className="flex-row items-center flex-1">
              <Ionicons name="lock-closed" size={24} color="#3B82F6" />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  Auto-Lock
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Lock app after 5 minutes of inactivity
                </Text>
              </View>
            </View>
            <Switch
              value={autoLockEnabled}
              onValueChange={setAutoLockEnabled}
              trackColor={{ false: '#ccc', true: '#3B82F6' }}
              thumbColor="#fff"
            />
          </View>
        </View>

        {/* Appearance Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            Appearance
          </Text>

          <TouchableOpacity
            className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}
            onPress={() => setTheme('light')}
          >
            <View className="flex-row items-center flex-1">
              <Ionicons
                name="sunny"
                size={24}
                color={theme === 'light' ? '#3B82F6' : (isDark ? '#9CA3AF' : '#666')}
              />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  Light Mode
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Use light colors
                </Text>
              </View>
            </View>
            {theme === 'light' && (
              <Ionicons name="checkmark-circle" size={24} color="#3B82F6" />
            )}
          </TouchableOpacity>

          <TouchableOpacity
            className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}
            onPress={() => setTheme('dark')}
          >
            <View className="flex-row items-center flex-1">
              <Ionicons
                name="moon"
                size={24}
                color={theme === 'dark' ? '#3B82F6' : (isDark ? '#9CA3AF' : '#666')}
              />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  Dark Mode
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Use dark colors
                </Text>
              </View>
            </View>
            {theme === 'dark' && (
              <Ionicons name="checkmark-circle" size={24} color="#3B82F6" />
            )}
          </TouchableOpacity>

          <TouchableOpacity
            className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}
            onPress={() => setTheme('system')}
          >
            <View className="flex-row items-center flex-1">
              <Ionicons
                name="phone-portrait-outline"
                size={24}
                color={theme === 'system' ? '#3B82F6' : (isDark ? '#9CA3AF' : '#666')}
              />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  System
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Follow system preference {colorScheme === 'dark' ? '(Dark)' : '(Light)'}
                </Text>
              </View>
            </View>
            {theme === 'system' && (
              <Ionicons name="checkmark-circle" size={24} color="#3B82F6" />
            )}
          </TouchableOpacity>
        </View>

        {/* Data & Sync Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            Data & Sync
          </Text>

          <TouchableOpacity
            className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}
            onPress={handleSync}
            disabled={syncStatus.isSyncing}
          >
            <View className="flex-row items-center flex-1">
              <Ionicons
                name={syncStatus.isSyncing ? 'sync' : 'cloud-upload-outline'}
                size={24}
                color="#3B82F6"
              />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  {syncStatus.isSyncing ? 'Syncing...' : 'Sync Now'}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {syncStatus.lastSyncAt
                    ? `Last sync: ${new Date(syncStatus.lastSyncAt).toLocaleString()}`
                    : 'Never synced'}
                </Text>
              </View>
            </View>
            <Ionicons name="chevron-forward" size={20} color={isDark ? '#6B7280' : '#999'} />
          </TouchableOpacity>

          {syncStatus.pendingActions > 0 && (
            <View className={`flex-row items-center px-5 py-3 ${
              isDark ? 'bg-orange-900/20' : 'bg-orange-50'
            }`}>
              <Ionicons name="information-circle-outline" size={20} color="#FFA500" />
              <Text className={`ml-2 text-xs flex-1 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                {syncStatus.pendingActions} pending action(s) will sync when online
              </Text>
            </View>
          )}
        </View>

        {/* About Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            About
          </Text>

          <View className={`flex-row items-center px-5 py-4 border-b ${
            isDark ? 'border-gray-800' : 'border-gray-100'
          }`}>
            <View className="flex-row items-center flex-1">
              <Ionicons name="information-circle-outline" size={24} color={isDark ? '#9CA3AF' : '#666'} />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  Version
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  1.0.0 (MVP)
                </Text>
              </View>
            </View>
          </View>

          <View className={`flex-row items-center px-5 py-4 border-b ${
            isDark ? 'border-gray-800' : 'border-gray-100'
          }`}>
            <View className="flex-row items-center flex-1">
              <Ionicons name="shield-checkmark-outline" size={24} color={isDark ? '#9CA3AF' : '#666'} />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  SafeSignal
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Emergency Alert System
                </Text>
              </View>
            </View>
          </View>
        </View>

        {/* Logout Button */}
        <TouchableOpacity
          className={`flex-row items-center justify-center mx-5 mt-8 py-4 rounded-xl border-2 border-red-600 ${
            isDark ? '' : 'bg-white'
          }`}
          onPress={handleLogout}
        >
          <Ionicons name="log-out-outline" size={24} color="#DC2626" />
          <Text className="ml-2 text-base font-semibold text-red-600">
            Log Out
          </Text>
        </TouchableOpacity>

        <View className="items-center py-8">
          <Text className={`text-xs my-0.5 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            © 2025 SafeSignal
          </Text>
          <Text className={`text-xs my-0.5 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            For authorized personnel only
          </Text>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
