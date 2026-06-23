import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import LangSwitch from '../components/LangSwitch'
import StatsPanel from '../components/StatsPanel'
import { statsService } from '../services/statsService'
import { fetchMe, login } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import type { PublicStats } from '../types/stats'

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

  // Public site stats shown below the form (visible to everyone, like a forum footer).
  const [stats, setStats] = useState<PublicStats | null>(null)
  useEffect(() => {
    statsService.getPublic().then(setStats).catch(() => {})
  }, [])

  const {
    register: field,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = async (values: FormValues) => {
    try {
      await dispatch(login(values)).unwrap()
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

          <button
            type="submit"
            disabled={status === 'loading'}
            className="w-full rounded-lg bg-[#1E2A44] py-2.5 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            {status === 'loading' ? t('auth.loggingIn') : t('auth.login')}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-slate-600">
          {t('auth.needAccount')}{' '}
          <Link to="/register" className="font-semibold text-[#1E2A44] hover:underline">
            {t('auth.signup')}
          </Link>
        </p>
      </div>

      {stats && (
        <div className="w-full max-w-md">
          <StatsPanel stats={stats} />
        </div>
      )}
    </div>
  )
}
