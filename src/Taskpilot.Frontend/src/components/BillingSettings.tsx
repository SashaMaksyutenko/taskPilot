import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { notify } from '../lib/toast'
import { billingService, type BillingStatus } from '../services/billingService'

/**
 * Admin panel card for the workspace subscription: shows the current plan and project usage, and
 * (when Stripe is configured) an Upgrade-to-Pro checkout or a Manage-billing portal button. Handles
 * the `?billing=success|cancel` return from Stripe Checkout.
 */
export default function BillingSettings() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<BillingStatus | null>(null)
  const [busy, setBusy] = useState(false)

  const loaded = useRef(false)
  useEffect(() => {
    if (loaded.current) return
    loaded.current = true
    billingService.getStatus().then(setStatus).catch(() => {})

    // Coming back from Stripe Checkout.
    const params = new URLSearchParams(window.location.search)
    const billing = params.get('billing')
    if (billing === 'success') {
      notify.success(t('billing.upgraded'))
      // The plan flips via the webhook, which may lag a moment — refetch shortly after.
      setTimeout(() => billingService.getStatus().then(setStatus).catch(() => {}), 1500)
    } else if (billing === 'cancel') {
      notify.error(t('billing.canceled'))
    }
    if (billing) {
      params.delete('billing')
      params.delete('tab')
      const qs = params.toString()
      window.history.replaceState({}, '', window.location.pathname + (qs ? `?${qs}` : ''))
    }
  }, [t])

  const redirect = async (getUrl: () => Promise<string>) => {
    setBusy(true)
    try {
      window.location.href = await getUrl()
    } catch (e) {
      notify.error(apiErrorMessage(e))
      setBusy(false)
    }
  }

  const base = `${window.location.origin}/admin?tab=settings&billing=`
  const upgrade = () => redirect(() => billingService.checkout(`${base}success`, `${base}cancel`))
  const manage = () => redirect(() => billingService.portal(`${window.location.origin}/admin?tab=settings`))

  if (!status) return null

  const isPro = status.plan === 'Pro'
  const limit = status.projectLimit < 0 ? '∞' : String(status.projectLimit)

  return (
    <section className="mb-6 rounded-xl border border-border bg-surface p-5">
      <h2 className="mb-1 font-bold">{t('billing.title')}</h2>
      <p className="mb-3 text-sm text-muted">{t('billing.subtitle')}</p>

      <div className="flex flex-wrap items-center gap-3">
        <span
          className={`rounded-full px-3 py-1 text-sm font-semibold ${
            isPro ? 'bg-primary/10 text-primary' : 'bg-canvas text-muted'
          }`}
        >
          {t('billing.planLabel', { plan: status.plan })}
        </span>
        {isPro && status.renewsAt && (
          <span className="text-xs text-muted">
            {t('billing.renews', { date: new Date(status.renewsAt).toLocaleDateString() })}
          </span>
        )}
        <span className="text-sm text-muted">
          {t('billing.projectsUsage', { count: status.projectCount, limit })}
        </span>
      </div>

      {!status.billingEnabled ? (
        <p className="mt-3 text-xs text-muted">{t('billing.notConfigured')}</p>
      ) : (
        <div className="mt-4 flex flex-wrap gap-2">
          {!isPro && (
            <button
              onClick={upgrade}
              disabled={busy}
              className="rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
            >
              {t('billing.upgrade')}
            </button>
          )}
          {status.canManage && (
            <button
              onClick={manage}
              disabled={busy}
              className="rounded-lg border border-border px-4 py-1.5 text-sm font-medium text-foreground hover:bg-canvas disabled:opacity-50"
            >
              {t('billing.manage')}
            </button>
          )}
        </div>
      )}
    </section>
  )
}
