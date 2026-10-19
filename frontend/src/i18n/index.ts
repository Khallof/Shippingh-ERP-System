import i18n from 'i18next';
import { useCallback } from 'react';
import { initReactI18next, useTranslation } from 'react-i18next';
import ar from './ar';
import en from './en';

export type Language = 'ar' | 'en';

const STORAGE_KEY = 'erp.language';

function storedLanguage(): Language {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'ar';
  } catch {
    return 'ar';
  }
}

export function applyDocumentLanguage(language: Language): void {
  document.documentElement.lang = language;
  document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
}

void i18n.use(initReactI18next).init({
  resources: { ar: { translation: ar }, en: { translation: en } },
  lng: storedLanguage(),
  fallbackLng: 'ar',
  interpolation: { escapeValue: false },
  returnNull: false
});

applyDocumentLanguage(i18n.language as Language);

export function setLanguage(language: Language): void {
  try {
    localStorage.setItem(STORAGE_KEY, language);
  } catch {
    // The language is a convenience: ignore a blocked storage.
  }

  applyDocumentLanguage(language);
  void i18n.changeLanguage(language);
}

/**
 * Page-local text in both languages: tx('رقم التتبع', 'Tracking number'). Shared words live in the dictionaries
 * (t('common.save')); field labels used once stay next to their field.
 */
export function useTx() {
  const { i18n: instance } = useTranslation();
  const isArabic = instance.language !== 'en';
  return useCallback((arabic: string, english: string) => (isArabic ? arabic : english), [isArabic]);
}

export function useLanguage(): Language {
  const { i18n: instance } = useTranslation();
  return instance.language === 'en' ? 'en' : 'ar';
}

export default i18n;
