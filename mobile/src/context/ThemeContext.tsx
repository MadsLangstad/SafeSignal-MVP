import React, { createContext, useContext, useEffect } from 'react';
import { useColorScheme } from 'react-native';
import { useAppStore } from '../store';

export type ThemeMode = 'light' | 'dark' | 'system';
export type ColorScheme = 'light' | 'dark';

interface ThemeContextType {
  theme: ThemeMode;
  colorScheme: ColorScheme;
  setTheme: (theme: ThemeMode) => void;
  isDark: boolean;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const systemColorScheme = useColorScheme() as ColorScheme;
  const { theme, setTheme } = useAppStore();

  // Determine the actual color scheme based on theme setting
  const colorScheme: ColorScheme =
    theme === 'system' ? systemColorScheme || 'light' : theme;

  const isDark = colorScheme === 'dark';

  // Update NativeWind's color scheme
  useEffect(() => {
    if (global.document) {
      global.document.documentElement.classList.remove('light', 'dark');
      global.document.documentElement.classList.add(colorScheme);
    }
  }, [colorScheme]);

  return (
    <ThemeContext.Provider value={{ theme, colorScheme, setTheme, isDark }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}
