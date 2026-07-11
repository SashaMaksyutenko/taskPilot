import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { AxiosError } from 'axios'
import QRCode from 'qrcode'
import Avatar from '../components/Avatar'
import { notify } from '../lib/toast'
import { apiErrorMessage } from '../lib/apiError'
import { enablePush, disablePush, getPushEnabled, pushSupported } from '../lib/push'
import { authService } from '../services/authService'
import { notificationService } from '../services/notificationService'
import { userService, type UpdateProfileData } from '../services/userService'
import { webhookService } from '../services/webhookService'
import { apiKeyService, type ApiKey } from '../services/apiKeyService'
import { fetchMe, logout } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import { WEBHOOK_EVENTS, type Webhook } from '../types/webhook'
import type { Appeal, Warning } from '../types/admin'
import type { Session } from '../types/auth'
import AppealModal from '../components/AppealModal'

// Notification types the user can toggle (mirror the backend NotificationType enum).
const NOTIF_TYPES = ['Task', 'Chat', 'Forum', 'Marketplace', 'Moderation', 'General'] as const

/** Friendly "Browser on OS" label from a user-agent string. */
function deviceLabel(ua: string | null): string {
  if (!ua) return 'Unknown device'
  const browser = /Edg/.test(ua) ? 'Edge'
    : /Chrome/.test(ua) ? 'Chrome'
    : /Firefox/.test(ua) ? 'Firefox'
    : /Safari/.test(ua) ? 'Safari' : 'Browser'
  const os = /Windows/.test(ua) ? 'Windows'
    : /Android/.test(ua) ? 'Android'
    : /iPhone|iPad|iOS/.test(ua) ? 'iOS'
    : /Mac OS/.test(ua) ? 'macOS'
    : /Linux/.test(ua) ? 'Linux' : ''
  return os ? `${browser} · ${os}` : browser
}

const emptyForm: UpdateProfileData = {
  name: '',
  title: '',
  bio: '',
  location: '',
  website: '',
  linkedIn: '',
  github: '',
  phone: '',
  showEmail: false,
}

/**
 * Account settings: edit profile + contact links (with an email-visibility toggle)
 * and change the password.
 */
