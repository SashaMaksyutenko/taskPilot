import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import LangSwitch from '../components/LangSwitch'
import { fetchMe, login } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

// Login validation only checks the fields are present/well-formed.
const schema = z.object({
  email: z.email('Invalid email address'),
  password: z.string().min(1, 'Password is required'),
})

type FormValues = z.infer<typeof schema>

/**
 * Login page: dispatches the login thunk, loads the user profile on success,
 * then navigates to the home page.
 */
export default function LoginPage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { error: serverError, status } = useAppSelector((s) => s.auth)

  const {
    register: field,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  // Two-factor step: shown when the server asks for a TOTP code.
  const [needCode, setNeedCode] = useState(false)
  const [code, setCode] = useState('')

  // Google sign-in is offered only when a client id is configured at build time.
  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined

  // Redirect to Google's consent screen; it returns to /auth/google/callback with a code.
  const startGoogle = () => {
    const redirectUri = `${window.location.origin}/auth/google/callback`
    const params = new URLSearchParams({
      client_id: googleClientId ?? '',
      redirect_uri: redirectUri,
      response_type: 'code',
      scope: 'openid email profile',
      prompt: 'select_account',
    })
    window.location.href = `https://accounts.google.com/o/oauth2/v2/auth?${params.toString()}`
  }

  const onSubmit = async (values: FormValues) => {
    try {
      const res = await dispatch(
        login({ ...values, twoFactorCode: needCode ? code : undefined }),
      ).unwrap()
      if (res.requiresTwoFactor) {
        setNeedCode(true)
        return
      }
      // Tokens are stored now; load the full profile, then go home.
      await dispatch(fetchMe())
      navigate('/')
    } catch {
      // Error message is in the store and rendered below.
    }
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-6 bg-slate-50 px-4 py-10">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 shadow-lg">
        <div className="flex justify-end">
          <LangSwitch />
        </div>
        <img src="/logo.svg" alt="TaskPilot" className="mx-auto w-44" />

        <h1 className="mt-2 text-center text-2xl font-bold text-[#1E2A44]">
          {t('auth.welcomeBack')}
        </h1>

        {serverError && (
          <div className="mt-4 rounded-lg bg-red-50 px-4 py-2 text-sm text-red-700">
            {serverError}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4" noValidate>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">{t('auth.email')}</label>
            <input
              type="email"
              {...field('email')}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44]"
              placeholder="you@example.com"
            />
            {errors.email && <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">{t('auth.password')}</label>
            <input
              type="password"
              {...field('password')}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44]"
              placeholder="••••••••"
            />
            {errors.password && (
              <p className="mt-1 text-sm text-red-600">{errors.password.message}</p>
            )}
          </div>

          {needCode && (
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">{t('auth.twoFactorCode')}</label>
              <input
                value={code}
                onChange={(e) => setCode(e.target.value)}
                inputMode="numeric"
                autoFocus
                placeholder="123456"
                className="w-full rounded-lg border border-slate-300 px-3 py-2 tracking-widest outline-none focus:border-[#1E2A44]"
              />
              <p className="mt-1 text-xs text-slate-500">{t('auth.twoFactorHint')}</p>
            </div>
          )}

          <button
            type="submit"
            disabled={status === 'loading'}
            className="w-full rounded-lg bg-[#1E2A44] py-2.5 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            {status === 'loading' ? t('auth.loggingIn') : needCode ? t('auth.verify') : t('auth.login')}
          </button>
        </form>

        {/* Google sign-in — shown only when a client id is configured. */}
        {googleClientId && (
          <>
            <div className="my-5 flex items-center gap-3 text-xs text-slate-400">
              <span className="h-px flex-1 bg-slate-200" />
              {t('auth.or')}
              <span className="h-px flex-1 bg-slate-200" />
            </div>
            <button
              type="button"
              onClick={startGoogle}
              className="flex w-full items-center justify-center gap-2 rounded-lg border border-slate-300 py-2.5 font-semibold text-slate-700 transition hover:bg-slate-50"
            >
              <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
                <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
                <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
                <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
                <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
              </svg>
              {t('auth.googleSignIn')}
            </button>
          </>
        )}

        <p className="mt-6 text-center text-sm text-slate-600">
          {t('auth.needAccount')}{' '}
          <Link to="/register" className="font-semibold text-[#1E2A44] hover:underline">
            {t('auth.signup')}
          </Link>
        </p>
      </div>
    </div>
  )
}
