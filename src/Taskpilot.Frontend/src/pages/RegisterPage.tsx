import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import BrandLogo from '../components/BrandLogo'
import { useBranding } from '../hooks/useBranding'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import LangSwitch from '../components/LangSwitch'
import SocialSignIn from '../components/auth/SocialSignIn'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import { register as registerThunk } from '../store/authSlice'

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

/** Registration page with split brand + form layout. */
export default function RegisterPage() {
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

  const onSubmit = async (values: FormValues) => {
    try {
      await dispatch(registerThunk(values)).unwrap()
      navigate('/login')
    } catch {
      /* error in store */
    }
  }

  return (
    <div className="flex min-h-screen">
      <div className="hidden w-[45%] flex-col justify-between gradient-hero p-12 lg:flex">
        <div>
          <BrandLogo className="h-10 w-10 rounded" />
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

          <BrandLogo fallback="/logo.svg" className="mx-auto h-16 w-16 rounded-lg lg:hidden" />
          <h1 className="mt-4 text-center text-2xl font-bold">{t('auth.createAccount')}</h1>

          {serverError && (
            <div className="mt-4 rounded-lg bg-red-50 px-4 py-2 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-300">
              {serverError}
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4" noValidate>
            <div>
              <label className="mb-1.5 block text-sm font-medium">{t('auth.name')}</label>
              <Input type="text" {...field('name')} placeholder={t('auth.namePlaceholder')} />
              {errors.name && <p className="mt-1 text-sm text-red-600">{errors.name.message}</p>}
            </div>

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
            </div>

            <Button type="submit" disabled={status === 'loading'} className="w-full">
              {status === 'loading' ? t('auth.creating') : t('auth.signup')}
            </Button>
          </form>

          <SocialSignIn />

          <p className="mt-6 text-center text-sm text-muted">
            {t('auth.haveAccount')}{' '}
            <Link to="/login" className="font-semibold text-primary hover:underline">
              {t('auth.login')}
            </Link>
          </p>
        </Card>
      </div>
    </div>
  )
}
