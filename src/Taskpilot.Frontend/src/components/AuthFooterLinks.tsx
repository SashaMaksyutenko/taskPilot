import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'

/** Small "Home · Docs · Pricing" link row shown on the auth pages so guests can reach the public pages. */
export default function AuthFooterLinks() {
  const { t } = useTranslation()
  const cls = 'text-muted transition hover:text-foreground'
  return (
    <nav className="mt-8 flex items-center justify-center gap-4 text-xs">
      <Link to="/" className={cls}>{t('nav.home')}</Link>
      <span className="text-border">·</span>
      <Link to="/docs" className={cls}>{t('docs.nav')}</Link>
      <span className="text-border">·</span>
      <Link to="/pricing" className={cls}>{t('pricing.nav')}</Link>
    </nav>
  )
}
