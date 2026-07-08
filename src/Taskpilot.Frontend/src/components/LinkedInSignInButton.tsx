import { useTranslation } from 'react-i18next'

/**
 * "Continue with LinkedIn" button. Renders only when a LinkedIn client id is
 * configured (VITE_LINKEDIN_CLIENT_ID); clicking it redirects to LinkedIn's consent
 * screen, which returns to /auth/linkedin/callback with an authorization code.
 */
export default function LinkedInSignInButton() {
  const { t } = useTranslation()
  const linkedInClientId = import.meta.env.VITE_LINKEDIN_CLIENT_ID as string | undefined

  if (!linkedInClientId) return null

  const startLinkedIn = () => {
    const redirectUri = `${window.location.origin}/auth/linkedin/callback`
    const params = new URLSearchParams({
      response_type: 'code',
      client_id: linkedInClientId,
      redirect_uri: redirectUri,
      scope: 'openid profile email',
    })
    window.location.href = `https://www.linkedin.com/oauth/v2/authorization?${params.toString()}`
  }

  return (
    <button
      type="button"
      onClick={startLinkedIn}
      className="flex w-full items-center justify-center gap-2 rounded-lg border border-slate-300 py-2.5 font-semibold text-slate-700 transition hover:bg-slate-50"
    >
      <svg width="18" height="18" viewBox="0 0 24 24" fill="#0A66C2" aria-hidden="true">
        <path d="M20.45 20.45h-3.56v-5.57c0-1.33-.02-3.04-1.85-3.04-1.85 0-2.13 1.45-2.13 2.94v5.67H9.35V9h3.42v1.56h.05c.48-.9 1.64-1.85 3.37-1.85 3.6 0 4.27 2.37 4.27 5.46v6.28zM5.34 7.43a2.07 2.07 0 1 1 0-4.14 2.07 2.07 0 0 1 0 4.14zM7.12 20.45H3.55V9h3.57v11.45zM22.22 0H1.77C.79 0 0 .77 0 1.72v20.56C0 23.23.79 24 1.77 24h20.45c.98 0 1.78-.77 1.78-1.72V1.72C24 .77 23.2 0 22.22 0z" />
      </svg>
      {t('auth.linkedinContinue')}
    </button>
  )
}
