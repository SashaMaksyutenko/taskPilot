import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useBranding } from '../hooks/useBranding'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import LangSwitch from '../components/LangSwitch'
import SocialSignIn from '../components/auth/SocialSignIn'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import { fetchMe, login } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

const schema = z.object({
  email: z.email('Invalid email address'),
  password: z.string().min(1, 'Password is required'),
})

type FormValues = z.infer<typeof schema>

/** Login page with split layout: brand panel + form. */
export default function LoginPage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { name: orgName } = useBranding()
  const { error: serverError, status } = useAppSelector((s) => s.auth)

  const {
    register: field,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const [needCode, setNeedCode] = useState(false)
  const [code, setCode] = useState('')
  const [remember, setRemember] = useState(true)

  const onSubmit = async (values: FormValues) => {
    try {
      const res = await dispatch(
        login({ ...values, twoFactorCode: needCode ? code : undefined, remember }),
      ).unwrap()
      if (res.requiresTwoFactor) {
        setNeedCode(true)
        return
      }
      await dispatch(fetchMe())
      navigate('/')
    } catch {
      /* error in store */
    }
  }

  return (
    <div className="flex min-h-screen">
      {/* Brand panel — hidden on small screens */}
      <div className="hidden w-[45%] flex-col justify-between gradient-hero p-12 lg:flex">
        <div>
          <img src="/logo-mark.svg" alt="" className="h-10 w-10" />
          <h1 className="mt-8 text-3xl font-extrabold tracking-tight">{orgName}</h1>
          <p className="mt-3 max-w-sm text-muted">{t('landing.subtitle')}</p>
        </div>
        <p className="text-sm text-muted">© {new Date().getFullYear()} {orgName}</p>
      </div>

      <div className="flex flex-1 flex-col items-center justify-center px-4 py-10">
        <div className="mb-6 w-full max-w-md self-end lg:hidden">
          <LangSwitch />
        </div>

        <Card className="w-full max-w-md p-8 shadow-card">
          <div className="mb-6 hidden justify-end lg:flex">
            <LangSwitch />
          </div>

          <img src="/logo.svg" alt="TaskPilot" className="mx-auto h-16 w-16 lg:hidden" />
          <h1 className="mt-4 text-center text-2xl font-bold">{t('auth.welcomeBack')}</h1>

          {serverError && (
            <div className="mt-4 rounded-lg bg-red-50 px-4 py-2 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-300">
              {serverError}
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4" noValidate>
            <div>
              <label className="mb-1.5 block text-sm font-medium">{t('auth.email')}</label>
              <Input type="email" {...field('email')} placeholder="you@example.com" />
              {errors.email && <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>}
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium">{t('auth.password')}</label>
              <Input type="password" {...field('password')} placeholder="••••••••" />
              {errors.password && (
                <p className="mt-1 text-sm text-red-600">{errors.password.message}</p>
              )}
              <div className="mt-1 text-right">
                <Link to="/forgot-password" className="text-xs font-medium text-primary hover:underline">
                  {t('auth.forgotPassword')}
                </Link>
              </div>
            </div>

            {needCode && (
              <div>
                <label className="mb-1.5 block text-sm font-medium">{t('auth.twoFactorCode')}</label>
                <Input
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  inputMode="numeric"
                  autoFocus
                  placeholder="123456"
                  className="tracking-widest"
                />
                <p className="mt-1 text-xs text-muted">{t('auth.twoFactorHint')}</p>
              </div>
            )}

            <label className="flex items-center gap-2 text-sm text-muted select-none">
              <input
                type="checkbox"
                checked={remember}
                onChange={(e) => setRemember(e.target.checked)}
                className="h-4 w-4 rounded accent-primary"
              />
              {t('auth.rememberMe')}
            </label>

            <Button type="submit" disabled={status === 'loading'} className="w-full">
              {status === 'loading' ? t('auth.loggingIn') : needCode ? t('auth.verify') : t('auth.login')}
            </Button>
          </form>

          <SocialSignIn />

          <p className="mt-6 text-center text-sm text-muted">
            {t('auth.needAccount')}{' '}
            <Link to="/register" className="font-semibold text-primary hover:underline">
              {t('auth.signup')}
            </Link>
          </p>
        </Card>
      </div>
    </div>
  )
}
