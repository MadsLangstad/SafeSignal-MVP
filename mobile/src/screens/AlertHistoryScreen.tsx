import React, { useEffect } from 'react';
import {
  View,
  Text,
  FlatList,
  RefreshControl,
  TouchableOpacity,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { format } from 'date-fns';
import { useNavigation } from '@react-navigation/native';
import { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { ALERT_MODES } from '../constants';
import type { Alert } from '../types';

type RootStackParamList = {
  AlertClearance: { alertId: string; alert: Alert };
  AlertHistory: undefined;
};

type AlertHistoryScreenNavigationProp = NativeStackNavigationProp<RootStackParamList, 'AlertHistory'>;

export default function AlertHistoryScreen() {
  const { t } = useTranslation();
  const navigation = useNavigation<AlertHistoryScreenNavigationProp>();
  const { isDark } = useTheme();
  const {
    user,
    alerts,
    buildings,
    hasMoreAlerts,
    loadAlerts,
    loadMoreAlerts,
    syncData,
  } = useAppStore();

  const [refreshing, setRefreshing] = React.useState(false);
  const [loadingMore, setLoadingMore] = React.useState(false);

  useEffect(() => {
    // Only load alerts if user is authenticated
    if (user && alerts.length === 0) {
      loadAlerts(true);
    }
  }, [user]);

  const onRefresh = async () => {
    setRefreshing(true);
    await syncData();
    await loadAlerts(true);
    setRefreshing(false);
  };

  const onLoadMore = async () => {
    if (!hasMoreAlerts || loadingMore) return;

    setLoadingMore(true);
    await loadMoreAlerts();
    setLoadingMore(false);
  };

  const getStatusStyle = (status: string) => {
    const darkMode = isDark;
    switch (status) {
      case 'Resolved':
      case 'COMPLETED':
        return darkMode ? 'bg-green-900/20' : 'bg-green-50';
      case 'PendingClearance':
        return darkMode ? 'bg-yellow-900/20' : 'bg-yellow-50';
      case 'New':
      case 'TRIGGERED':
        return darkMode ? 'bg-orange-900/20' : 'bg-orange-50';
      case 'Acknowledged':
        return darkMode ? 'bg-blue-900/20' : 'bg-blue-50';
      case 'Cancelled':
      case 'FAILED':
        return darkMode ? 'bg-red-900/20' : 'bg-red-50';
      case 'ESCALATED':
        return darkMode ? 'bg-blue-900/20' : 'bg-blue-50';
      default:
        return darkMode ? 'bg-gray-800' : 'bg-gray-50';
    }
  };

  const getClearanceBadge = (alert: Alert) => {
    if (alert.status === 'PendingClearance') {
      return (
        <View className={`flex-row items-center mt-2 px-2 py-1 rounded-full ${
          isDark ? 'bg-yellow-900/30' : 'bg-yellow-100'
        }`}>
          <Ionicons name="people-outline" size={14} color="#F59E0B" />
          <Text className="ml-1 text-xs font-semibold text-yellow-500">
            1/2 Cleared
          </Text>
        </View>
      );
    }
    if (alert.status === 'Resolved' && alert.fullyClearedAt) {
      return (
        <View className={`flex-row items-center mt-2 px-2 py-1 rounded-full ${
          isDark ? 'bg-green-900/30' : 'bg-green-100'
        }`}>
          <Ionicons name="checkmark-done-outline" size={14} color="#10B981" />
          <Text className="ml-1 text-xs font-semibold text-green-500">
            2/2 Cleared
          </Text>
        </View>
      );
    }
    return null;
  };

  const getBuildingAndRoomName = (alert: Alert) => {
    const building = buildings.find((b) => b.id === alert.buildingId);
    if (!building) return 'Unknown Location';

    const room = building.rooms.find((r) => r.id === alert.sourceRoomId);
    if (!room) return building.name;

    return `${building.name} • ${room.name}`;
  };

  const getStatusTranslation = (status: string): string => {
    const statusKey = `alertHistory.statuses.${status}`;
    const translated = t(statusKey);
    // If translation key doesn't exist, return the original status
    return translated === statusKey ? status : translated;
  };

  const renderAlert = ({ item }: { item: Alert }) => {
    const config = ALERT_MODES[item.mode];
    const modeKey = item.mode.toLowerCase() as 'audible' | 'silent' | 'lockdown' | 'evacuation';
    const location = getBuildingAndRoomName(item);
    const clearanceBadge = getClearanceBadge(item);
    const statusText = getStatusTranslation(item.status);

    return (
      <TouchableOpacity
        className={`flex-row mx-4 my-2 rounded-xl overflow-hidden shadow-sm ${
          isDark ? 'bg-dark-surface' : 'bg-white'
        }`}
        onPress={() => navigation.navigate('AlertClearance', { alertId: item.id, alert: item })}
      >
        <View
          className="w-1.5"
          style={{ backgroundColor: config.color }}
        />

        <View className="flex-1 p-4">
          <View className="flex-row justify-between items-center mb-3">
            <View className="flex-row items-center flex-1">
              <Ionicons
                name={config.icon as any}
                size={22}
                color={config.color}
                style={{ marginRight: 8 }}
              />
              <Text className={`text-base font-semibold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {t(`alertModes.${modeKey}.label`)}
              </Text>
            </View>
            <View className={`px-3 py-1.5 rounded-lg ${getStatusStyle(item.status)}`}>
              <Text className={`text-xs font-bold uppercase ${isDark ? 'text-gray-200' : 'text-gray-700'}`}>
                {statusText}
              </Text>
            </View>
          </View>

          <View className="space-y-2.5">
            <View className="flex-row items-center">
              <Ionicons
                name="time-outline"
                size={16}
                color={isDark ? '#9CA3AF' : '#666'}
              />
              <Text className={`ml-2.5 text-sm ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                {format(new Date(item.triggeredAt), 'MMM dd, yyyy • h:mm a')}
              </Text>
            </View>

            <View className="flex-row items-center">
              <Ionicons
                name="location-outline"
                size={16}
                color={isDark ? '#9CA3AF' : '#666'}
              />
              <Text className={`ml-2.5 text-sm ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
                {location}
              </Text>
            </View>

            {clearanceBadge}

            {!item.synced && (
              <View className="flex-row items-center mt-2 pt-2 border-t border-gray-700">
                <Ionicons
                  name="cloud-upload-outline"
                  size={16}
                  color="#F59E0B"
                />
                <Text className="ml-2.5 text-sm text-amber-500 font-medium">
                  {t('home.syncing', { count: 1 })}
                </Text>
              </View>
            )}
          </View>
        </View>
      </TouchableOpacity>
    );
  };

  const renderEmpty = () => (
    <View className="flex-1 justify-center items-center p-10">
      <Ionicons
        name="list-outline"
        size={80}
        color={isDark ? '#4B5563' : '#ccc'}
      />
      <Text className={`text-xl font-semibold mt-5 ${isDark ? 'text-gray-500' : 'text-gray-400'}`}>
        {t('alertHistory.noAlerts')}
      </Text>
      <Text className={`text-sm mt-2 text-center ${isDark ? 'text-gray-600' : 'text-gray-300'}`}>
        {t('alertHistory.noAlertsMessage')}
      </Text>
    </View>
  );

  const renderFooter = () => {
    if (!loadingMore) return null;

    return (
      <View className="py-5 items-center">
        <ActivityIndicator size="small" color="#3B82F6" />
      </View>
    );
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`} edges={['top']}>
      <View className={`p-5 ${
        isDark ? '' : 'bg-white'
      }`}>
        <Text className={`text-3xl font-bold ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
          {t('alertHistory.title')}
        </Text>
      </View>

      <FlatList
        data={alerts}
        keyExtractor={(item) => item.id}
        renderItem={renderAlert}
        ListEmptyComponent={renderEmpty}
        ListFooterComponent={renderFooter}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
        onEndReached={onLoadMore}
        onEndReachedThreshold={0.5}
        contentContainerStyle={alerts.length === 0 ? { flexGrow: 1 } : undefined}
      />
    </SafeAreaView>
  );
}
