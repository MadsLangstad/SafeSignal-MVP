import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  ScrollView,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  Alert as RNAlert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { format } from 'date-fns';
import * as Location from 'expo-location';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../context/ThemeContext';
import { apiClient } from '../services/api';
import type { Alert, AlertClearance, Location as LocationType } from '../types';

type RootStackParamList = {
  AlertClearance: { alertId: string; alert?: Alert };
  AlertHistory: undefined;
};

type AlertClearanceScreenRouteProp = RouteProp<RootStackParamList, 'AlertClearance'>;
type AlertClearanceScreenNavigationProp = NativeStackNavigationProp<RootStackParamList, 'AlertClearance'>;

export default function AlertClearanceScreen() {
  const { t } = useTranslation();
  const { isDark } = useTheme();
  const navigation = useNavigation<AlertClearanceScreenNavigationProp>();
  const route = useRoute<AlertClearanceScreenRouteProp>();
  const { alertId, alert: initialAlert } = route.params;

  const [loading, setLoading] = useState(false);
  const [loadingClearances, setLoadingClearances] = useState(true);
  const [notes, setNotes] = useState('');
  const [location, setLocation] = useState<LocationType | null>(null);
  const [locationLoading, setLocationLoading] = useState(false);
  const [clearances, setClearances] = useState<AlertClearance[]>([]);
  const [alert, setAlert] = useState<Alert | undefined>(initialAlert);

  useEffect(() => {
    loadClearances();
    requestLocation();
  }, []);

  const loadClearances = async () => {
    try {
      setLoadingClearances(true);
      const response = await apiClient.getAlertClearances(alertId);
      if (response.success && response.data) {
        setClearances(response.data);
      }
    } catch (error) {
      console.error('Failed to load clearances:', error);
    } finally {
      setLoadingClearances(false);
    }
  };

  const requestLocation = async () => {
    try {
      setLocationLoading(true);
      const { status } = await Location.requestForegroundPermissionsAsync();
      if (status !== 'granted') {
        RNAlert.alert(
          t('common.error'),
          'Location permission is required for clearance verification. GPS coordinates will be recorded.'
        );
        return;
      }

      const currentLocation = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.High,
      });

      setLocation({
        latitude: currentLocation.coords.latitude,
        longitude: currentLocation.coords.longitude,
      });
    } catch (error) {
      console.error('Failed to get location:', error);
      RNAlert.alert(
        t('common.error'),
        'Could not get your location. You can still proceed, but location will not be recorded.'
      );
    } finally {
      setLocationLoading(false);
    }
  };

  const handleClear = async () => {
    if (!notes.trim()) {
      RNAlert.alert(t('common.error'), 'Please add notes describing the situation.');
      return;
    }

    RNAlert.alert(
      t('alertClearance.title'),
      clearances.length === 0
        ? 'This will be the FIRST clearance. A second person must also clear this alert.\n\nConfirm all is clear?'
        : 'This will be the SECOND clearance and will fully resolve the alert.\n\nConfirm all is clear?',
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('alertClearance.confirmClear'),
          style: 'default',
          onPress: confirmClear,
        },
      ]
    );
  };

  const confirmClear = async () => {
    try {
      setLoading(true);
      const response = await apiClient.clearAlert(alertId, notes, location || undefined);

      if (response.success && response.data) {
        const clearanceStep = response.data.clearanceStep;
        const message = response.data.message;

        RNAlert.alert(
          t('common.success'),
          message,
          [
            {
              text: t('common.ok'),
              onPress: () => {
                if (clearanceStep === 2) {
                  // Alert fully resolved, go back to history
                  navigation.navigate('AlertHistory');
                } else {
                  // First clearance complete, reload clearances
                  loadClearances();
                  setNotes(''); // Clear notes for potential second clearance
                }
              },
            },
          ]
        );
      }
    } catch (error: any) {
      RNAlert.alert(
        t('common.error'),
        error.response?.data?.error || 'Failed to clear alert. Please try again.'
      );
    } finally {
      setLoading(false);
    }
  };

  const canClear = () => {
    // Can clear if there are no clearances yet, or only one clearance exists
    return clearances.length < 2;
  };

  const renderClearanceCard = (clearance: AlertClearance) => {
    return (
      <View
        key={clearance.id}
        className={`mx-4 my-2 p-4 rounded-xl ${isDark ? 'bg-dark-surface' : 'bg-white'} shadow-sm`}
      >
        <View className="flex-row items-center justify-between mb-3">
          <View className="flex-row items-center">
            <View
              className={`w-10 h-10 rounded-full items-center justify-center mr-3 ${
                clearance.clearanceStep === 1
                  ? isDark
                    ? 'bg-blue-900/30'
                    : 'bg-blue-100'
                  : isDark
                  ? 'bg-green-900/30'
                  : 'bg-green-100'
              }`}
            >
              <Text
                className={`text-lg font-bold ${
                  clearance.clearanceStep === 1 ? 'text-blue-500' : 'text-green-500'
                }`}
              >
                {clearance.clearanceStep}
              </Text>
            </View>
            <View>
              <Text className={`font-semibold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {clearance.userName}
              </Text>
              <Text className={`text-sm ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                {clearance.userEmail}
              </Text>
            </View>
          </View>
          <View className={`px-3 py-1 rounded-full ${
            clearance.clearanceStep === 1
              ? isDark
                ? 'bg-blue-900/30'
                : 'bg-blue-100'
              : isDark
              ? 'bg-green-900/30'
              : 'bg-green-100'
          }`}>
            <Text
              className={`text-xs font-semibold ${
                clearance.clearanceStep === 1 ? 'text-blue-500' : 'text-green-500'
              }`}
            >
              {clearance.clearanceStep === 1 ? 'FIRST' : 'SECOND'}
            </Text>
          </View>
        </View>

        <View className="mb-3">
          <Text className={`text-sm font-medium mb-1 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            Cleared At:
          </Text>
          <Text className={isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}>
            {format(clearance.clearedAt, 'MMM dd, yyyy • h:mm a')}
          </Text>
        </View>

        {clearance.notes && (
          <View className="mb-3">
            <Text className={`text-sm font-medium mb-1 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
              Notes:
            </Text>
            <Text className={isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}>
              {clearance.notes}
            </Text>
          </View>
        )}

        {clearance.location && (
          <View className="flex-row items-center">
            <Ionicons
              name="location-outline"
              size={16}
              color={isDark ? '#9CA3AF' : '#6B7280'}
            />
            <Text className={`text-sm ml-1 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
              {clearance.location.latitude.toFixed(6)}, {clearance.location.longitude.toFixed(6)}
            </Text>
          </View>
        )}
      </View>
    );
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`}>
      {/* Header */}
      <View className={`px-4 py-3 border-b ${isDark ? 'border-gray-800' : 'border-gray-200'}`}>
        <TouchableOpacity
          onPress={() => navigation.goBack()}
          className="flex-row items-center mb-2"
        >
          <Ionicons
            name="chevron-back"
            size={24}
            color={isDark ? '#FFFFFF' : '#000000'}
          />
          <Text className={`ml-1 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>{t('common.back')}</Text>
        </TouchableOpacity>
        <Text className={`text-2xl font-bold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
          {t('alertClearance.title')}
        </Text>
        <Text className={`text-sm mt-1 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
          Two-person verification required
        </Text>
      </View>

      <ScrollView className="flex-1">
        {/* Status Banner */}
        <View className="mx-4 mt-4 p-4 rounded-xl bg-blue-500/10 border border-blue-500/20">
          <View className="flex-row items-center mb-2">
            <Ionicons name="information-circle" size={24} color="#3B82F6" />
            <Text className="ml-2 text-blue-500 font-semibold">Clearance Status</Text>
          </View>
          <Text className={isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}>
            {clearances.length === 0 && 'No clearances yet. First verification required.'}
            {clearances.length === 1 && 'First clearance complete. Second verification required.'}
            {clearances.length === 2 && 'Alert fully cleared by two people.'}
          </Text>
        </View>

        {/* Existing Clearances */}
        {loadingClearances ? (
          <View className="py-8">
            <ActivityIndicator size="large" color="#3B82F6" />
          </View>
        ) : (
          <>
            {clearances.length > 0 && (
              <View className="mt-4">
                <Text className={`px-4 mb-2 font-semibold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                  {t('alertHistory.title')}
                </Text>
                {clearances.map(renderClearanceCard)}
              </View>
            )}
          </>
        )}

        {/* Clearance Form */}
        {canClear() && (
          <View className="mx-4 mt-4 mb-6">
            <Text className={`mb-2 font-semibold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
              {clearances.length === 0 ? 'First Clearance' : 'Second Clearance'}
            </Text>

            {/* Notes Input */}
            <View className="mb-4">
              <Text className={`text-sm mb-2 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                Notes (Required) *
              </Text>
              <TextInput
                className={`p-4 rounded-xl ${
                  isDark ? 'bg-dark-surface text-dark-text-primary' : 'bg-white text-light-text-primary'
                } border ${isDark ? 'border-gray-700' : 'border-gray-300'}`}
                placeholder="Describe the situation and verification steps taken..."
                placeholderTextColor={isDark ? '#6B7280' : '#9CA3AF'}
                value={notes}
                onChangeText={setNotes}
                multiline
                numberOfLines={4}
                textAlignVertical="top"
              />
            </View>

            {/* Location Status */}
            <View className="mb-4 flex-row items-center">
              <Ionicons
                name={location ? 'checkmark-circle' : 'location-outline'}
                size={20}
                color={location ? '#10B981' : isDark ? '#9CA3AF' : '#6B7280'}
              />
              <Text className={`ml-2 text-sm ${
                location
                  ? 'text-green-500'
                  : isDark
                  ? 'text-gray-400'
                  : 'text-gray-600'
              }`}>
                {locationLoading
                  ? 'Getting location...'
                  : location
                  ? `Location: ${location.latitude.toFixed(6)}, ${location.longitude.toFixed(6)}`
                  : 'Location not available'}
              </Text>
            </View>

            {/* Clear Button */}
            <TouchableOpacity
              onPress={handleClear}
              disabled={loading || !notes.trim()}
              className={`py-4 rounded-xl items-center ${
                loading || !notes.trim()
                  ? isDark
                    ? 'bg-gray-800'
                    : 'bg-gray-300'
                  : clearances.length === 0
                  ? 'bg-blue-500'
                  : 'bg-green-500'
              }`}
            >
              {loading ? (
                <ActivityIndicator color="#FFFFFF" />
              ) : (
                <Text className="text-white font-semibold text-lg">
                  {clearances.length === 0 ? t('alertClearance.clearAlert') : t('alertClearance.clearAlert')}
                </Text>
              )}
            </TouchableOpacity>
          </View>
        )}

        {/* Fully Cleared Message */}
        {clearances.length === 2 && (
          <View className="mx-4 mt-4 p-4 rounded-xl bg-green-500/10 border border-green-500/20">
            <View className="flex-row items-center mb-2">
              <Ionicons name="checkmark-circle" size={24} color="#10B981" />
              <Text className="ml-2 text-green-500 font-semibold">{t('alertHistory.cleared')}</Text>
            </View>
            <Text className={isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}>
              This alert has been fully cleared by two different people and is now resolved.
            </Text>
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
