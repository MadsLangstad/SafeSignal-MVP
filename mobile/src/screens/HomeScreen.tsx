import React, { useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  Alert,
  ScrollView,
  RefreshControl,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { StackNavigationProp } from '@react-navigation/stack';
import { useTranslation } from 'react-i18next';
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { ALERT_MODES, UI_CONSTANTS } from '../constants';
import type { RootStackParamList, AlertMode } from '../types';

type NavigationProp = StackNavigationProp<RootStackParamList>;

export default function HomeScreen() {
  const { t } = useTranslation();
  const navigation = useNavigation<NavigationProp>();
  const { isDark } = useTheme();

  const {
    user,
    buildings,
    selectedBuilding,
    selectedRoomId,
    selectBuilding,
    selectRoom,
    loadBuildings,
    syncData,
    syncStatus,
  } = useAppStore();

  const [refreshing, setRefreshing] = React.useState(false);

  useEffect(() => {
    // Only load buildings if user is authenticated
    if (user && buildings.length === 0) {
      loadBuildings();
    }
  }, [user]);

  const onRefresh = async () => {
    setRefreshing(true);
    await syncData();
    setRefreshing(false);
  };

  const handleAlertPress = (mode: AlertMode) => {
    if (!selectedBuilding) {
      Alert.alert(t('home.buildingRequired'), t('home.buildingRequiredMessage'));
      return;
    }

    if (!selectedRoomId) {
      Alert.alert(t('home.roomRequired'), t('home.roomRequiredMessage'));
      return;
    }

    navigation.navigate('AlertConfirmation', { mode });
  };

  const renderBuildingSelector = () => {
    if (buildings.length === 0) {
      return (
        <View className="mb-5">
          <Text className={`text-sm font-semibold mb-3 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
            {t('home.noBuildingsAvailable')}
          </Text>
          <TouchableOpacity
            className="flex-row items-center"
            onPress={loadBuildings}
          >
            <Ionicons name="refresh" size={20} color="#3B82F6" />
            <Text className="text-primary font-semibold ml-2">{t('common.refresh')}</Text>
          </TouchableOpacity>
        </View>
      );
    }

    return (
      <View className="mb-5">
        <Text className={`text-sm font-semibold mb-3 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
          {t('home.selectBuilding')}
        </Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          {buildings.map((building) => (
            <TouchableOpacity
              key={building.id}
              className={`flex-row items-center px-4 py-2.5 rounded-full mr-3 border-2 ${
                selectedBuilding?.id === building.id
                  ? 'bg-primary border-primary'
                  : (isDark ? 'bg-dark-surface' : 'bg-white') + ' border-primary'
              }`}
              onPress={() => selectBuilding(building)}
            >
              <Ionicons
                name="business"
                size={16}
                color={selectedBuilding?.id === building.id ? '#fff' : '#3B82F6'}
              />
              <Text
                className={`ml-2 font-semibold ${
                  selectedBuilding?.id === building.id
                    ? 'text-white'
                    : 'text-primary'
                }`}
              >
                {building.name}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>
    );
  };

  const renderRoomSelector = () => {
    if (!selectedBuilding) {
      return null;
    }

    return (
      <View className="mb-5">
        <Text className={`text-sm font-semibold mb-3 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
          {t('home.selectRoom')}
        </Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          {selectedBuilding.rooms.map((room) => (
            <TouchableOpacity
              key={room.id}
              className={`flex-row items-center px-4 py-2.5 rounded-full mr-3 border ${
                selectedRoomId === room.id
                  ? (isDark ? 'bg-gray-500 border-gray-500' : 'bg-gray-600 border-gray-600')
                  : (isDark ? 'bg-dark-surface border-gray-600' : 'bg-white border-gray-300')
              }`}
              onPress={() => selectRoom(room.id)}
            >
              <Ionicons
                name="location"
                size={16}
                color={selectedRoomId === room.id ? '#fff' : (isDark ? '#9CA3AF' : '#666')}
              />
              <Text
                className={`ml-2 font-semibold ${
                  selectedRoomId === room.id
                    ? 'text-white'
                    : (isDark ? 'text-gray-300' : 'text-gray-600')
                }`}
              >
                {room.name}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>
    );
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`} edges={['top']}>
      <ScrollView
        contentContainerStyle={{ padding: 20 }}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
      >
        {/* Header */}
        <View className="flex-row justify-between items-center mb-8">
          <View>
            <Text className={`text-base ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
              {t('home.welcomeBack')}
            </Text>
            <Text className={`text-2xl font-bold mt-1 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
              {user?.name || 'User'}
            </Text>
          </View>
          {syncStatus.pendingActions > 0 && (
            <View className={`flex-row items-center px-3 py-1.5 rounded-full ${
              isDark ? 'bg-orange-900/20' : 'bg-orange-50'
            }`}>
              <Ionicons name="cloud-upload-outline" size={16} color="#FFA500" />
              <Text className="ml-1.5 text-orange-500 font-semibold">
                {syncStatus.pendingActions}
              </Text>
            </View>
          )}
        </View>

        {/* Building & Room Selection */}
        {renderBuildingSelector()}
        {renderRoomSelector()}

        {/* Emergency Button */}
        <View className="items-center my-10">
          <Text className={`text-xl font-bold mb-2 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
            {t('home.emergencyAlert')}
          </Text>
          <Text className={`text-sm mb-8 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            {t('home.pressAndHold')}
          </Text>

          <TouchableOpacity
            className={`items-center justify-center rounded-full shadow-lg ${
              !selectedBuilding || !selectedRoomId
                ? (isDark ? 'bg-gray-600' : 'bg-gray-300')
                : 'bg-red-600'
            }`}
            style={{
              width: UI_CONSTANTS.EMERGENCY_BUTTON_SIZE,
              height: UI_CONSTANTS.EMERGENCY_BUTTON_SIZE,
              shadowColor: !selectedBuilding || !selectedRoomId ? '#000' : '#DC2626',
              shadowOffset: { width: 0, height: 4 },
              shadowOpacity: 0.3,
              shadowRadius: 8,
              elevation: 8,
            }}
            onPress={() => handleAlertPress('AUDIBLE')}
            onLongPress={() => handleAlertPress('AUDIBLE')}
            delayLongPress={500}
            disabled={!selectedBuilding || !selectedRoomId}
          >
            <Ionicons name="alert-circle" size={80} color="#fff" />
            <Text className="text-white text-lg font-bold mt-2">{t('home.emergency')}</Text>
          </TouchableOpacity>
        </View>

        {/* Quick Alert Modes */}
        <View className="mt-5">
          <Text className={`text-lg font-bold mb-4 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
            {t('home.alertModes')}
          </Text>
          <View className="space-y-3">
            {(Object.keys(ALERT_MODES) as AlertMode[]).map((mode) => {
              const config = ALERT_MODES[mode];
              const modeKey = mode.toLowerCase() as 'audible' | 'silent' | 'medical' | 'fire';
              return (
                <TouchableOpacity
                  key={mode}
                  className={`p-4 rounded-xl shadow-sm ${isDark ? 'bg-dark-surface' : 'bg-white'}`}
                  style={{
                    borderLeftWidth: 4,
                    borderLeftColor: config.color,
                  }}
                  onPress={() => handleAlertPress(mode)}
                  disabled={!selectedBuilding || !selectedRoomId}
                >
                  <View className="mb-2">
                    <Text className={`text-base font-semibold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                      {t(`alertModes.${modeKey}.label`)}
                    </Text>
                  </View>
                  <Text className={`text-sm ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                    {t(`alertModes.${modeKey}.description`)}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>

        {/* Status Info */}
        <View className="mt-8 items-center">
          <View className="flex-row items-center">
            {syncStatus.isSyncing || syncStatus.pendingCount > 0 ? (
              <>
                <Ionicons name="sync" size={20} color="#FF9800" />
                <Text className={`ml-2 text-sm font-semibold ${isDark ? 'text-orange-400' : 'text-orange-600'}`}>
                  {t('home.syncing', { count: syncStatus.pendingCount })}
                </Text>
              </>
            ) : (
              <>
                <Ionicons name="checkmark-circle" size={20} color="#4CAF50" />
                <Text className={`ml-2 text-sm font-semibold ${isDark ? 'text-green-400' : 'text-green-600'}`}>
                  {t('home.systemOnline')}
                </Text>
              </>
            )}
          </View>
          {syncStatus.lastSyncAt && (
            <Text className={`mt-2 text-xs ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
              {t('home.lastSync', { time: new Date(syncStatus.lastSyncAt).toLocaleTimeString() })}
            </Text>
          )}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
