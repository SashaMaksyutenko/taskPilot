import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useAppSelector } from '../store/hooks'

/**
 * Shared header for the public marketing pages (pricing, docs). Auth-aware: guests get
 * Log in / Get started; signed-in users get a Dashboard button instead. The logo returns home.
 */
export default function PublicPageHeader() {
  const { t } = useTranslation()
  const user = useAppSelector((s) => s.auth.user)
  const link = 'text-sm font-semibold text-muted hover:text-foreground'
  const primary = 'rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover'

  return (
    <header className="mx-auto flex max-w-6xl items-center gap-3 px-6 py-5">
      <Link to="/" className="flex items-center gap-3">
        <img src="/logo-mark.svg" alt="" className="h-9 w-9" />
        <span className="text-lg font-bold tracking-tight">TaskPilot</span>
      </Link>
      <div className="ml-auto flex items-center gap-4">
        <Link to="/docs" className={link}>{t('docs.nav')}</Link>
        <Link to="/pricing" className={link}>{t('pricing.nav')}</Link>
        {user ? (
          <Link to="/" className={primary}>{t('nav.dashboard')} →</Link>
        ) : (
          <>
            <Link to="/login" className={link}>{t('landing.login')}</Link>
            <Link to="/register" className={primary}>{t('landing.getStarted')}</Link>
          </>
        )}
      </div>
    </header>
  )
}
