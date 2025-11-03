import React from 'react';
import { View, ActivityIndicator, Text } from 'react-native';

interface LoadingSpinnerProps {
  size?: 'small' | 'large';
  color?: string;
  text?: string;
  fullScreen?: boolean;
  className?: string;
}

export default function LoadingSpinner({
  size = 'large',
  color = '#3B82F6',
  text,
  fullScreen = false,
  className = '',
}: LoadingSpinnerProps) {
  const content = (
    <View className={`items-center justify-center ${className}`}>
      <ActivityIndicator size={size} color={color} />
      {text && (
        <Text className="text-sm text-light-text-secondary dark:text-dark-text-secondary mt-3">
          {text}
        </Text>
      )}
    </View>
  );

  if (fullScreen) {
    return (
      <View className="flex-1 items-center justify-center bg-light-background dark:bg-dark-background">
        {content}
      </View>
    );
  }

  return content;
}
