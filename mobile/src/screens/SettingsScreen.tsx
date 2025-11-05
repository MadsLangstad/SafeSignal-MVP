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
import { useTranslation } from 'react-i18next';
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { authService } from '../services/auth';
import { changeLanguage, getCurrentLanguage } from '../i18n';
import type { ThemeMode } from '../store';

export default function SettingsScreen() {
  const { user, logout, syncData, syncStatus } = useAppStore();
  const { theme, setTheme, colorScheme, isDark } = useTheme();
  const { t, i18n } = useTranslation();

  const [biometricAvailable, setBiometricAvailable] = useState(false);
  const [biometricEnabled, setBiometricEnabled] = useState(false);
  const [biometricType, setBiometricType] = useState<string>('Biometric');
  const [requireAuthForAlerts, setRequireAuthForAlerts] = useState(true);
  const [autoLockEnabled, setAutoLockEnabled] = useState(true);
  const [currentLanguage, setCurrentLanguage] = useState<'en' | 'nb'>(getCurrentLanguage());

  const handleLanguageChange = async (lang: 'en' | 'nb') => {
    await changeLanguage(lang);
    setCurrentLanguage(lang);
  };

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
        Alert.alert(t('common.success'), t('settings.security.biometricEnabled', { type: biometricType }));
      } else {
        Alert.alert(t('common.error'), result.error || 'Failed to enable biometric');
      }
    } else {
      await authService.disableBiometric();
      setBiometricEnabled(false);
      Alert.alert(t('common.success'), t('settings.security.biometricDisabled', { type: biometricType }));
    }
  };

  const handleLogout = () => {
    Alert.alert(
      t('auth.confirmLogout'),
      t('auth.confirmLogoutMessage'),
      [
        {
          text: t('common.cancel'),
          style: 'cancel',
        },
        {
          text: t('auth.logout'),
          style: 'destructive',
          onPress: async () => {
            await logout();
            Alert.alert(t('common.success'), t('success.loggedOut'));
          },
        },
      ]
    );
  };

  const handleSync = async () => {
    await syncData();
    Alert.alert(t('common.success'), t('settings.dataSync.syncSuccess'));
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`} edges={['top']}>
      <ScrollView>
        {/* Header */}
        <View className={`p-5 ${
          isDark ? '' : 'bg-white'
        }`}>
          <Text className={`text-3xl font-bold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
            {t('settings.title')}
          </Text>
        </View>

        {/* User Profile Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            {t('settings.sections.profile')}
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
            {t('settings.sections.security')}
          </Text>

          {biometricAvailable && (
            <View className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}>
              <View className="flex-row items-center flex-1">
                <Ionicons name="finger-print" size={24} color="#3B82F6" />
                <View className="ml-4 flex-1">
                  <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                    {t('settings.security.biometric', { type: biometricType })}
                  </Text>
                  <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                    {t('settings.security.biometricDescription', { type: biometricType.toLowerCase() })}
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
                  {t('settings.security.requireAuthForAlerts')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.security.requireAuthForAlertsDescription')}
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
                  {t('settings.security.autoLock')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.security.autoLockDescription')}
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
            {t('settings.sections.appearance')}
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
                  {t('settings.appearance.lightMode')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.appearance.lightModeDescription')}
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
                  {t('settings.appearance.darkMode')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.appearance.darkModeDescription')}
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
                  {t('settings.appearance.system')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.appearance.systemDescriptionWithMode', { mode: colorScheme === 'dark' ? t('settings.appearance.darkMode') : t('settings.appearance.lightMode') })}
                </Text>
              </View>
            </View>
            {theme === 'system' && (
              <Ionicons name="checkmark-circle" size={24} color="#3B82F6" />
            )}
          </TouchableOpacity>
        </View>

        {/* Language Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            {t('settings.sections.language')}
          </Text>

          <TouchableOpacity
            className={`flex-row items-center justify-between px-5 py-4 border-b ${
              isDark ? 'border-gray-800' : 'border-gray-100'
            }`}
            onPress={() => handleLanguageChange('nb')}
          >
            <View className="flex-row items-center flex-1">
              <Ionicons
                name="language"
                size={24}
                color={currentLanguage === 'nb' ? '#3B82F6' : (isDark ? '#9CA3AF' : '#666')}
              />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  {t('settings.language.norwegian')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  Norsk
                </Text>
              </View>
            </View>
            {currentLanguage === 'nb' && (
              <Ionicons name="checkmark-circle" size={24} color="#3B82F6" />
            )}
          </TouchableOpacity>

          <TouchableOpacity
            className={`flex-row items-center justify-between px-5 py-4 ${
              isDark ? '' : ''
            }`}
            onPress={() => handleLanguageChange('en')}
          >
            <View className="flex-row items-center flex-1">
              <Ionicons
                name="language"
                size={24}
                color={currentLanguage === 'en' ? '#3B82F6' : (isDark ? '#9CA3AF' : '#666')}
              />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  {t('settings.language.english')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  English
                </Text>
              </View>
            </View>
            {currentLanguage === 'en' && (
              <Ionicons name="checkmark-circle" size={24} color="#3B82F6" />
            )}
          </TouchableOpacity>
        </View>

        {/* Data & Sync Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            {t('settings.sections.dataSync')}
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
                  {syncStatus.isSyncing ? t('settings.dataSync.syncing') : t('settings.dataSync.syncNow')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {syncStatus.lastSyncAt
                    ? t('settings.dataSync.lastSync', { time: new Date(syncStatus.lastSyncAt).toLocaleString() })
                    : t('settings.dataSync.neverSynced')}
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
                {t('settings.dataSync.pendingActions', { count: syncStatus.pendingActions })}
              </Text>
            </View>
          )}
        </View>

        {/* About Section */}
        <View className={`mt-5 py-3 ${isDark ? '' : 'bg-white'}`}>
          <Text className={`text-xs font-semibold uppercase px-5 mb-3 ${
            isDark ? 'text-gray-500' : 'text-gray-400'
          }`}>
            {t('settings.sections.about')}
          </Text>

          <View className={`flex-row items-center px-5 py-4 border-b ${
            isDark ? 'border-gray-800' : 'border-gray-100'
          }`}>
            <View className="flex-row items-center flex-1">
              <Ionicons name="information-circle-outline" size={24} color={isDark ? '#9CA3AF' : '#666'} />
              <View className="ml-4 flex-1">
                <Text className={`text-base font-medium mb-0.5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  {t('settings.about.version')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.about.versionNumber')}
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
                  {t('settings.about.appName')}
                </Text>
                <Text className={`text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                  {t('settings.about.appDescription')}
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
            {t('auth.logout')}
          </Text>
        </TouchableOpacity>

        <View className="items-center py-8">
          <Text className={`text-xs my-0.5 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            {t('settings.about.copyright')}
          </Text>
          <Text className={`text-xs my-0.5 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            {t('settings.about.authorizedOnly')}
          </Text>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
