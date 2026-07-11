import { useTranslation } from 'react-i18next'
import GoogleSignInButton from './GoogleSignInButton'
import GitHubSignInButton from './GitHubSignInButton'
import LinkedInSignInButton from './LinkedInSignInButton'

/**
 * Groups the third-party sign-in buttons under a single "or" divider. Renders
 * nothing when no OAuth provider is configured, so the divider never shows alone.
 */
export default function SocialSignIn() {
  const { t } = useTranslation()
  const anyConfigured =
    import.meta.env.VITE_GOOGLE_CLIENT_ID ||
    import.meta.env.VITE_GITHUB_CLIENT_ID ||
    import.meta.env.VITE_LINKEDIN_CLIENT_ID
  if (!anyConfigured) return null

  return (
    <>
      <div className="my-5 flex items-center gap-3 text-xs text-muted">
        <span className="h-px flex-1 bg-border" />
        {t('auth.or')}
        <span className="h-px flex-1 bg-border" />
      </div>
      <div className="space-y-2">
        <GoogleSignInButton />
        <GitHubSignInButton />
        <LinkedInSignInButton />
      </div>
    </>
  )
}
