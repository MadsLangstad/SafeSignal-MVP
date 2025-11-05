import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  Alert,
  KeyboardAvoidingView,
  Platform,
  ActivityIndicator,
  ScrollView,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { useAuthStore } from '../store/authStore';
import { useTheme } from '../context/ThemeContext';
import { feideAuth, bankIDAuth } from '../services/auth';
import { VALIDATION, ERROR_MESSAGES } from '../constants';
import { SafeSignalLogo } from '../components';

export default function LoginScreen() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);

  const navigation = useNavigation();
  const {
    isLoading,
    error,
    biometricEnabled,
    login,
    loginWithFeide,
    loginWithBankID,
    loginWithBiometric,
    checkBiometricAvailability,
    clearError,
  } = useAuthStore();

  const { isDark } = useTheme();

  useEffect(() => {
    checkBiometricAvailability();
  }, []);

  useEffect(() => {
    if (error) {
      Alert.alert('Authentication Error', error);
      clearError();
    }
  }, [error]);

  const handleEmailLogin = async () => {
    const trimmedEmail = email.trim();
    const trimmedPassword = password.trim();

    if (!trimmedEmail) {
      Alert.alert('Error', 'Please enter your email');
      return;
    }

    if (!VALIDATION.EMAIL_REGEX.test(trimmedEmail)) {
      Alert.alert('Error', 'Please enter a valid email address');
      return;
    }

    if (!trimmedPassword) {
      Alert.alert('Error', 'Please enter your password');
      return;
    }

    if (trimmedPassword.length < VALIDATION.PASSWORD_MIN_LENGTH) {
      Alert.alert(
        'Error',
        `Password must be at least ${VALIDATION.PASSWORD_MIN_LENGTH} characters`
      );
      return;
    }

    const success = await login(trimmedEmail, trimmedPassword);

    if (!success) {
      Alert.alert('Login Failed', ERROR_MESSAGES.AUTH_FAILED);
    }
  };

  const handleFeideLogin = async () => {
    if (!feideAuth.isConfigured()) {
      Alert.alert(
        'Not Configured',
        'Feide authentication is not configured. Please contact your administrator.'
      );
      return;
    }

    await loginWithFeide();
  };

  const handleBankIDLogin = () => {
    // Navigate to BankID screen
    navigation.navigate('BankIDAuth' as never);
  };

  const handleBiometricLogin = async () => {
    const success = await loginWithBiometric();

    if (!success) {
      Alert.alert(
        'Setup Required',
        'Please login with your email and password first to enable biometric authentication.'
      );
    }
  };

  return (
    <SafeAreaView
      className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`}
    >
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        className="flex-1"
      >
        <ScrollView
          contentContainerStyle={{ flexGrow: 1 }}
          keyboardShouldPersistTaps="handled"
        >
          <View className="flex-1 justify-center px-8 py-8">
            {/* Logo/Title */}
            <View className="items-center mb-8">
              <SafeSignalLogo size={100} />
              <Text
                className={`text-4xl font-bold mt-5 ${
                  isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
                }`}
              >
                SafeSignal
              </Text>
              <Text
                className="text-xs font-semibold tracking-widest mt-2"
                style={{ color: '#3B82F6' }}
              >
                EMERGENCY ALERTS
              </Text>
            </View>

            {/* SSO Options */}
            <View className="w-full mb-6">
              {/* Feide Login */}
              {feideAuth.isConfigured() && (
                <TouchableOpacity
                  className={`h-12 rounded-xl justify-center items-center mb-3 flex-row ${
                    isDark ? 'bg-dark-surface border border-gray-700' : 'bg-white border border-gray-200'
                  }`}
                  onPress={handleFeideLogin}
                  disabled={isLoading}
                >
                  <Ionicons
                    name="school-outline"
                    size={20}
                    color={isDark ? '#fff' : '#333'}
                  />
                  <Text
                    className={`text-base font-semibold ml-3 ${
                      isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
                    }`}
                  >
                    Sign in with Feide
                  </Text>
                </TouchableOpacity>
              )}

              {/* BankID Login */}
              <TouchableOpacity
                className={`h-12 rounded-xl justify-center items-center mb-3 flex-row ${
                  isDark ? 'bg-dark-surface border border-gray-700' : 'bg-white border border-gray-200'
                }`}
                onPress={handleBankIDLogin}
                disabled={isLoading}
              >
                <Ionicons
                  name="shield-checkmark-outline"
                  size={20}
                  color={isDark ? '#fff' : '#333'}
                />
                <Text
                  className={`text-base font-semibold ml-3 ${
                    isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
                  }`}
                >
                  Sign in with BankID
                </Text>
              </TouchableOpacity>

              {/* Divider */}
              <View className="flex-row items-center my-6">
                <View
                  className={`flex-1 h-px ${
                    isDark ? 'bg-gray-700' : 'bg-gray-300'
                  }`}
                />
                <Text
                  className={`mx-4 text-sm ${
                    isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'
                  }`}
                >
                  Or continue with email
                </Text>
                <View
                  className={`flex-1 h-px ${
                    isDark ? 'bg-gray-700' : 'bg-gray-300'
                  }`}
                />
              </View>
            </View>

            {/* Email/Password Form */}
            <View className="w-full">
              <View
                className={`flex-row items-center rounded-xl mb-4 px-4 border ${
                  isDark
                    ? 'bg-dark-surface border-gray-700'
                    : 'bg-white border-gray-200'
                }`}
              >
                <Ionicons
                  name="mail-outline"
                  size={20}
                  color={isDark ? '#9CA3AF' : '#666'}
                />
                <TextInput
                  className={`flex-1 h-12 text-base ml-3 ${
                    isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
                  }`}
                  placeholder="Email"
                  placeholderTextColor={isDark ? '#6B7280' : '#999'}
                  value={email}
                  onChangeText={setEmail}
                  keyboardType="email-address"
                  autoCapitalize="none"
                  autoCorrect={false}
                  editable={!isLoading}
                />
              </View>

              <View
                className={`flex-row items-center rounded-xl mb-4 px-4 border ${
                  isDark
                    ? 'bg-dark-surface border-gray-700'
                    : 'bg-white border-gray-200'
                }`}
              >
                <Ionicons
                  name="lock-closed-outline"
                  size={20}
                  color={isDark ? '#9CA3AF' : '#666'}
                />
                <TextInput
                  className={`flex-1 h-12 text-base ml-3 ${
                    isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
                  }`}
                  placeholder="Password"
                  placeholderTextColor={isDark ? '#6B7280' : '#999'}
                  value={password}
                  onChangeText={setPassword}
                  secureTextEntry={!showPassword}
                  editable={!isLoading}
                />
                <TouchableOpacity
                  onPress={() => setShowPassword(!showPassword)}
                  className="p-1"
                >
                  <Ionicons
                    name={showPassword ? 'eye-off-outline' : 'eye-outline'}
                    size={20}
                    color={isDark ? '#9CA3AF' : '#666'}
                  />
                </TouchableOpacity>
              </View>

              <TouchableOpacity
                className={`h-12 rounded-xl justify-center items-center mt-2 ${
                  isLoading
                    ? isDark
                      ? 'bg-gray-600'
                      : 'bg-gray-300'
                    : 'bg-primary'
                }`}
                onPress={handleEmailLogin}
                disabled={isLoading}
              >
                {isLoading ? (
                  <ActivityIndicator color="#fff" />
                ) : (
                  <Text className="text-white text-lg font-semibold">
                    Sign In
                  </Text>
                )}
              </TouchableOpacity>

              {/* Biometric Login */}
              {biometricEnabled && (
                <TouchableOpacity
                  className="flex-row items-center justify-center mt-5 p-4"
                  onPress={handleBiometricLogin}
                  disabled={isLoading}
                >
                  <Ionicons name="finger-print" size={24} color="#3B82F6" />
                  <Text className="text-primary text-base ml-3">
                    Use Biometric Authentication
                  </Text>
                </TouchableOpacity>
              )}
            </View>

            {/* Footer */}
            <Text
              className={`text-center text-xs mt-8 ${
                isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'
              }`}
            >
              For authorized personnel only
            </Text>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
