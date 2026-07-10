import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { authService } from '../services/authService'
import { notify } from '../lib/toast'
import { apiErrorMessage } from '../lib/apiError'

/**
 * Reset-password: landing target of the emailed link (/reset-password?token=...).
 * The user chooses a new password; on success we send them to the login page.
 */
export default function ResetPasswordPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''

  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    if (password !== confirm) {
      setError(t('reset.mismatch'))
      return
    }
    setBusy(true)
    try {
      await authService.resetPassword(token, password)
      notify.success(t('reset.success'))
      navigate('/login')
    } catch (err) {
      setError(apiErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-6">
      <div className="w-full max-w-sm rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="text-2xl font-bold text-primary">{t('reset.title')}</h1>

        {!token ? (
          <>
            <p className="mt-3 text-sm text-slate-600">{t('reset.noToken')}</p>
            <Link to="/forgot-password" className="mt-6 inline-block text-sm font-semibold text-primary hover:underline">
              {t('reset.requestNew')}
            </Link>
          </>
        ) : (
          <form onSubmit={submit} className="mt-6 space-y-4" noValidate>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">{t('reset.newPassword')}</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoFocus
                className="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-primary"
              />
              <p className="mt-1 text-xs text-slate-500">{t('reset.hint')}</p>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">{t('reset.confirm')}</label>
              <input
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-primary"
              />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button
              type="submit"
              disabled={busy || !password || !confirm}
              className="w-full rounded-lg bg-primary py-2.5 font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
            >
              {busy ? t('reset.saving') : t('reset.submit')}
            </button>
          </form>
        )}
      </div>
    </div>
  )
}
