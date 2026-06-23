import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import en from '../locales/en.json'
import uk from '../locales/uk.json'

// Key under which the chosen language is persisted (same key the detector reads).
const STORAGE_KEY = 'i18nextLng'

// Read the previously chosen language (if any) so we can apply it explicitly.
// We persist it ourselves rather than rely on the detector's auto-cache, which is
// unreliable across the installed i18next/detector major versions.
const savedLng = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null

/**
 * App localization (i18n). English is the default/fallback; Ukrainian is also
 * supported. The chosen language is saved to localStorage and re-applied on load,
 * so it persists across reloads.
 */
i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    // If the user previously picked a language, apply it explicitly; otherwise let
    // the detector fall back to the browser language on the very first visit.
    ...(savedLng ? { lng: savedLng } : {}),
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

// Persist the language on every change so a reload restores the user's choice.
i18n.on('languageChanged', (lng) => {
  try {
    localStorage.setItem(STORAGE_KEY, lng)
  } catch {
    // Ignore storage failures (e.g. private mode); language still works for the session.
  }
})

export default i18n
