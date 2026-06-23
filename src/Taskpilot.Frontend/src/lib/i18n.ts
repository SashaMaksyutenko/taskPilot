import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import en from '../locales/en.json'
import uk from '../locales/uk.json'

/**
 * App localization (i18n). English is the default/fallback; Ukrainian is also
 * supported. The chosen language is detected from (and saved to) localStorage so
 * it persists across reloads.
 */
i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      uk: { translation: uk },
    },
    fallbackLng: 'en',
    supportedLngs: ['en', 'uk'],
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
    },
    interpolation: {
      escapeValue: false, // React already escapes values, so this is safe
    },
  })

export default i18n
