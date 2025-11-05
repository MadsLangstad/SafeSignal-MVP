import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  Animated,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { StackNavigationProp } from '@react-navigation/stack';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../context/ThemeContext';
import { useAppStore } from '../store';
import type { RootStackParamList } from '../navigation';

type RouteProps = RouteProp<RootStackParamList, 'AlertSuccess'>;
type NavigationProps = StackNavigationProp<RootStackParamList>;

export default function AlertSuccessScreen() {
  const { t } = useTranslation();
  const navigation = useNavigation<NavigationProps>();
  const route = useRoute<RouteProps>();
  const { alertId, buildingName, roomName, triggeredAt: triggeredAtString } = route.params;
  const triggeredAt = new Date(triggeredAtString); // Parse ISO string back to Date
  const { isDark } = useTheme();
  const { resolveAlert } = useAppStore();

  const [isResolving, setIsResolving] = useState(false);
  const [isResolved, setIsResolved] = useState(false);

  const scaleAnim = React.useRef(new Animated.Value(0)).current;
  const fadeAnim = React.useRef(new Animated.Value(0)).current;

  useEffect(() => {
    // Animate the success icon
    Animated.sequence([
      Animated.spring(scaleAnim, {
        toValue: 1,
        tension: 50,
        friction: 3,
        useNativeDriver: true,
      }),
      Animated.timing(fadeAnim, {
        toValue: 1,
        duration: 300,
        useNativeDriver: true,
      }),
    ]).start();
  }, []);

  const handleDone = () => {
    navigation.navigate('Main');
  };

  const handleViewHistory = () => {
    navigation.navigate('AlertHistory');
  };

  const handleClearAlert = async () => {
    Alert.alert(
      t('alertClearance.title'),
      t('alertClearance.message'),
      [
        {
          text: t('common.cancel'),
          style: 'cancel',
        },
        {
          text: t('alertClearance.confirmClear'),
          style: 'destructive',
          onPress: async () => {
            setIsResolving(true);
            try {
              const success = await resolveAlert(alertId);
              if (success) {
                setIsResolved(true);
                Alert.alert(t('common.success'), t('alertClearance.clearedSuccessfully'));
              } else {
                Alert.alert(t('common.error'), 'Failed to clear alert. Please try again.');
              }
            } catch (error) {
              console.error('Clear alert error:', error);
              Alert.alert(t('common.error'), 'An unexpected error occurred');
            } finally {
              setIsResolving(false);
            }
          },
        },
      ]
    );
  };

  const formatTime = (date: Date) => {
    return new Date(date).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-white'}`}>
      <View className="flex-1 p-5 items-center justify-center">
        {/* Success Icon */}
        <Animated.View
          style={{
            transform: [{ scale: scaleAnim }],
          }}
        >
          <View className={`w-36 h-36 rounded-full items-center justify-center mb-8 ${
            isDark ? 'bg-green-900/20' : 'bg-green-50'
          }`}>
            <Ionicons name="checkmark-circle" size={100} color="#4CAF50" />
          </View>
        </Animated.View>

        {/* Success Message */}
        <Animated.View style={{ opacity: fadeAnim }}>
          <Text className={`text-3xl font-bold mb-2 text-center ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
            {t('alertSuccess.title')}
          </Text>
          <Text className={`text-base mb-8 text-center ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            {t('alertSuccess.message')}
          </Text>
        </Animated.View>

        {/* Alert Details Card */}
        <View className={`w-full rounded-xl p-4 mb-5 border ${
          isDark
            ? 'bg-dark-surface border-gray-700'
            : 'bg-gray-50 border-gray-200'
        }`}>
          <View className="flex-row items-center py-2">
            <View className="w-9 items-center justify-center">
              <Ionicons
                name="barcode-outline"
                size={20}
                color={isDark ? '#9CA3AF' : '#666'}
              />
            </View>
            <View className="flex-1 ml-3">
              <Text className={`text-xs uppercase tracking-wide mb-0.5 ${
                isDark ? 'text-gray-500' : 'text-gray-400'
              }`}>
                {t('alertSuccess.alertType')}
              </Text>
              <Text className={`text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {alertId}
              </Text>
            </View>
          </View>

          <View className={`h-px my-2 ${isDark ? 'bg-gray-700' : 'bg-gray-200'}`} />

          <View className="flex-row items-center py-2">
            <View className="w-9 items-center justify-center">
              <Ionicons
                name="time-outline"
                size={20}
                color={isDark ? '#9CA3AF' : '#666'}
              />
            </View>
            <View className="flex-1 ml-3">
              <Text className={`text-xs uppercase tracking-wide mb-0.5 ${
                isDark ? 'text-gray-500' : 'text-gray-400'
              }`}>
                {t('alertSuccess.timestamp')}
              </Text>
              <Text className={`text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {formatTime(triggeredAt)}
              </Text>
            </View>
          </View>

          <View className={`h-px my-2 ${isDark ? 'bg-gray-700' : 'bg-gray-200'}`} />

          <View className="flex-row items-center py-2">
            <View className="w-9 items-center justify-center">
              <Ionicons
                name="business-outline"
                size={20}
                color={isDark ? '#9CA3AF' : '#666'}
              />
            </View>
            <View className="flex-1 ml-3">
              <Text className={`text-xs uppercase tracking-wide mb-0.5 ${
                isDark ? 'text-gray-500' : 'text-gray-400'
              }`}>
                {t('alertConfirmation.building')}
              </Text>
              <Text className={`text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {buildingName}
              </Text>
            </View>
          </View>

          <View className={`h-px my-2 ${isDark ? 'bg-gray-700' : 'bg-gray-200'}`} />

          <View className="flex-row items-center py-2">
            <View className="w-9 items-center justify-center">
              <Ionicons
                name="location-outline"
                size={20}
                color={isDark ? '#9CA3AF' : '#666'}
              />
            </View>
            <View className="flex-1 ml-3">
              <Text className={`text-xs uppercase tracking-wide mb-0.5 ${
                isDark ? 'text-gray-500' : 'text-gray-400'
              }`}>
                {t('alertConfirmation.room')}
              </Text>
              <Text className={`text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {roomName}
              </Text>
            </View>
          </View>
        </View>

        {/* Status Badge */}
        <View className={`flex-row items-center px-4 py-2 rounded-full mb-8 ${
          isResolved
            ? (isDark ? 'bg-green-900/20' : 'bg-green-50')
            : (isDark ? 'bg-blue-900/20' : 'bg-blue-50')
        }`}>
          <View className={`w-2 h-2 rounded-full mr-2 ${
            isResolved ? 'bg-green-500' : 'bg-blue-500'
          }`} />
          <Text className={`text-sm font-semibold ${
            isResolved
              ? (isDark ? 'text-green-400' : 'text-green-700')
              : (isDark ? 'text-blue-400' : 'text-blue-700')
          }`}>
            {t('alertSuccess.status')}: {isResolved ? t('alertHistory.cleared') : t('alertSuccess.active')}
          </Text>
        </View>

        {/* Actions */}
        <View className="w-full space-y-3">
          {!isResolved && (
            <TouchableOpacity
              className={`w-full h-14 rounded-xl justify-center items-center flex-row ${
                isResolving ? 'opacity-60 bg-orange-400' : 'bg-orange-500'
              }`}
              onPress={handleClearAlert}
              disabled={isResolving}
            >
              {isResolving ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <>
                  <Ionicons name="checkmark-done" size={20} color="#fff" />
                  <Text className="text-base font-semibold text-white ml-2">
                    {t('alertClearance.clearAlert')}
                  </Text>
                </>
              )}
            </TouchableOpacity>
          )}

          <View className="flex-row w-full space-x-3">
            <TouchableOpacity
              className={`flex-1 h-14 rounded-xl justify-center items-center flex-row border-2 border-primary ${
                isDark ? 'bg-dark-surface' : 'bg-white'
              }`}
              onPress={handleViewHistory}
            >
              <Ionicons name="list-outline" size={20} color="#3B82F6" />
              <Text className="text-base font-semibold text-primary ml-2">
                {t('alertHistory.title')}
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              className="flex-1 h-14 rounded-xl justify-center items-center bg-green-500"
              onPress={handleDone}
            >
              <Text className="text-base font-semibold text-white">
                {t('common.done')}
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </SafeAreaView>
  );
}
