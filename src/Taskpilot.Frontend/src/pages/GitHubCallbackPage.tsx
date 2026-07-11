import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { fetchMe, githubLogin } from '../store/authSlice'
import { useAppDispatch } from '../store/hooks'
import { notify } from '../lib/toast'

/**
 * Landing route for GitHub's OAuth redirect. Reads the "?code=" GitHub appends,
 * exchanges it with the backend, loads the profile and goes home. On failure it
 * returns to the login page with a toast.
 */
export default function GitHubCallbackPage() {
  const { t } = useTranslation()
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  // Guard against the effect running twice (React StrictMode double-invoke).
  const handled = useRef(false)

  useEffect(() => {
    if (handled.current) return
    handled.current = true

    const code = searchParams.get('code')
    if (!code) {
      navigate('/login', { replace: true })
      return
    }

    dispatch(githubLogin(code))
      .unwrap()
      .then(() => dispatch(fetchMe()))
      .then(() => navigate('/', { replace: true }))
      .catch((message: string) => {
        notify.error(message || t('auth.githubFailed'))
        navigate('/login', { replace: true })
      })
  }, [dispatch, navigate, searchParams, t])

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas text-muted">
      {t('auth.githubSigningIn')}
    </div>
  )
}
