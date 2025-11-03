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
  Image,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { authService } from '../services/auth';
import { secureStorage } from '../services/secureStorage';
import { VALIDATION, ERROR_MESSAGES } from '../constants';
import { SafeSignalLogo } from '../components';

export default function LoginScreen() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [biometricAvailable, setBiometricAvailable] = useState(false);
  const [biometricEnabled, setBiometricEnabled] = useState(false);

  const { login, isLoading } = useAppStore();
  const { isDark } = useTheme();

  useEffect(() => {
    checkBiometric();
  }, []);

  const checkBiometric = async () => {
    const { available } = await authService.checkBiometricAvailability();
    setBiometricAvailable(available);

    if (available) {
      const enabled = await authService.isBiometricEnabled();
      setBiometricEnabled(enabled);
    }
  };

  const handleLogin = async () => {
    // Validation
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

    // Attempt login with trimmed values
    const success = await login(trimmedEmail, trimmedPassword);

    if (!success) {
      Alert.alert('Login Failed', ERROR_MESSAGES.AUTH_FAILED);
    }
  };

  const handleBiometricLogin = async () => {
    const result = await authService.authenticateWithBiometric();

    if (result.success) {
      // Biometric authentication successful, load stored tokens/credentials
      const tokens = await secureStorage.getTokens();
      const user = await secureStorage.getUser();

      if (tokens && user) {
        // Tokens exist, validate and auto-login
        // The auth context will handle token refresh if needed
        await login(user.email, ''); // Pass empty password since we're using stored tokens
      } else {
        // No stored credentials, fall back to manual login
        Alert.alert('Setup Required', 'Please login with your email and password first');
      }
    } else {
      Alert.alert('Authentication Failed', result.error || 'Please try again');
    }
  };

  return (
    <SafeAreaView className={`flex-1 ${isDark ? 'bg-dark-background' : 'bg-light-background'}`}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        className="flex-1"
      >
        <View className="flex-1 justify-center px-8">
          {/* Logo/Title */}
          <View className="items-center mb-12">
            <SafeSignalLogo size={100} />
            <Text className={`text-4xl font-bold mt-5 ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}>
              SafeSignal
            </Text>
            <Text className="text-xs font-semibold tracking-widest mt-2" style={{ color: '#3B82F6' }}>
              EMERGENCY ALERTS
            </Text>
          </View>

          {/* Login Form */}
          <View className="w-full">
            <View className={`flex-row items-center rounded-xl mb-4 px-4 border ${
              isDark
                ? 'bg-dark-surface border-gray-700'
                : 'bg-white border-gray-200'
            }`}>
              <Ionicons
                name="mail-outline"
                size={20}
                color={isDark ? '#9CA3AF' : '#666'}
                className="mr-3"
              />
              <TextInput
                className={`flex-1 h-12 text-base ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}
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

            <View className={`flex-row items-center rounded-xl mb-4 px-4 border ${
              isDark
                ? 'bg-dark-surface border-gray-700'
                : 'bg-white border-gray-200'
            }`}>
              <Ionicons
                name="lock-closed-outline"
                size={20}
                color={isDark ? '#9CA3AF' : '#666'}
                className="mr-3"
              />
              <TextInput
                className={`flex-1 h-12 text-base ${isDark ? 'text-dark-text-primary' : 'text-light-text-primary'}`}
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
                  ? (isDark ? 'bg-gray-600' : 'bg-gray-300')
                  : 'bg-primary'
              }`}
              onPress={handleLogin}
              disabled={isLoading}
            >
              {isLoading ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text className="text-white text-lg font-semibold">Sign In</Text>
              )}
            </TouchableOpacity>

            {/* Biometric Login */}
            {biometricAvailable && biometricEnabled && (
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
          <Text className={`text-center text-xs mt-8 ${isDark ? 'text-dark-text-secondary' : 'text-light-text-secondary'}`}>
            For authorized personnel only
          </Text>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
