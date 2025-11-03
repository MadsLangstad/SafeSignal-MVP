import React from 'react';
import { View, ViewStyle } from 'react-native';

interface CardProps {
  children: React.ReactNode;
  className?: string;
  padding?: 'none' | 'sm' | 'md' | 'lg';
  elevated?: boolean;
}

export default function Card({
  children,
  className = '',
  padding = 'md',
  elevated = true,
}: CardProps) {
  const getPaddingClasses = () => {
    const paddings = {
      none: '',
      sm: 'p-3',
      md: 'p-4',
      lg: 'p-6',
    };
    return paddings[padding];
  };

  return (
    <View
      className={`
        bg-white dark:bg-dark-surface
        rounded-xl
        ${getPaddingClasses()}
        ${elevated ? 'shadow-sm' : ''}
        ${className}
      `}
    >
      {children}
    </View>
  );
}
