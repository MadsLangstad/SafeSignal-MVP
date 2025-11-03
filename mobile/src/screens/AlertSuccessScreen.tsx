import React, { useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  Animated,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { StackNavigationProp } from '@react-navigation/stack';
import { useTheme } from '../context/ThemeContext';
import type { RootStackParamList } from '../navigation';

type RouteProps = RouteProp<RootStackParamList, 'AlertSuccess'>;
type NavigationProps = StackNavigationProp<RootStackParamList>;

export default function AlertSuccessScreen() {
  const navigation = useNavigation<NavigationProps>();
  const route = useRoute<RouteProps>();
  const { alertId, buildingName, roomName, triggeredAt } = route.params;
  const { isDark } = useTheme();

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
            Alert Triggered Successfully
          </Text>
          <Text className={`text-base mb-8 text-center ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            Emergency responders have been notified
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
                Alert ID
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
                Time
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
                Building
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
                Room
              </Text>
              <Text className={`text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
                {roomName}
              </Text>
            </View>
          </View>
        </View>

        {/* Status Badge */}
        <View className={`flex-row items-center px-4 py-2 rounded-full mb-8 ${
          isDark ? 'bg-blue-900/20' : 'bg-blue-50'
        }`}>
          <View className="w-2 h-2 rounded-full bg-blue-500 mr-2" />
          <Text className={`text-sm font-semibold ${isDark ? 'text-blue-400' : 'text-blue-700'}`}>
            Status: New
          </Text>
        </View>

        {/* Actions */}
        <View className="flex-row w-full space-x-3">
          <TouchableOpacity
            className={`flex-1 h-14 rounded-xl justify-center items-center flex-row border-2 border-primary ${
              isDark ? 'bg-dark-surface' : 'bg-white'
            }`}
            onPress={handleViewHistory}
          >
            <Ionicons name="list-outline" size={20} color="#3B82F6" />
            <Text className="text-base font-semibold text-primary ml-2">
              View History
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            className="flex-1 h-14 rounded-xl justify-center items-center bg-green-500"
            onPress={handleDone}
          >
            <Text className="text-base font-semibold text-white">
              Done
            </Text>
          </TouchableOpacity>
        </View>
      </View>
    </SafeAreaView>
  );
}