export default function SettingsPage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { user, isAuthenticated } = useAppSelector((s) => s.auth)

  // Account closure (irreversible).
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deletePassword, setDeletePassword] = useState('')
  const [deleteMsg, setDeleteMsg] = useState('')

  const confirmDelete = async () => {
    setDeleteMsg('')
    try {
      await userService.deleteAccount(deletePassword)
      dispatch(logout())
      navigate('/login')
    } catch (e) {
      setDeleteMsg(e instanceof AxiosError ? (e.response?.data?.error ?? t('settings.failed')) : t('settings.failed'))
    }
  }

  const [form, setForm] = useState<UpdateProfileData>(emptyForm)
  const [profileMsg, setProfileMsg] = useState('')
  const [saving, setSaving] = useState(false)
  const [avatarBusy, setAvatarBusy] = useState(false)

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [pwMsg, setPwMsg] = useState('')

  const [webhooks, setWebhooks] = useState<Webhook[]>([])
  const [hookUrl, setHookUrl] = useState('')
  const [hookEvent, setHookEvent] = useState<string>(WEBHOOK_EVENTS[0])

  // Notification preferences (opt-out sets per channel).
  const [disabledNotif, setDisabledNotif] = useState<string[]>([])
  const [disabledEmail, setDisabledEmail] = useState<string[]>([])
  useEffect(() => {
    notificationService
      .getPreferences()
      .then((p) => {
        setDisabledNotif(p.disabledTypes)
        setDisabledEmail(p.disabledEmailTypes)
      })
      .catch(() => {})
  }, [])

  // Browser (Web Push) notifications.
  const [pushEnabled, setPushEnabled] = useState(false)
  const [pushBusy, setPushBusy] = useState(false)
  useEffect(() => {
    getPushEnabled().then(setPushEnabled).catch(() => {})
  }, [])

  const togglePush = async () => {
    setPushBusy(true)
    try {
      if (pushEnabled) {
        await disablePush()
        setPushEnabled(false)
      } else {
        await enablePush()
        setPushEnabled(true)
        notify.success(t('push.enabled'))
      }
    } catch (e) {
      const code = e instanceof Error ? e.message : ''
      const key = code === 'denied' ? 'push.denied' : code === 'not-configured' ? 'push.notConfigured' : code === 'unsupported' ? 'push.unsupported' : 'push.failed'
      notify.error(t(key))
    } finally {
      setPushBusy(false)
    }
  }

  // Telegram linking.
  const [telegram, setTelegram] = useState<{ linked: boolean; botUsername: string }>({ linked: false, botUsername: '' })
  const [telegramCode, setTelegramCode] = useState<string | null>(null)
  useEffect(() => {
    notificationService.getTelegramStatus().then(setTelegram).catch(() => {})
  }, [])

  const connectTelegram = async () => {
    try {
      const res = await notificationService.createTelegramLinkCode()
      setTelegramCode(res.code)
      setTelegram((s) => ({ ...s, botUsername: res.botUsername }))
    } catch (e) {
      // Surface why nothing happened (e.g. "Telegram bot is not configured").
      notify.error(apiErrorMessage(e))
    }
  }

  const unlinkTelegram = async () => {
    await notificationService.unlinkTelegram().catch(() => {})
    setTelegramCode(null)
    setTelegram((s) => ({ ...s, linked: false }))
  }

  // Viber linking.
  const [viber, setViber] = useState<{ linked: boolean; botName: string }>({ linked: false, botName: '' })
  const [viberCode, setViberCode] = useState<string | null>(null)
  useEffect(() => {
    notificationService.getViberStatus().then(setViber).catch(() => {})
  }, [])

  const connectViber = async () => {
    try {
      const res = await notificationService.createViberLinkCode()
      setViberCode(res.code)
      setViber((s) => ({ ...s, botName: res.botName }))
    } catch (e) {
      // Surface why nothing happened (e.g. "Viber bot is not configured").
      notify.error(apiErrorMessage(e))
    }
  }

  const unlinkViber = async () => {
    await notificationService.unlinkViber().catch(() => {})
    setViberCode(null)
    setViber((s) => ({ ...s, linked: false }))
  }

  // Personal API keys.
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([])
  const [newKeyName, setNewKeyName] = useState('')
  const [createdKey, setCreatedKey] = useState<string | null>(null)
  useEffect(() => {
    apiKeyService.list().then(setApiKeys).catch(() => {})
  }, [])

  const createApiKey = async () => {
    if (!newKeyName.trim()) return
    try {
      const created = await apiKeyService.create(newKeyName.trim())
      setCreatedKey(created.key) // shown once
      setNewKeyName('')
      setApiKeys((keys) => [{ ...created }, ...keys])
    } catch (e) {
      notify.error(apiErrorMessage(e))
    }
  }

  const revokeApiKey = async (id: string) => {
    await apiKeyService.revoke(id).catch(() => {})
    setApiKeys((keys) => keys.filter((k) => k.id !== id))
  }

  const copyKey = async () => {
    if (!createdKey) return
    await navigator.clipboard.writeText(createdKey).catch(() => {})
    notify.success(t('apiKeys.copied'))
  }

  // channel: 'inapp' toggles the bell; 'email' toggles email delivery.
  const toggleNotif = async (type: string, channel: 'inapp' | 'email') => {
    const [current, setter] = channel === 'inapp' ? [disabledNotif, setDisabledNotif] : [disabledEmail, setDisabledEmail]
    const next = current.includes(type) ? current.filter((x) => x !== type) : [...current, type]
    setter(next)
    const inApp = channel === 'inapp' ? next : disabledNotif
    const email = channel === 'email' ? next : disabledEmail
    notificationService.updatePreferences(inApp, email).catch(() => {})
  }

  // Active sessions.
  const [sessions, setSessions] = useState<Session[]>([])
  const loadSessions = () => authService.getSessions().then(setSessions).catch(() => {})
  useEffect(() => {
    loadSessions()
  }, [])

  const revokeSession = async (id: string) => {
    await authService.revokeSession(id).catch(() => {})
    loadSessions()
  }
  const revokeOthers = async () => {
    await authService.revokeOtherSessions().catch(() => {})
    loadSessions()
  }

  // Two-factor auth.
  const [twoFaSetup, setTwoFaSetup] = useState<{ secret: string; otpauthUri: string } | null>(null)
  const [twoFaQr, setTwoFaQr] = useState('')
  const [twoFaCode, setTwoFaCode] = useState('')
  const [twoFaMsg, setTwoFaMsg] = useState('')
  const [backupCodes, setBackupCodes] = useState<string[]>([])
  const [backupRemaining, setBackupRemaining] = useState<number | null>(null)

  // Load remaining backup-code count when 2FA is on.
  useEffect(() => {
    if (user?.twoFactorEnabled) authService.backupCodesCount().then(setBackupRemaining).catch(() => {})
    else setBackupRemaining(null)
  }, [user?.twoFactorEnabled])

  const regenerateBackup = async () => {
    const codes = await authService.regenerateBackupCodes().catch(() => null)
    if (codes) {
      setBackupCodes(codes)
      setBackupRemaining(codes.length)
    }
  }

  // Render the otpauth URI as a scannable QR image.
  useEffect(() => {
    if (!twoFaSetup) {
      setTwoFaQr('')
      return
    }
    QRCode.toDataURL(twoFaSetup.otpauthUri, { margin: 1, width: 180 })
      .then(setTwoFaQr)
      .catch(() => setTwoFaQr(''))
  }, [twoFaSetup])

  const startTwoFa = async () => {
    setTwoFaMsg('')
    const setup = await authService.setupTwoFactor().catch(() => null)
    if (setup) setTwoFaSetup(setup)
  }
  const confirmTwoFa = async () => {
    try {
      const codes = await authService.enableTwoFactor(twoFaCode.trim())
      setBackupCodes(codes)
      setTwoFaSetup(null)
      setTwoFaCode('')
      dispatch(fetchMe())
    } catch (e) {
      setTwoFaMsg(e instanceof AxiosError ? (e.response?.data?.error ?? t('settings.failed')) : t('settings.failed'))
    }
  }
  const disableTwoFa = async () => {
    try {
      await authService.disableTwoFactor(twoFaCode.trim())
      setTwoFaCode('')
      dispatch(fetchMe())
    } catch (e) {
      setTwoFaMsg(e instanceof AxiosError ? (e.response?.data?.error ?? t('settings.failed')) : t('settings.failed'))
    }
  }

  const downloadData = async () => {
    const blob = await userService.exportData().catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'taskpilot-data.json'
    a.click()
    URL.revokeObjectURL(url)
  }

  const [warnings, setWarnings] = useState<Warning[]>([])
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const [appealTarget, setAppealTarget] = useState<Warning | null>(null)

  const loadModeration = () => {
    userService.getMyWarnings().then(setWarnings).catch(() => {})
    userService.getMyAppeals().then(setAppeals).catch(() => {})
  }
  useEffect(loadModeration, [])

  // Warning ids that already have a pending appeal (to disable the button).
  const pendingAppealWarningIds = new Set(
    appeals.filter((a) => a.status === 'Pending' && a.warningId).map((a) => a.warningId as string),
  )

  const submitAppeal = async (message: string) => {
    if (!appealTarget) return
    await userService.createAppeal({ warningId: appealTarget.id, message }).catch(() => {})
    setAppealTarget(null)
    loadModeration()
  }

  const loadWebhooks = () => webhookService.getWebhooks().then(setWebhooks).catch(() => {})
  useEffect(() => {
    loadWebhooks()
  }, [])

  const addWebhook = async () => {
    if (!hookUrl.trim()) return
    await webhookService.createWebhook({ url: hookUrl.trim(), event: hookEvent }).catch(() => {})
    setHookUrl('')
    loadWebhooks()
  }

  const removeWebhook = async (id: string) => {
    await webhookService.deleteWebhook(id).catch(() => {})
    loadWebhooks()
  }

  useEffect(() => {
    if (isAuthenticated && !user) dispatch(fetchMe())
  }, [isAuthenticated, user, dispatch])

  // Populate the form once the profile is loaded.
  useEffect(() => {
    if (user) {
      setForm({
        name: user.name,
        title: user.title ?? '',
        bio: user.bio ?? '',
        location: user.location ?? '',
        website: user.website ?? '',
        linkedIn: user.linkedIn ?? '',
        github: user.github ?? '',
        phone: user.phone ?? '',
        showEmail: user.showEmail,
      })
    }
  }, [user])

  const set = (key: keyof UpdateProfileData, value: string | boolean) =>
    setForm((f) => ({ ...f, [key]: value }))

  const saveProfile = async () => {
    setSaving(true)
    setProfileMsg('')
    try {
      await userService.updateProfile(form)
      dispatch(fetchMe())
      setProfileMsg(t('settings.profileSaved'))
    } catch {
      setProfileMsg(t('settings.profileSaveError'))
    } finally {
      setSaving(false)
    }
  }

  const onAvatarChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setAvatarBusy(true)
    try {
      await userService.uploadAvatar(file)
      dispatch(fetchMe())
    } finally {
      setAvatarBusy(false)
      e.target.value = '' // allow re-selecting the same file
    }
  }

  const onAvatarRemove = async () => {
    setAvatarBusy(true)
    try {
      await userService.removeAvatar()
      dispatch(fetchMe())
    } finally {
      setAvatarBusy(false)
    }
  }

  const changePassword = async () => {
    setPwMsg('')
    try {
      await userService.changePassword(current, next)
      setPwMsg(t('settings.passwordChanged'))
      setCurrent('')
      setNext('')
    } catch (e) {
      const msg = e instanceof AxiosError ? (e.response?.data?.error ?? t('settings.failed')) : t('settings.failed')
      setPwMsg(msg)
    }
  }

  return (
    <>
    <div className="mx-auto max-w-2xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">{t('settings.title')}</h1>

        {/* Moderation warnings */}
        {warnings.length > 0 && (
          <section className="mb-8 rounded-xl border border-amber-300 bg-amber-50 p-6 dark:border-amber-700 dark:bg-amber-950/30">
            <h2 className="mb-3 font-bold text-amber-800 dark:text-amber-200">
              ⚠️ {t('warn.myTitle', { count: warnings.length })}
            </h2>
            <ul className="space-y-3">
              {warnings.map((w) => (
                <li key={w.id} className="flex items-start justify-between gap-3 text-sm text-amber-900 dark:text-amber-100">
                  <div className="min-w-0">
                    <p className="whitespace-pre-wrap">{w.reason}</p>
                    <p className="mt-1 text-xs text-amber-700/80 dark:text-amber-300/70">
                      {w.issuedByName} · {new Date(w.createdAt).toLocaleString()}
                    </p>
                  </div>
                  {pendingAppealWarningIds.has(w.id) ? (
                    <span className="flex-none text-xs font-semibold text-amber-700/80 dark:text-amber-300/70">
                      {t('appeal.pending')}
                    </span>
                  ) : (
                    <button
                      onClick={() => setAppealTarget(w)}
                      className="flex-none rounded-lg border border-amber-400 px-3 py-1 text-xs font-semibold text-amber-800 hover:bg-amber-100 dark:border-amber-600 dark:text-amber-200 dark:hover:bg-amber-900/40"
                    >
                      {t('appeal.appeal')}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* My appeals */}
        {appeals.length > 0 && (
          <section className="mb-8 rounded-xl border border-border bg-surface p-6">
            <h2 className="mb-3 font-bold">{t('appeal.myTitle')}</h2>
            <ul className="space-y-3">
              {appeals.map((a) => (
                <li key={a.id} className="text-sm">
                  <div className="flex items-center gap-2">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                        a.status === 'Approved'
                          ? 'bg-green-100 text-green-700'
                          : a.status === 'Rejected'
                            ? 'bg-red-100 text-red-700'
                            : 'bg-border text-foreground'
                      }`}
                    >
                      {t(`appeal.status.${a.status}`, a.status)}
                    </span>
                    <span className="text-xs text-muted">{new Date(a.createdAt).toLocaleString()}</span>
                  </div>
                  <p className="mt-1 whitespace-pre-wrap text-foreground">{a.message}</p>
                  {a.reviewNote && (
                    <p className="mt-1 text-xs text-muted">
                      {t('appeal.reviewNote')}: {a.reviewNote}
                    </p>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* Profile */}
        <section className="mb-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-4 font-bold">{t('settings.profile')}</h2>

          {/* Avatar */}
          <div className="mb-6 flex items-center gap-4">
            <Avatar name={user?.name ?? '?'} src={user?.avatarUrl} size={72} />
            <div className="flex flex-col gap-2">
              <label
                className={`cursor-pointer rounded-lg border border-border px-4 py-2 text-sm font-semibold transition hover:bg-canvas ${
                  avatarBusy ? 'pointer-events-none opacity-60' : ''
                }`}
              >
                {t('settings.changeAvatar')}
                <input type="file" accept="image/*" className="hidden" onChange={onAvatarChange} disabled={avatarBusy} />
              </label>
              {user?.avatarUrl && (
                <button
                  onClick={onAvatarRemove}
                  disabled={avatarBusy}
                  className="text-left text-xs font-semibold text-red-600 hover:underline disabled:opacity-60"
                >
                  {t('settings.removeAvatar')}
                </button>
              )}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('settings.name')} value={form.name} onChange={(v) => set('name', v)} />
            <Field label={t('settings.jobTitle')} value={form.title ?? ''} onChange={(v) => set('title', v)} />
            <Field label={t('settings.location')} value={form.location ?? ''} onChange={(v) => set('location', v)} />
            <Field label={t('settings.phone')} value={form.phone ?? ''} onChange={(v) => set('phone', v)} />
            <Field label={t('settings.website')} value={form.website ?? ''} onChange={(v) => set('website', v)} />
            <Field label={t('settings.linkedin')} value={form.linkedIn ?? ''} onChange={(v) => set('linkedIn', v)} />
            <Field label={t('settings.github')} value={form.github ?? ''} onChange={(v) => set('github', v)} />
          </div>

          <label className="mb-1 mt-4 block text-sm font-medium text-foreground">{t('settings.bio')}</label>
          <textarea
            value={form.bio ?? ''}
            onChange={(e) => set('bio', e.target.value)}
            rows={3}
            className="w-full rounded-lg border border-border bg-canvas px-3 py-2 outline-none focus:border-primary"
          />

          <label className="mt-4 flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.showEmail} onChange={(e) => set('showEmail', e.target.checked)} />
            {t('settings.showEmail')}
          </label>

          <div className="mt-5 flex items-center gap-3">
            <button
              onClick={saveProfile}
              disabled={saving}
              className="rounded-lg bg-primary px-5 py-2 font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
            >
              {t('settings.saveProfile')}
            </button>
            {profileMsg && <span className="text-sm text-muted">{profileMsg}</span>}
          </div>
        </section>

        {/* Change password */}
        <section className="rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-4 font-bold">{t('settings.changePassword')}</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('settings.currentPassword')} type="password" value={current} onChange={setCurrent} />
            <Field label={t('settings.newPassword')} type="password" value={next} onChange={setNext} />
          </div>
          <div className="mt-5 flex items-center gap-3">
            <button
              onClick={changePassword}
              className="rounded-lg bg-primary px-5 py-2 font-semibold text-white transition hover:bg-primary-hover"
            >
              {t('settings.changePassword')}
            </button>
            {pwMsg && <span className="text-sm text-muted">{pwMsg}</span>}
          </div>
        </section>

        {/* Active sessions */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="font-bold">{t('sessions.title')}</h2>
            {sessions.length > 1 && (
              <button onClick={revokeOthers} className="text-sm font-semibold text-red-600 hover:underline">
                {t('sessions.revokeOthers')}
              </button>
            )}
          </div>
          <ul className="space-y-2">
            {sessions.map((s) => (
              <li key={s.id} className="flex items-center gap-3 rounded-lg border border-border px-3 py-2 text-sm">
                <div className="min-w-0 flex-1">
                  <div className="font-medium">
                    {deviceLabel(s.userAgent)}
                    {s.isCurrent && (
                      <span className="ml-2 rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-semibold text-green-700 dark:bg-green-900/40 dark:text-green-300">
                        {t('sessions.current')}
                      </span>
                    )}
                  </div>
                  <div className="text-xs text-muted">
                    {s.ipAddress ?? '—'} · {new Date(s.createdAtUtc).toLocaleString()}
                  </div>
                </div>
                {!s.isCurrent && (
                  <button onClick={() => revokeSession(s.id)} className="flex-none text-xs font-semibold text-red-600 hover:underline">
                    {t('sessions.revoke')}
                  </button>
                )}
              </li>
            ))}
          </ul>
        </section>

        {/* Two-factor authentication */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('twoFa.title')}</h2>
          <p className="mb-4 text-sm text-muted">{t('twoFa.desc')}</p>

          {user?.twoFactorEnabled ? (
            <div className="space-y-3">
              <div className="inline-flex items-center gap-2 rounded-full bg-green-100 px-3 py-1 text-xs font-semibold text-green-700 dark:bg-green-900/40 dark:text-green-300">
                ✓ {t('twoFa.enabled')}
              </div>
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted">
                  {t('twoFa.backupRemaining', { count: backupRemaining ?? 0 })}
                </span>
                <button onClick={regenerateBackup} className="font-semibold text-primary hover:underline">
                  {t('twoFa.regenerate')}
                </button>
              </div>
              <div className="flex flex-col gap-2 sm:flex-row">
                <input
                  value={twoFaCode}
                  onChange={(e) => setTwoFaCode(e.target.value)}
                  inputMode="numeric"
                  placeholder={t('twoFa.codePlaceholder')}
                  className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm tracking-widest outline-none"
                />
                <button onClick={disableTwoFa} className="rounded-lg border border-red-300 px-4 py-2 text-sm font-semibold text-red-600 hover:bg-red-50 dark:border-red-700 dark:hover:bg-red-950">
                  {t('twoFa.disable')}
                </button>
              </div>
            </div>
          ) : twoFaSetup ? (
            <div className="space-y-3">
              <p className="text-sm text-foreground">{t('twoFa.scanHint')}</p>
              {twoFaQr && (
                <img
                  src={twoFaQr}
                  alt="2FA QR code"
                  className="rounded-lg border border-border bg-surface p-2"
                  width={180}
                  height={180}
                />
              )}
              <div className="rounded-lg bg-canvas p-3 text-sm">
                <div className="font-mono break-all text-primary">{twoFaSetup.secret}</div>
                <a href={twoFaSetup.otpauthUri} className="mt-1 block break-all text-xs text-muted hover:underline">
                  {twoFaSetup.otpauthUri}
                </a>
              </div>
              <div className="flex flex-col gap-2 sm:flex-row">
                <input
                  value={twoFaCode}
                  onChange={(e) => setTwoFaCode(e.target.value)}
                  inputMode="numeric"
                  autoFocus
                  placeholder={t('twoFa.codePlaceholder')}
                  className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm tracking-widest outline-none"
                />
                <button onClick={confirmTwoFa} className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-hover">
                  {t('twoFa.confirm')}
                </button>
                <button onClick={() => { setTwoFaSetup(null); setTwoFaCode(''); setTwoFaMsg('') }} className="text-sm font-semibold text-muted hover:text-primary">
                  {t('twoFa.cancel')}
                </button>
              </div>
            </div>
          ) : (
            <button onClick={startTwoFa} className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-hover">
              {t('twoFa.enable')}
            </button>
          )}

          {backupCodes.length > 0 && (
            <div className="mt-4 rounded-lg border border-amber-300 bg-amber-50 p-4 dark:border-amber-700 dark:bg-amber-950/30">
              <p className="mb-2 text-sm font-semibold text-amber-800 dark:text-amber-200">⚠️ {t('twoFa.backupTitle')}</p>
              <div className="grid grid-cols-2 gap-x-6 gap-y-1 font-mono text-sm text-amber-900 dark:text-amber-100">
                {backupCodes.map((c) => (
                  <span key={c}>{c}</span>
                ))}
              </div>
              <button onClick={() => setBackupCodes([])} className="mt-3 text-xs font-semibold text-amber-700 hover:underline dark:text-amber-300">
                {t('twoFa.backupSaved')}
              </button>
            </div>
          )}

          {twoFaMsg && <p className="mt-2 text-sm text-red-600">{twoFaMsg}</p>}
        </section>

        {/* Data & privacy */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('privacy.title')}</h2>
          <p className="mb-4 text-sm text-muted">{t('privacy.exportDesc')}</p>
          <button
            onClick={downloadData}
            className="rounded-lg border border-border px-4 py-2 text-sm font-semibold hover:bg-canvas"
          >
            {t('privacy.export')}
          </button>
        </section>

        {/* Notification preferences */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('notifPrefs.title')}</h2>
          <p className="mb-4 text-sm text-muted">{t('notifPrefs.desc')}</p>

          {/* A row per type with an in-app and an email toggle. */}
          <div className="grid grid-cols-[1fr_auto_auto] items-center gap-x-6 gap-y-2 text-sm">
            <span />
            <span className="text-center text-xs font-semibold text-muted">{t('notifPrefs.inApp')}</span>
            <span className="text-center text-xs font-semibold text-muted">{t('notifPrefs.email')}</span>
            {NOTIF_TYPES.map((type) => (
              <div key={type} className="contents">
                <span>{t(`notifPrefs.type.${type}`, type)}</span>
                <input
                  type="checkbox"
                  className="mx-auto"
                  checked={!disabledNotif.includes(type)}
                  onChange={() => toggleNotif(type, 'inapp')}
                />
                <input
                  type="checkbox"
                  className="mx-auto"
                  checked={!disabledEmail.includes(type)}
                  onChange={() => toggleNotif(type, 'email')}
                />
              </div>
            ))}
          </div>
        </section>

        {/* Browser push notifications */}
        {pushSupported() && (
          <section className="mt-8 rounded-xl border border-border bg-surface p-6">
            <h2 className="mb-1 font-bold">{t('push.title')}</h2>
            <p className="mb-4 text-sm text-muted">{t('push.desc')}</p>
            <div className="flex items-center gap-3 text-sm">
              {pushEnabled && (
                <span className="rounded-full bg-green-100 px-2 py-0.5 font-semibold text-green-700 dark:bg-green-900/40 dark:text-green-300">
                  ✓ {t('push.on')}
                </span>
              )}
              <button
                onClick={togglePush}
                disabled={pushBusy}
                className={`rounded-lg px-5 py-2 text-sm font-semibold transition disabled:opacity-60 ${
                  pushEnabled
                    ? 'text-red-600 hover:underline'
                    : 'bg-primary text-white hover:bg-primary-hover'
                }`}
              >
                {pushEnabled ? t('push.disable') : t('push.enable')}
              </button>
            </div>
          </section>
        )}

        {/* Telegram */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('telegram.title')}</h2>
          <p className="mb-4 text-sm text-muted">{t('telegram.desc')}</p>

          {telegram.linked ? (
            <div className="flex items-center gap-3 text-sm">
              <span className="rounded-full bg-green-100 px-2 py-0.5 font-semibold text-green-700 dark:bg-green-900/40 dark:text-green-300">
                ✓ {t('telegram.linked')}
              </span>
              <button onClick={unlinkTelegram} className="font-semibold text-red-600 hover:underline">
                {t('telegram.unlink')}
              </button>
            </div>
          ) : telegramCode ? (
            <div className="space-y-3 text-sm">
              <p>{t('telegram.step1')}</p>
              <div className="rounded-lg bg-canvas px-4 py-3 text-center font-mono text-lg font-bold tracking-widest">
                {telegramCode}
              </div>
              {telegram.botUsername && (
                <a
                  href={`https://t.me/${telegram.botUsername}?start=${telegramCode}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-block rounded-lg bg-[#229ED9] px-4 py-2 font-semibold text-white transition hover:brightness-95"
                >
                  {t('telegram.openBot')}
                </a>
              )}
              <p className="text-xs text-muted">{t('telegram.step2')}</p>
            </div>
          ) : (
            <button
              onClick={connectTelegram}
              className="rounded-lg bg-[#229ED9] px-5 py-2 text-sm font-semibold text-white transition hover:brightness-95"
            >
              {t('telegram.connect')}
            </button>
          )}
        </section>

        {/* Viber */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('viber.title')}</h2>
          <p className="mb-4 text-sm text-muted">{t('viber.desc')}</p>

          {viber.linked ? (
            <div className="flex items-center gap-3 text-sm">
              <span className="rounded-full bg-green-100 px-2 py-0.5 font-semibold text-green-700 dark:bg-green-900/40 dark:text-green-300">
                ✓ {t('viber.linked')}
              </span>
              <button onClick={unlinkViber} className="font-semibold text-red-600 hover:underline">
                {t('viber.unlink')}
              </button>
            </div>
          ) : viberCode ? (
            <div className="space-y-3 text-sm">
              <p>{t('viber.step1', { bot: viber.botName || 'TaskPilot' })}</p>
              <div className="rounded-lg bg-canvas px-4 py-3 text-center font-mono text-lg font-bold tracking-widest">
                {viberCode}
              </div>
              <p className="text-xs text-muted">{t('viber.step2')}</p>
            </div>
          ) : (
            <button
              onClick={connectViber}
              className="rounded-lg bg-[#7360F2] px-5 py-2 text-sm font-semibold text-white transition hover:brightness-95"
            >
              {t('viber.connect')}
            </button>
          )}
        </section>

        {/* API keys */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('apiKeys.title')}</h2>
          <p className="mb-4 text-sm text-muted">{t('apiKeys.desc')}</p>

          {/* Freshly created key — shown once */}
          {createdKey && (
            <div className="mb-4 rounded-lg border border-green-300 bg-green-50 p-3 text-sm dark:border-green-700 dark:bg-green-950/30">
              <p className="mb-2 font-semibold text-green-800 dark:text-green-300">{t('apiKeys.createdOnce')}</p>
              <div className="flex items-center gap-2">
                <code className="flex-1 overflow-x-auto rounded bg-canvas px-2 py-1 font-mono text-xs">{createdKey}</code>
                <button onClick={copyKey} className="rounded-lg bg-primary px-3 py-1 text-xs font-semibold text-white hover:bg-primary-hover">
                  {t('apiKeys.copy')}
                </button>
                <button onClick={() => setCreatedKey(null)} className="text-xs text-muted hover:underline">
                  {t('apiKeys.dismiss')}
                </button>
              </div>
            </div>
          )}

          {/* Create */}
          <div className="mb-4 flex gap-2">
            <input
              value={newKeyName}
              onChange={(e) => setNewKeyName(e.target.value)}
              placeholder={t('apiKeys.namePlaceholder')}
              className="flex-1 rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary"
            />
            <button
              onClick={createApiKey}
              disabled={!newKeyName.trim()}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
            >
              {t('apiKeys.create')}
            </button>
          </div>

          {/* List */}
          {apiKeys.length === 0 ? (
            <p className="text-sm text-muted">{t('apiKeys.empty')}</p>
          ) : (
            <ul className="space-y-2">
              {apiKeys.map((k) => (
                <li key={k.id} className="flex items-center gap-3 rounded-lg border border-border px-3 py-2 text-sm">
                  <span className="font-semibold">{k.name}</span>
                  <code className="rounded bg-canvas px-1.5 py-0.5 font-mono text-xs text-muted">{k.prefix}…</code>
                  <span className="ml-auto text-xs text-muted">
                    {k.lastUsedAt ? t('apiKeys.lastUsed', { date: new Date(k.lastUsedAt).toLocaleDateString() }) : t('apiKeys.neverUsed')}
                  </span>
                  <button onClick={() => revokeApiKey(k.id)} className="text-xs font-semibold text-red-600 hover:underline">
                    {t('apiKeys.revoke')}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Webhooks */}
        <section className="mt-8 rounded-xl border border-border bg-surface p-6">
          <h2 className="mb-1 font-bold">{t('settings.webhooks')}</h2>
          <p className="mb-4 text-sm text-muted">
            {t('settings.webhooksDesc')}
          </p>

          {/* Existing webhooks */}
          {webhooks.length === 0 ? (
            <p className="mb-4 text-sm text-muted">{t('settings.noWebhooks')}</p>
          ) : (
            <ul className="mb-4 space-y-2">
              {webhooks.map((w) => (
                <li
                  key={w.id}
                  className="flex items-center gap-3 rounded-lg border border-border px-3 py-2 text-sm"
                >
                  <span className="rounded bg-canvas px-2 py-0.5 text-[11px] font-semibold">
                    {w.event}
                  </span>
                  <span className="min-w-0 flex-1 truncate text-foreground">{w.url}</span>
                  <button
                    onClick={() => removeWebhook(w.id)}
                    className="flex-none text-xs font-semibold text-red-600 hover:underline"
                  >
                    {t('settings.delete')}
                  </button>
                </li>
              ))}
            </ul>
          )}

          {/* Add webhook */}
          <div className="flex flex-col gap-2 sm:flex-row">
            <select
              value={hookEvent}
              onChange={(e) => setHookEvent(e.target.value)}
              className="rounded-lg border border-border bg-canvas px-2 py-2 text-sm outline-none"
            >
              {WEBHOOK_EVENTS.map((ev) => (
                <option key={ev} value={ev}>
                  {ev}
                </option>
              ))}
            </select>
            <input
              value={hookUrl}
              onChange={(e) => setHookUrl(e.target.value)}
              placeholder="https://example.com/webhook"
              className="min-w-0 flex-1 rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary"
            />
            <button
              onClick={addWebhook}
              className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-hover"
            >
              {t('settings.add')}
            </button>
          </div>
        </section>

        {/* Danger zone */}
        <section className="mt-8 rounded-xl border border-red-300 bg-red-50 p-6 dark:border-red-800 dark:bg-red-950/30">
          <h2 className="mb-1 font-bold text-red-700 dark:text-red-300">{t('danger.title')}</h2>
          <p className="mb-4 text-sm text-red-700/80 dark:text-red-300/80">{t('danger.deleteDesc')}</p>
          <button
            onClick={() => { setDeleteOpen(true); setDeletePassword(''); setDeleteMsg('') }}
            className="rounded-lg border border-red-400 px-4 py-2 text-sm font-semibold text-red-600 hover:bg-red-100 dark:border-red-700 dark:text-red-300 dark:hover:bg-red-900/40"
          >
            {t('danger.delete')}
          </button>
        </section>
      </div>

      {appealTarget && (
        <AppealModal
          warningReason={appealTarget.reason}
          onClose={() => setAppealTarget(null)}
          onSubmit={submitAppeal}
        />
      )}

      {deleteOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={() => setDeleteOpen(false)}>
          <div className="w-full max-w-md rounded-xl bg-surface p-6 shadow-elevated" onClick={(e) => e.stopPropagation()}>
            <h2 className="mb-2 text-lg font-bold text-red-700 dark:text-red-300">{t('danger.confirmTitle')}</h2>
            <p className="mb-4 text-sm text-foreground">{t('danger.confirmDesc')}</p>
            <input
              type="password"
              value={deletePassword}
              onChange={(e) => setDeletePassword(e.target.value)}
              placeholder={t('settings.currentPassword')}
              className="mb-3 w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-red-500"
            />
            {deleteMsg && <p className="mb-3 text-sm text-red-600">{deleteMsg}</p>}
            <div className="flex items-center justify-end gap-3">
              <button onClick={() => setDeleteOpen(false)} className="text-sm font-semibold text-muted hover:text-primary">
                {t('twoFa.cancel')}
              </button>
              <button
                onClick={confirmDelete}
                disabled={!deletePassword}
                className="rounded-lg bg-red-600 px-5 py-2 text-sm font-semibold text-white transition hover:bg-red-700 disabled:opacity-60"
              >
                {t('danger.delete')}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}

function Field({
  label,
  value,
  onChange,
  type = 'text',
}: {
  label: string
  value: string
  onChange: (v: string) => void
  type?: string
}) {
  return (
    <div>
      <label className="mb-1 block text-sm font-medium text-foreground">{label}</label>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-border bg-canvas px-3 py-2 outline-none focus:border-primary"
      />
    </div>
  )
}
