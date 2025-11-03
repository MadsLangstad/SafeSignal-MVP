import React from 'react';
import { TouchableOpacity, Text, ActivityIndicator, ViewStyle, TextStyle } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'success' | 'ghost';
type ButtonSize = 'sm' | 'md' | 'lg';

interface ButtonProps {
  title: string;
  onPress: () => void;
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: keyof typeof Ionicons.glyphMap;
  iconPosition?: 'left' | 'right';
  disabled?: boolean;
  loading?: boolean;
  fullWidth?: boolean;
  className?: string;
}

export default function Button({
  title,
  onPress,
  variant = 'primary',
  size = 'md',
  icon,
  iconPosition = 'left',
  disabled = false,
  loading = false,
  fullWidth = false,
  className = '',
}: ButtonProps) {
  const getVariantClasses = () => {
    const variants = {
      primary: 'bg-primary active:bg-primary-dark',
      secondary: 'bg-white dark:bg-dark-surface border-2 border-primary',
      danger: 'bg-red-500 active:bg-red-600',
      success: 'bg-green-500 active:bg-green-600',
      ghost: 'bg-transparent',
    };
    return variants[variant];
  };

  const getTextVariantClasses = () => {
    const textVariants = {
      primary: 'text-white',
      secondary: 'text-primary',
      danger: 'text-white',
      success: 'text-white',
      ghost: 'text-primary',
    };
    return textVariants[variant];
  };

  const getSizeClasses = () => {
    const sizes = {
      sm: 'h-10 px-4',
      md: 'h-12 px-6',
      lg: 'h-14 px-8',
    };
    return sizes[size];
  };

  const getTextSizeClasses = () => {
    const textSizes = {
      sm: 'text-sm',
      md: 'text-base',
      lg: 'text-lg',
    };
    return textSizes[size];
  };

  const iconSize = size === 'sm' ? 18 : size === 'md' ? 20 : 24;
  const iconColor = variant === 'secondary' || variant === 'ghost' ? '#3B82F6' : '#fff';

  return (
    <TouchableOpacity
      className={`
        rounded-xl justify-center items-center flex-row
        ${getVariantClasses()}
        ${getSizeClasses()}
        ${fullWidth ? 'w-full' : ''}
        ${disabled || loading ? 'opacity-50' : ''}
        ${className}
      `}
      onPress={onPress}
      disabled={disabled || loading}
      activeOpacity={0.7}
    >
      {loading ? (
        <ActivityIndicator color={iconColor} />
      ) : (
        <>
          {icon && iconPosition === 'left' && (
            <Ionicons name={icon} size={iconSize} color={iconColor} style={{ marginRight: 8 }} />
          )}
          <Text className={`font-semibold ${getTextSizeClasses()} ${getTextVariantClasses()}`}>
            {title}
          </Text>
          {icon && iconPosition === 'right' && (
            <Ionicons name={icon} size={iconSize} color={iconColor} style={{ marginLeft: 8 }} />
          )}
        </>
      )}
    </TouchableOpacity>
  );
}
