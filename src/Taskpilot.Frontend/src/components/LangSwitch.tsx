import { useTranslation } from 'react-i18next'

/**
 * Small EN/UK language toggle for pages without a navbar (login, register).
 * Persists the choice via the i18next language detector (localStorage).
 */
export default function LangSwitch() {
  const { i18n } = useTranslation()
  const isUk = i18n.language.startsWith('uk')

  return (
    <button
      onClick={() => i18n.changeLanguage(isUk ? 'en' : 'uk')}
      title="Change language"
      className="rounded-lg border border-slate-300 px-2 py-1 text-xs font-bold text-slate-600 hover:bg-slate-100"
    >
      {isUk ? 'УКР' : 'EN'}
    </button>
  )
}
