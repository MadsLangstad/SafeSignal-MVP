import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  Platform,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import QRCode from 'react-native-qrcode-svg';
import { useAuthStore } from '../store/authStore';
import { useTheme } from '../context/ThemeContext';

export default function BankIDAuthScreen() {
  const [showManualEntry, setShowManualEntry] = useState(false);
  const [personalNumber, setPersonalNumber] = useState('');
  const pollingInterval = useRef<NodeJS.Timeout | null>(null);

  const navigation = useNavigation();
  const {
    isLoading,
    ssoSession,
    loginWithBankID,
    pollBankIDStatus,
    clearSSOSession,
  } = useAuthStore();

  const { isDark } = useTheme();

  useEffect(() => {
    // Auto-initiate BankID on mount
    initiateBankID();

    return () => {
      // Cleanup polling on unmount
      if (pollingInterval.current) {
        clearInterval(pollingInterval.current);
      }
    };
  }, []);

  useEffect(() => {
    // Start polling when session is initiated
    if (ssoSession?.provider === 'bankid' && ssoSession.status === 'pending') {
      startPolling();
    }

    // Handle completion or failure
    if (ssoSession?.status === 'completed') {
      stopPolling();
      // AuthStore will handle navigation via isAuthenticated state
    } else if (ssoSession?.status === 'failed') {
      stopPolling();
      Alert.alert(
        'Authentication Failed',
        ssoSession.error || 'BankID authentication failed. Please try again.',
        [
          {
            text: 'Try Again',
            onPress: () => initiateBankID(),
          },
          {
            text: 'Cancel',
            onPress: () => handleCancel(),
            style: 'cancel',
          },
        ]
      );
    }
  }, [ssoSession]);

  const initiateBankID = async () => {
    const success = await loginWithBankID(
      showManualEntry ? personalNumber : undefined
    );

    if (!success) {
      Alert.alert(
        'Error',
        'Failed to initiate BankID authentication. Please try again.'
      );
    }
  };

  const startPolling = () => {
    if (pollingInterval.current) {
      clearInterval(pollingInterval.current);
    }

    pollingInterval.current = setInterval(async () => {
      await pollBankIDStatus();
    }, 2000); // Poll every 2 seconds
  };

  const stopPolling = () => {
    if (pollingInterval.current) {
      clearInterval(pollingInterval.current);
      pollingInterval.current = null;
    }
  };

  const handleCancel = () => {
    stopPolling();
    clearSSOSession();
    navigation.goBack();
  };

  const getStatusMessage = (): string => {
    if (!ssoSession || ssoSession.provider !== 'bankid') {
      return 'Initializing BankID...';
    }

    const hintCode = ssoSession.bankid?.hintCode;

    if (hintCode) {
      const messages: Record<string, string> = {
        outstandingTransaction: 'Open the BankID app on your device',
        noClient: 'BankID app is not installed',
        started: 'Signing started in BankID app',
        userSign: 'Enter your security code in the BankID app',
        expiredTransaction: 'BankID session has expired',
        certificateErr: 'BankID certificate is invalid',
        userCancel: 'Authentication cancelled',
        cancelled: 'Authentication cancelled',
        startFailed: 'Could not start BankID app',
      };

      return messages[hintCode] || 'Processing authentication...';
    }

    if (ssoSession.status === 'pending') {
      return Platform.OS === 'web'
        ? 'Scan the QR code with the BankID app'
        : 'Open the BankID app on this device';
    }

    return 'Processing...';
  };

  return (
    <SafeAreaView
      className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`}
    >
      <View className="flex-1 px-6 py-8">
        {/* Header */}
        <View className="flex-row items-center mb-8">
          <TouchableOpacity
            onPress={handleCancel}
            className="mr-4 p-2"
            disabled={isLoading}
          >
            <Ionicons
              name="arrow-back"
              size={24}
              color={isDark ? '#fff' : '#000'}
            />
          </TouchableOpacity>
          <Text
            className={`text-2xl font-bold ${
              isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
            }`}
          >
            BankID Authentication
          </Text>
        </View>

        {/* Content */}
        <View className="flex-1 justify-center items-center">
          {isLoading && !ssoSession ? (
            <ActivityIndicator size="large" color="#3B82F6" />
          ) : (
            <>
              {/* QR Code */}
              {ssoSession?.bankid?.qrCodeData && Platform.OS === 'web' && (
                <View
                  className={`p-6 rounded-2xl mb-8 ${
                    isDark ? 'bg-dark-surface' : 'bg-white'
                  }`}
                  style={{
                    shadowColor: '#000',
                    shadowOffset: { width: 0, height: 2 },
                    shadowOpacity: 0.1,
                    shadowRadius: 8,
                    elevation: 5,
                  }}
                >
                  <QRCode
                    value={ssoSession.bankid.qrCodeData}
                    size={250}
                    backgroundColor="white"
                    color="black"
                  />
                </View>
              )}

              {/* BankID Logo/Icon */}
              {Platform.OS !== 'web' && (
                <View
                  className={`w-32 h-32 rounded-full items-center justify-center mb-8 ${
                    isDark ? 'bg-dark-surface' : 'bg-white'
                  }`}
                >
                  <Ionicons
                    name="shield-checkmark"
                    size={64}
                    color="#3B82F6"
                  />
                </View>
              )}

              {/* Status Message */}
              <Text
                className={`text-lg text-center mb-4 px-8 ${
                  isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
                }`}
              >
                {getStatusMessage()}
              </Text>

              {/* Hint Text */}
              {ssoSession?.status === 'pending' && (
                <View className="items-center px-8">
                  {Platform.OS === 'web' ? (
                    <Text
                      className={`text-sm text-center ${
                        isDark
                          ? 'text-dark-text-secondary'
                          : 'text-light-text-secondary'
                      }`}
                    >
                      Open the BankID app on your mobile device and scan the QR
                      code above
                    </Text>
                  ) : (
                    <>
                      <Text
                        className={`text-sm text-center mb-4 ${
                          isDark
                            ? 'text-dark-text-secondary'
                            : 'text-light-text-secondary'
                        }`}
                      >
                        The BankID app should open automatically. If not, tap
                        the button below.
                      </Text>
                      <TouchableOpacity
                        className="bg-primary px-6 py-3 rounded-xl"
                        onPress={() => initiateBankID()}
                      >
                        <Text className="text-white font-semibold">
                          Open BankID
                        </Text>
                      </TouchableOpacity>
                    </>
                  )}
                </View>
              )}

              {/* Loading Indicator */}
              {ssoSession?.status === 'pending' && (
                <View className="mt-8">
                  <ActivityIndicator size="small" color="#3B82F6" />
                </View>
              )}
            </>
          )}
        </View>

        {/* Footer */}
        <View className="items-center">
          <TouchableOpacity onPress={handleCancel} className="py-3">
            <Text className="text-primary text-base font-semibold">
              Cancel Authentication
            </Text>
          </TouchableOpacity>

          <Text
            className={`text-xs text-center mt-4 ${
              isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'
            }`}
          >
            BankID authentication is secure and verified
          </Text>
        </View>
      </View>
    </SafeAreaView>
  );
}
