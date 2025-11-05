import { format as dateFnsFormat } from 'date-fns';
import { nb, enUS } from 'date-fns/locale';
import { getCurrentLanguage } from '../i18n';

// Get the appropriate locale for date-fns based on current language
export const getDateLocale = () => {
  const currentLang = getCurrentLanguage();
  return currentLang === 'nb' ? nb : enUS;
};

// Format date with current locale
export const formatDate = (date: Date | string, formatStr: string = 'PPP'): string => {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  return dateFnsFormat(dateObj, formatStr, { locale: getDateLocale() });
};

// Format time with current locale
export const formatTime = (date: Date | string, formatStr: string = 'p'): string => {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  return dateFnsFormat(dateObj, formatStr, { locale: getDateLocale() });
};

// Format date and time with current locale
export const formatDateTime = (date: Date | string, formatStr: string = 'PPp'): string => {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  return dateFnsFormat(dateObj, formatStr, { locale: getDateLocale() });
};

// Locale-aware relative time (e.g., "2 hours ago")
export const formatRelativeTime = (date: Date | string): string => {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  const now = new Date();
  const diffInSeconds = Math.floor((now.getTime() - dateObj.getTime()) / 1000);

  const currentLang = getCurrentLanguage();
  const isNorwegian = currentLang === 'nb';

  if (diffInSeconds < 60) {
    return isNorwegian ? 'nå nettopp' : 'just now';
  } else if (diffInSeconds < 3600) {
    const minutes = Math.floor(diffInSeconds / 60);
    return isNorwegian
      ? `${minutes} ${minutes === 1 ? 'minutt' : 'minutter'} siden`
      : `${minutes} ${minutes === 1 ? 'minute' : 'minutes'} ago`;
  } else if (diffInSeconds < 86400) {
    const hours = Math.floor(diffInSeconds / 3600);
    return isNorwegian
      ? `${hours} ${hours === 1 ? 'time' : 'timer'} siden`
      : `${hours} ${hours === 1 ? 'hour' : 'hours'} ago`;
  } else {
    const days = Math.floor(diffInSeconds / 86400);
    return isNorwegian
      ? `${days} ${days === 1 ? 'dag' : 'dager'} siden`
      : `${days} ${days === 1 ? 'day' : 'days'} ago`;
  }
};
