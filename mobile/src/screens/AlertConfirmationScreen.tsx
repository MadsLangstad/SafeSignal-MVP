import React, { useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import { StackNavigationProp } from '@react-navigation/stack';
import { useTranslation } from 'react-i18next';
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { ALERT_MODES } from '../constants';
import type { RootStackParamList } from '../navigation';

type RouteProps = RouteProp<RootStackParamList, 'AlertConfirmation'>;
type NavigationProps = StackNavigationProp<RootStackParamList>;

export default function AlertConfirmationScreen() {
  const { t } = useTranslation();
  const navigation = useNavigation<NavigationProps>();
  const route = useRoute<RouteProps>();
  const { mode } = route.params;
  const { isDark } = useTheme();

  const { triggerAlert, selectedBuilding, selectedRoomId } = useAppStore();
  const [isTriggering, setIsTriggering] = useState(false);

  const config = ALERT_MODES[mode];
  const modeKey = mode.toLowerCase() as 'audible' | 'silent' | 'medical' | 'fire';

  const handleConfirm = async () => {
    setIsTriggering(true);

    try {
      const alert = await triggerAlert(mode);

      if (alert && selectedBuilding) {
        // Find the room name
        const room = selectedBuilding.rooms.find((r) => r.id === selectedRoomId);
        const roomName = room?.name || 'Unknown Room';

        // Navigate to success screen with alert details
        navigation.navigate('AlertSuccess', {
          alertId: alert.id,
          buildingName: selectedBuilding.name,
          roomName: roomName,
          triggeredAt: alert.triggeredAt.toISOString(), // Serialize Date to string
        });
      } else {
        Alert.alert(
          t('common.error'),
          'Failed to trigger alert. Please try again or contact support.',
          [
            {
              text: 'Retry',
              onPress: handleConfirm,
            },
            {
              text: t('common.cancel'),
              style: 'cancel',
            },
          ]
        );
      }
    } catch (error) {
      console.error('Alert confirmation error:', error);
      Alert.alert(t('common.error'), `An unexpected error occurred: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      setIsTriggering(false);
    }
  };

  const handleCancel = () => {
    navigation.goBack();
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-white'}`}>
      <View className="flex-1 p-5 items-center justify-center">
        {/* Icon */}
        <View
          className="w-36 h-36 rounded-full items-center justify-center mb-8"
          style={{ backgroundColor: config.color + '20' }}
        >
          <Ionicons name="alert-circle" size={80} color={config.color} />
        </View>

        {/* Alert Info */}
        <Text className={`text-3xl font-bold mb-3 text-center ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
          {t(`alertModes.${modeKey}.label`)}
        </Text>
        <Text className={`text-base text-center mb-8 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
          {t(`alertModes.${modeKey}.description`)}
        </Text>

        {/* Location Info */}
        <View className={`w-full rounded-xl p-4 mb-5 ${isDark ? 'bg-dark-surface' : 'bg-gray-50'}`}>
          <View className="flex-row items-center my-2">
            <Ionicons
              name="business"
              size={20}
              color={isDark ? '#9CA3AF' : '#666'}
            />
            <Text className={`ml-3 text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
              {selectedBuilding?.name}
            </Text>
          </View>
          <View className="flex-row items-center my-2">
            <Ionicons
              name="location"
              size={20}
              color={isDark ? '#9CA3AF' : '#666'}
            />
            <Text className={`ml-3 text-base font-medium ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
              {selectedBuilding?.rooms.find((r) => r.id === selectedRoomId)?.name}
            </Text>
          </View>
        </View>

        {/* Warning */}
        <View className={`flex-row rounded-xl p-4 mb-8 items-start ${
          isDark ? 'bg-orange-900/20' : 'bg-orange-50'
        }`}>
          <Ionicons name="warning" size={24} color="#FFA500" />
          <Text className={`flex-1 ml-3 text-sm leading-5 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            {t('alertConfirmation.message', { mode: t(`alertModes.${modeKey}.label`) })}
          </Text>
        </View>

        {/* Actions */}
        <View className="flex-row w-full space-x-3">
          <TouchableOpacity
            className={`flex-1 h-14 rounded-xl justify-center items-center border ${
              isDark
                ? 'bg-dark-surface border-gray-700'
                : 'bg-gray-50 border-gray-200'
            }`}
            onPress={handleCancel}
            disabled={isTriggering}
          >
            <Text className={`text-base font-semibold ${isDark ? 'text-gray-300' : 'text-gray-600'}`}>
              {t('common.cancel')}
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            className={`flex-1 h-14 rounded-xl justify-center items-center flex-row ${
              isTriggering ? 'opacity-60' : ''
            }`}
            style={{ backgroundColor: config.color }}
            onPress={handleConfirm}
            disabled={isTriggering}
          >
            {isTriggering ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <>
                <Ionicons name="alert-circle" size={20} color="#fff" />
                <Text className="text-base font-semibold text-white ml-2">
                  {t('alertConfirmation.sendAlert')}
                </Text>
              </>
            )}
          </TouchableOpacity>
        </View>
      </View>
    </SafeAreaView>
  );
}
