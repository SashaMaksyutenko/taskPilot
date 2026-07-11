import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { authService } from '../services/authService'

/**
 * Forgot-password: the user enters their email and we ask the backend to send a
 * reset link. The response is intentionally generic (we never reveal whether an
 * account exists), so we always show the same "check your email" confirmation.
 */
export default function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email.trim() || busy) return
    setBusy(true)
    await authService.forgotPassword(email.trim()).catch(() => {})
    setBusy(false)
    setSent(true)
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas px-6">
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-8 shadow-sm">
        <h1 className="text-2xl font-bold text-primary">{t('forgot.title')}</h1>
        {sent ? (
          <>
            <p className="mt-3 text-sm text-muted">{t('forgot.sent')}</p>
            <Link to="/login" className="mt-6 inline-block text-sm font-semibold text-primary hover:underline">
              ← {t('forgot.backToLogin')}
            </Link>
          </>
        ) : (
          <form onSubmit={submit} className="mt-6 space-y-4" noValidate>
            <p className="text-sm text-muted">{t('forgot.subtitle')}</p>
            <div>
              <label className="mb-1 block text-sm font-medium text-foreground">{t('auth.email')}</label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoFocus
                className="w-full rounded-lg border border-border px-3 py-2 outline-none focus:border-primary"
              />
            </div>
            <button
              type="submit"
              disabled={busy || !email.trim()}
              className="w-full rounded-lg bg-primary py-2.5 font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
            >
              {busy ? t('forgot.sending') : t('forgot.send')}
            </button>
            <Link to="/login" className="block text-center text-sm text-muted hover:underline">
              {t('forgot.backToLogin')}
            </Link>
          </form>
        )}
      </div>
    </div>
  )
}
