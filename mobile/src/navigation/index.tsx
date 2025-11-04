import React from 'react';
import { View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createStackNavigator } from '@react-navigation/stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';

// Import screens (we'll create these next)
import LoginScreen from '../screens/LoginScreen';
import HomeScreen from '../screens/HomeScreen';
import AlertHistoryScreen from '../screens/AlertHistoryScreen';
import AlertClearanceScreen from '../screens/AlertClearanceScreen';
import SettingsScreen from '../screens/SettingsScreen';
import AlertConfirmationScreen from '../screens/AlertConfirmationScreen';
import AlertSuccessScreen from '../screens/AlertSuccessScreen';

import { useAppStore } from '../store';
import { useTheme } from '../context/ThemeContext';
import { SafeSignalHorizontalLogo } from '../components';

export type RootStackParamList = {
  Auth: undefined;
  Main: undefined;
  AlertConfirmation: { mode: 'SILENT' | 'AUDIBLE' | 'LOCKDOWN' | 'EVACUATION' };
  AlertSuccess: {
    alertId: string;
    buildingName: string;
    roomName: string;
    triggeredAt: string; // ISO string, not Date object (for serialization)
  };
  AlertHistory: undefined;
  AlertClearance: { alertId: string; alert?: any };
};

export type MainTabParamList = {
  Home: undefined;
  History: undefined;
  Settings: undefined;
};

const RootStack = createStackNavigator<RootStackParamList>();
const MainTab = createBottomTabNavigator<MainTabParamList>();

function MainTabs() {
  const { isDark } = useTheme();

  return (
    <MainTab.Navigator
      screenOptions={({ route }) => ({
        tabBarIcon: ({ focused, color, size }) => {
          let iconName: keyof typeof Ionicons.glyphMap;

          if (route.name === 'Home') {
            iconName = focused ? 'home' : 'home-outline';
          } else if (route.name === 'History') {
            iconName = focused ? 'list' : 'list-outline';
          } else if (route.name === 'Settings') {
            iconName = focused ? 'settings' : 'settings-outline';
          } else {
            iconName = 'alert-circle-outline';
          }

          return <Ionicons name={iconName} size={size} color={color} />;
        },
        tabBarActiveTintColor: '#3B82F6',
        tabBarInactiveTintColor: 'gray',
        tabBarStyle: {
          backgroundColor: isDark ? '#1E293B' : '#FFFFFF',
          borderTopColor: isDark ? '#374151' : '#E5E7EB',
        },
        headerShown: false as boolean,
      })}
    >
      <MainTab.Screen name="Home" component={HomeScreen} />
      <MainTab.Screen name="History" component={AlertHistoryScreen} />
      <MainTab.Screen name="Settings" component={SettingsScreen} />
    </MainTab.Navigator>
  );
}

export function RootNavigator() {
  const isAuthenticated = useAppStore((state) => state.isAuthenticated);

  return (
    <NavigationContainer>
      <RootStack.Navigator screenOptions={{ headerShown: false as boolean }}>
        {!isAuthenticated ? (
          <RootStack.Screen name="Auth" component={LoginScreen} />
        ) : (
          <>
            <RootStack.Screen name="Main" component={MainTabs} />
            <RootStack.Screen
              name="AlertConfirmation"
              component={AlertConfirmationScreen}
              options={{
                presentation: 'modal',
                headerShown: true as boolean,
                title: 'Confirm Emergency Alert',
              }}
            />
            <RootStack.Screen
              name="AlertSuccess"
              component={AlertSuccessScreen}
              options={{
                presentation: 'modal',
                headerShown: false as boolean,
              }}
            />
            <RootStack.Screen
              name="AlertHistory"
              component={AlertHistoryScreen}
              options={{
                headerShown: false as boolean,
              }}
            />
            <RootStack.Screen
              name="AlertClearance"
              component={AlertClearanceScreen}
              options={{
                headerShown: false as boolean,
              }}
            />
          </>
        )}
      </RootStack.Navigator>
    </NavigationContainer>
  );
}
