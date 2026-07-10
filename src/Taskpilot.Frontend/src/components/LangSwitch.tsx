import { useTranslation } from 'react-i18next'
import { cn } from '../lib/cn'

/** Small EN/UK language toggle. */
export default function LangSwitch() {
  const { i18n } = useTranslation()
  const isUk = i18n.language.startsWith('uk')

  return (
    <button
      type="button"
      onClick={() => i18n.changeLanguage(isUk ? 'en' : 'uk')}
      title="Change language"
      className={cn(
        'rounded-lg border border-border px-2.5 py-1 text-xs font-bold text-muted transition hover:bg-canvas hover:text-foreground',
      )}
    >
      {isUk ? 'УКР' : 'EN'}
    </button>
  )
}
