import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import LangSwitch from '../components/LangSwitch'
import GoogleSignInButton from '../components/GoogleSignInButton'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import { register as registerThunk } from '../store/authSlice'

// Client-side validation schema. Mirrors the backend RegisterValidator rules so
// the user gets instant feedback before a request is even sent.
const schema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters'),
  email: z.email('Invalid email address'),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[a-z]/, 'Must contain a lowercase letter')
    .regex(/[A-Z]/, 'Must contain an uppercase letter')
    .regex(/[0-9]/, 'Must contain a digit'),
})

type FormValues = z.infer<typeof schema>

/**
 * Registration page: validates input with zod, dispatches the register thunk,
 * and on success sends the user to the login page.
 */
export default function RegisterPage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t } = useTranslation()
  // Server-side error (e.g. "Email is already in use") and loading flag from the store.
  const { error: serverError, status } = useAppSelector((s) => s.auth)

  const {
    register: field,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = async (values: FormValues) => {
    try {
      // unwrap() throws if the thunk was rejected, so we can branch on success.
      await dispatch(registerThunk(values)).unwrap()
      navigate('/login')
    } catch {
      // The error message is already in the store and shown below.
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 shadow-lg">
        <div className="flex justify-end">
          <LangSwitch />
        </div>
        <img src="/logo.svg" alt="TaskPilot" className="mx-auto w-44" />

        <h1 className="mt-2 text-center text-2xl font-bold text-[#1E2A44]">
          {t('auth.createAccount')}
        </h1>

        {/* Server error banner */}
        {serverError && (
          <div className="mt-4 rounded-lg bg-red-50 px-4 py-2 text-sm text-red-700">
            {serverError}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4" noValidate>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">{t('auth.name')}</label>
            <input
              type="text"
              {...field('name')}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44]"
              placeholder={t('auth.namePlaceholder')}
            />
            {errors.name && <p className="mt-1 text-sm text-red-600">{errors.name.message}</p>}
          </div>

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
            {status === 'loading' ? t('auth.creating') : t('auth.signup')}
          </button>
        </form>

        <GoogleSignInButton />

        <p className="mt-6 text-center text-sm text-slate-600">
          {t('auth.haveAccount')}{' '}
          <Link to="/login" className="font-semibold text-[#1E2A44] hover:underline">
            {t('auth.login')}
          </Link>
        </p>
      </div>
    </div>
  )
}
