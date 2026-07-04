import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { fetchMe, googleLogin } from '../store/authSlice'
import { useAppDispatch } from '../store/hooks'
import { notify } from '../lib/toast'

/**
 * Landing route for Google's OAuth redirect. Reads the "?code=" Google appends,
 * exchanges it with the backend, loads the profile and goes home. On failure it
 * returns to the login page with a toast.
 */
export default function GoogleCallbackPage() {
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

    dispatch(googleLogin(code))
      .unwrap()
      .then(() => dispatch(fetchMe()))
      .then(() => navigate('/', { replace: true }))
      .catch((message: string) => {
        notify.error(message || t('auth.googleFailed'))
        navigate('/login', { replace: true })
      })
  }, [dispatch, navigate, searchParams, t])

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 text-slate-500">
      {t('auth.googleSigningIn')}
    </div>
  )
}
