import { Check, Sparkles } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import PublicPageHeader from '../components/PublicPageHeader'
import { useAppSelector } from '../store/hooks'

/**
 * Public marketing pricing page. The tiers are presentational — they organise TaskPilot's real
 * feature set into plausible plans. Reached from the landing page; no auth required.
 */
const TIERS = [
  {
    key: 'free',
    price: '$0',
    highlighted: false,
    cta: 'ctaFree',
    features: ['projects', 'calendar', 'collaboration', 'forum', 'sso'],
  },
  {
    key: 'pro',
    price: '$9',
    highlighted: true,
    cta: 'ctaPro',
    features: ['everythingFree', 'ai', 'recurring', 'automations', 'marketplace', 'integrations', 'reports'],
  },
  {
    key: 'enterprise',
    price: 'pricing.custom',
    highlighted: false,
    cta: 'ctaEnterprise',
    features: ['everythingPro', 'branding', 'api', 'audit', 'support'],
  },
] as const

export default function PricingPage() {
  const { t } = useTranslation()
  const user = useAppSelector((s) => s.auth.user)
  // A signed-in admin manages the plan in the admin panel; everyone else registers.
  const isAdmin = user?.role === 'Admin'

  return (
    <div className="min-h-screen gradient-hero text-foreground">
      <PublicPageHeader />

      <main className="mx-auto max-w-6xl px-6 pb-20">
        <section className="py-12 text-center lg:py-16">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-primary/20 bg-primary-muted px-3 py-1 text-xs font-semibold text-primary">
            <Sparkles className="h-3.5 w-3.5" />
            {t('pricing.badge')}
          </div>
          <h1 className="text-4xl font-extrabold tracking-tight sm:text-5xl">{t('pricing.title')}</h1>
          <p className="mx-auto mt-4 max-w-xl text-lg text-muted">{t('pricing.subtitle')}</p>
        </section>

        <section className="grid gap-6 lg:grid-cols-3">
          {TIERS.map((tier) => (
            <div
              key={tier.key}
              className={`relative flex flex-col rounded-[var(--radius-card)] border bg-surface p-6 shadow-soft ${
                tier.highlighted ? 'border-primary shadow-elevated ring-1 ring-primary/30' : 'border-border'
              }`}
            >
              {tier.highlighted && (
                <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-primary px-3 py-0.5 text-xs font-bold text-white">
                  {t('pricing.popular')}
                </span>
              )}
              <h2 className="text-lg font-bold">{t(`pricing.tier.${tier.key}`)}</h2>
              <p className="mt-1 text-sm text-muted">{t(`pricing.tagline.${tier.key}`)}</p>
              <div className="mt-4 flex items-baseline gap-1">
                <span className="text-4xl font-extrabold tracking-tight">
                  {tier.price.startsWith('pricing.') ? t(tier.price) : tier.price}
                </span>
                {!tier.price.startsWith('pricing.') && tier.price !== '$0' && (
                  <span className="text-sm text-muted">{t('pricing.perUserMonth')}</span>
                )}
              </div>

              <Link
                to={tier.key === 'pro' && isAdmin ? '/admin?tab=settings' : '/register'}
                className={`mt-6 rounded-lg px-4 py-2.5 text-center text-sm font-semibold transition ${
                  tier.highlighted
                    ? 'bg-primary text-white hover:bg-primary-hover'
                    : 'border border-border text-foreground hover:bg-canvas'
                }`}
              >
                {t(`pricing.${tier.cta}`)}
              </Link>

              <ul className="mt-6 space-y-2.5">
                {tier.features.map((f) => (
                  <li key={f} className="flex items-start gap-2 text-sm">
                    <Check className="mt-0.5 h-4 w-4 flex-none text-primary" />
                    <span>{t(`pricing.feature.${f}`)}</span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </section>

        <p className="mt-10 text-center text-sm text-muted">{t('pricing.footnote')}</p>
      </main>
    </div>
  )
}
