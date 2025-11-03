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
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { ALERT_MODES } from '../constants';
import type { Alert } from '../types';

export default function AlertHistoryScreen() {
  const { isDark } = useTheme();
  const {
    alerts,
    hasMoreAlerts,
    loadAlerts,
    loadMoreAlerts,
    syncData,
  } = useAppStore();

  const [refreshing, setRefreshing] = React.useState(false);
  const [loadingMore, setLoadingMore] = React.useState(false);

  useEffect(() => {
    if (alerts.length === 0) {
      loadAlerts(true);
    }
  }, []);

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
      case 'COMPLETED':
        return darkMode ? 'bg-green-900/20' : 'bg-green-50';
      case 'TRIGGERED':
        return darkMode ? 'bg-orange-900/20' : 'bg-orange-50';
      case 'FAILED':
        return darkMode ? 'bg-red-900/20' : 'bg-red-50';
      case 'ESCALATED':
        return darkMode ? 'bg-blue-900/20' : 'bg-blue-50';
      default:
        return darkMode ? 'bg-gray-800' : 'bg-gray-50';
    }
  };

  const renderAlert = ({ item }: { item: Alert }) => {
    const config = ALERT_MODES[item.mode];

    return (
      <TouchableOpacity className={`flex-row mx-4 my-2 rounded-xl overflow-hidden shadow-sm ${
        isDark ? 'bg-dark-surface' : 'bg-white'
      }`}>
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
                {config.label}
              </Text>
            </View>
            <View className={`px-3 py-1.5 rounded-lg ${getStatusStyle(item.status)}`}>
              <Text className={`text-xs font-bold uppercase ${isDark ? 'text-gray-200' : 'text-gray-700'}`}>
                {item.status}
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
                {item.sourceRoomId || 'Unknown Room'}
              </Text>
            </View>

            {!item.synced && (
              <View className="flex-row items-center mt-2 pt-2 border-t border-gray-700">
                <Ionicons
                  name="cloud-upload-outline"
                  size={16}
                  color="#F59E0B"
                />
                <Text className="ml-2.5 text-sm text-amber-500 font-medium">
                  Pending sync
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
        No Alerts
      </Text>
      <Text className={`text-sm mt-2 text-center ${isDark ? 'text-gray-600' : 'text-gray-300'}`}>
        Alert history will appear here once triggered
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
          Alert History
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
