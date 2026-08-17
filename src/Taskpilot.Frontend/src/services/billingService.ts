import api from '../lib/api'

/** The workspace's plan and limits (mirrors BillingStatusDto). */
export interface BillingStatus {
  plan: string
  billingEnabled: boolean
  projectLimit: number
  projectCount: number
  renewsAt: string | null
  canManage: boolean
}

export const billingService = {
  getStatus(): Promise<BillingStatus> {
    return api.get<BillingStatus>('/api/billing/status').then((r) => r.data)
  },

  /** Starts a Pro checkout (admin); returns the hosted Stripe URL to redirect to. */
  checkout(successUrl: string, cancelUrl: string): Promise<string> {
    return api.post<{ url: string }>('/api/billing/checkout', { successUrl, cancelUrl }).then((r) => r.data.url)
  },

  /** Opens the billing portal (admin); returns the hosted Stripe URL. */
  portal(returnUrl: string): Promise<string> {
    return api.post<{ url: string }>('/api/billing/portal', { successUrl: returnUrl, returnUrl }).then((r) => r.data.url)
  },
}
