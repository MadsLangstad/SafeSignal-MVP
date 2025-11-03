import React from 'react';
import { View, Text } from 'react-native';
import Svg, { Path } from 'react-native-svg';
import { useTheme } from '../context/ThemeContext';

interface SafeSignalHorizontalLogoProps {
  size?: number;
  showTagline?: boolean;
}

export default function SafeSignalHorizontalLogo({
  size = 40,
  showTagline = true,
}: SafeSignalHorizontalLogoProps) {
  const { isDark } = useTheme();
  const color = '#3B82F6'; // SafeSignal brand blue

  return (
    <View className="flex-row items-center">
      {/* Logo Icon */}
      <Svg
        width={size}
        height={size}
        viewBox="0 0 120 120"
        fill="none"
      >
        {/* Shield Background */}
        <Path
          d="M60 10 L95 25 L95 55 C95 75 85 92 60 105 C35 92 25 75 25 55 L25 25 Z"
          fill={color}
          opacity="0.1"
        />

        {/* Shield Outline */}
        <Path
          d="M60 10 L95 25 L95 55 C95 75 85 92 60 105 C35 92 25 75 25 55 L25 25 Z"
          stroke={color}
          strokeWidth="3"
          fill="none"
        />

        {/* Signal Waves - Inner */}
        <Path
          d="M60 45 L60 75 M50 52 L50 68 M70 52 L70 68"
          stroke={color}
          strokeWidth="4"
          strokeLinecap="round"
        />

        {/* Signal Waves - Outer */}
        <Path
          d="M40 57 L40 63 M80 57 L80 63"
          stroke={color}
          strokeWidth="3.5"
          strokeLinecap="round"
          opacity="0.7"
        />
      </Svg>

      {/* Brand Text */}
      <View className="ml-3">
        <Text
          className={`text-2xl font-bold tracking-tight ${
            isDark ? 'text-dark-text-primary' : 'text-light-text-primary'
          }`}
        >
          SafeSignal
        </Text>
        {showTagline && (
          <Text
            className="text-[10px] font-semibold tracking-widest"
            style={{ color }}
          >
            EMERGENCY ALERTS
          </Text>
        )}
      </View>
    </View>
  );
}
