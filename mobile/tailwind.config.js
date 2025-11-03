/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './App.{js,jsx,ts,tsx}',
    './src/**/*.{js,jsx,ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        // Primary Brand Colors - SafeSignal Blue
        primary: {
          DEFAULT: '#3B82F6', // SafeSignal Trust Blue
          light: '#DBEAFE',
          dark: '#1E40AF',
          red: '#DC2626', // Emergency Red
        },
        // Brand Accent Colors
        brand: {
          orange: '#FF6B35', // SafeSignal Orange
          green: '#10B981', // Safety Green
          navy: '#1E293B', // Professional Navy
        },
        // Light Mode Colors
        'light-background': '#FAFAFA',
        'light-surface': '#FFFFFF',
        'light-text-primary': '#1E293B',
        'light-text-secondary': '#64748B',
        // Dark Mode Colors
        'dark-background': '#0F172A', // Deep Navy
        'dark-surface': '#1E293B',
        'dark-text-primary': '#F8FAFC',
        'dark-text-secondary': '#CBD5E1',
        // Semantic Colors
        success: '#10B981',
        warning: '#F59E0B',
        error: '#EF4444',
        info: '#3B82F6',
      },
    },
  },
  plugins: [],
};
