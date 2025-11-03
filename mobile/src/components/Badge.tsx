import React from 'react';
import { View, Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

type BadgeVariant = 'success' | 'warning' | 'error' | 'info' | 'neutral';
type BadgeSize = 'sm' | 'md' | 'lg';

interface BadgeProps {
  label: string;
  variant?: BadgeVariant;
  size?: BadgeSize;
  icon?: keyof typeof Ionicons.glyphMap;
  dot?: boolean;
  className?: string;
}

export default function Badge({
  label,
  variant = 'neutral',
  size = 'md',
  icon,
  dot = false,
  className = '',
}: BadgeProps) {
  const getVariantClasses = () => {
    const variants = {
      success: 'bg-green-50 dark:bg-green-900/20',
      warning: 'bg-orange-50 dark:bg-orange-900/20',
      error: 'bg-red-50 dark:bg-red-900/20',
      info: 'bg-blue-50 dark:bg-blue-900/20',
      neutral: 'bg-gray-50 dark:bg-gray-800',
    };
    return variants[variant];
  };

  const getTextVariantClasses = () => {
    const textVariants = {
      success: 'text-green-700 dark:text-green-400',
      warning: 'text-orange-700 dark:text-orange-400',
      error: 'text-red-700 dark:text-red-400',
      info: 'text-blue-700 dark:text-blue-400',
      neutral: 'text-gray-700 dark:text-gray-300',
    };
    return textVariants[variant];
  };

  const getDotColor = () => {
    const dotColors = {
      success: '#4CAF50',
      warning: '#FFA500',
      error: '#F44336',
      info: '#2196F3',
      neutral: '#9E9E9E',
    };
    return dotColors[variant];
  };

  const getSizeClasses = () => {
    const sizes = {
      sm: 'px-2 py-0.5',
      md: 'px-2.5 py-1',
      lg: 'px-3 py-1.5',
    };
    return sizes[size];
  };

  const getTextSizeClasses = () => {
    const textSizes = {
      sm: 'text-xs',
      md: 'text-sm',
      lg: 'text-base',
    };
    return textSizes[size];
  };

  const iconSize = size === 'sm' ? 12 : size === 'md' ? 14 : 16;
  const iconColor = variant === 'success' ? '#4CAF50' :
                    variant === 'warning' ? '#FFA500' :
                    variant === 'error' ? '#F44336' :
                    variant === 'info' ? '#2196F3' : '#9E9E9E';

  return (
    <View
      className={`
        flex-row items-center rounded-full
        ${getVariantClasses()}
        ${getSizeClasses()}
        ${className}
      `}
    >
      {dot && (
        <View
          className="w-2 h-2 rounded-full mr-1.5"
          style={{ backgroundColor: getDotColor() }}
        />
      )}
      {icon && (
        <Ionicons
          name={icon}
          size={iconSize}
          color={iconColor}
          style={{ marginRight: 4 }}
        />
      )}
      <Text className={`font-semibold uppercase ${getTextSizeClasses()} ${getTextVariantClasses()}`}>
        {label}
      </Text>
    </View>
  );
}
