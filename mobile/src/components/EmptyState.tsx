import React from 'react';
import { View, Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '../context/ThemeContext';
import Button from './Button';

interface EmptyStateProps {
  icon?: keyof typeof Ionicons.glyphMap;
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
  className?: string;
}

export default function EmptyState({
  icon = 'folder-open-outline',
  title,
  description,
  actionLabel,
  onAction,
  className = '',
}: EmptyStateProps) {
  const { isDark } = useTheme();

  return (
    <View className={`flex-1 justify-center items-center p-10 ${className}`}>
      <Ionicons
        name={icon}
        size={80}
        color={isDark ? '#4B5563' : '#D1D5DB'}
      />
      <Text className="text-xl font-semibold text-gray-400 dark:text-gray-500 mt-5 text-center">
        {title}
      </Text>
      {description && (
        <Text className="text-sm text-gray-300 dark:text-gray-600 mt-2 text-center">
          {description}
        </Text>
      )}
      {actionLabel && onAction && (
        <View className="mt-6">
          <Button
            title={actionLabel}
            onPress={onAction}
            variant="primary"
            size="md"
          />
        </View>
      )}
    </View>
  );
}
