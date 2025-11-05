import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import * as Localization from 'expo-localization';
import AsyncStorage from '@react-native-async-storage/async-storage';

import en from './locales/en.json';
import nb from './locales/nb.json';

const LANGUAGE_STORAGE_KEY = '@safesignal:language';

const resources = {
  en: { translation: en },
  nb: { translation: nb },
};

// Get stored language or use device language
const getInitialLanguage = async (): Promise<string> => {
  try {
    const storedLanguage = await AsyncStorage.getItem(LANGUAGE_STORAGE_KEY);
    if (storedLanguage && (storedLanguage === 'en' || storedLanguage === 'nb')) {
      return storedLanguage;
    }
  } catch (error) {
    console.error('Error loading stored language:', error);
  }

  // Default to device locale or 'nb' (Norwegian) as fallback
  const deviceLocale = Localization.getLocales()[0]?.languageCode || 'nb';
  return deviceLocale === 'en' ? 'en' : 'nb';
};

export const initI18n = async () => {
  const initialLanguage = await getInitialLanguage();

  await i18n
    .use(initReactI18next)
    .init({
      resources,
      lng: initialLanguage,
      fallbackLng: 'nb',
      interpolation: {
        escapeValue: false,
      },
      compatibilityJSON: 'v3',
    });

  return i18n;
};

export const changeLanguage = async (language: 'en' | 'nb') => {
  try {
    await AsyncStorage.setItem(LANGUAGE_STORAGE_KEY, language);
    await i18n.changeLanguage(language);
  } catch (error) {
    console.error('Error changing language:', error);
  }
};

export const getCurrentLanguage = (): 'en' | 'nb' => {
  return i18n.language as 'en' | 'nb';
};

export default i18n;
