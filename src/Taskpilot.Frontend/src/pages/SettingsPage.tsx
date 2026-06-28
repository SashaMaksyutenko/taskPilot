import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AxiosError } from 'axios'
import Avatar from '../components/Avatar'
import Navbar from '../components/Navbar'
import { userService, type UpdateProfileData } from '../services/userService'
import { webhookService } from '../services/webhookService'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import { WEBHOOK_EVENTS, type Webhook } from '../types/webhook'
import type { Warning } from '../types/admin'

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
  const { t } = useTranslation()
  const { user, isAuthenticated } = useAppSelector((s) => s.auth)

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

  const [warnings, setWarnings] = useState<Warning[]>([])
  useEffect(() => {
    userService.getMyWarnings().then(setWarnings).catch(() => {})
  }, [])

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
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-2xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">{t('settings.title')}</h1>

        {/* Moderation warnings */}
        {warnings.length > 0 && (
          <section className="mb-8 rounded-xl border border-amber-300 bg-amber-50 p-6 dark:border-amber-700 dark:bg-amber-950/30">
            <h2 className="mb-3 font-bold text-amber-800 dark:text-amber-200">
              ⚠️ {t('warn.myTitle', { count: warnings.length })}
            </h2>
            <ul className="space-y-3">
              {warnings.map((w) => (
                <li key={w.id} className="text-sm text-amber-900 dark:text-amber-100">
                  <p className="whitespace-pre-wrap">{w.reason}</p>
                  <p className="mt-1 text-xs text-amber-700/80 dark:text-amber-300/70">
                    {w.issuedByName} · {new Date(w.createdAt).toLocaleString()}
                  </p>
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* Profile */}
        <section className="mb-8 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-4 font-bold">{t('settings.profile')}</h2>

          {/* Avatar */}
          <div className="mb-6 flex items-center gap-4">
            <Avatar name={user?.name ?? '?'} src={user?.avatarUrl} size={72} />
            <div className="flex flex-col gap-2">
              <label
                className={`cursor-pointer rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold transition hover:bg-slate-50 dark:border-slate-600 dark:hover:bg-slate-700 ${
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

          <label className="mb-1 mt-4 block text-sm font-medium text-slate-700 dark:text-slate-300">{t('settings.bio')}</label>
          <textarea
            value={form.bio ?? ''}
            onChange={(e) => set('bio', e.target.value)}
            rows={3}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />

          <label className="mt-4 flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.showEmail} onChange={(e) => set('showEmail', e.target.checked)} />
            {t('settings.showEmail')}
          </label>

          <div className="mt-5 flex items-center gap-3">
            <button
              onClick={saveProfile}
              disabled={saving}
              className="rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
            >
              {t('settings.saveProfile')}
            </button>
            {profileMsg && <span className="text-sm text-slate-500 dark:text-slate-400">{profileMsg}</span>}
          </div>
        </section>

        {/* Change password */}
        <section className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-4 font-bold">{t('settings.changePassword')}</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('settings.currentPassword')} type="password" value={current} onChange={setCurrent} />
            <Field label={t('settings.newPassword')} type="password" value={next} onChange={setNext} />
          </div>
          <div className="mt-5 flex items-center gap-3">
            <button
              onClick={changePassword}
              className="rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a]"
            >
              {t('settings.changePassword')}
            </button>
            {pwMsg && <span className="text-sm text-slate-500 dark:text-slate-400">{pwMsg}</span>}
          </div>
        </section>

        {/* Webhooks */}
        <section className="mt-8 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-1 font-bold">{t('settings.webhooks')}</h2>
          <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
            {t('settings.webhooksDesc')}
          </p>

          {/* Existing webhooks */}
          {webhooks.length === 0 ? (
            <p className="mb-4 text-sm text-slate-400">{t('settings.noWebhooks')}</p>
          ) : (
            <ul className="mb-4 space-y-2">
              {webhooks.map((w) => (
                <li
                  key={w.id}
                  className="flex items-center gap-3 rounded-lg border border-slate-200 px-3 py-2 text-sm dark:border-slate-700"
                >
                  <span className="rounded bg-slate-100 px-2 py-0.5 text-[11px] font-semibold dark:bg-slate-700">
                    {w.event}
                  </span>
                  <span className="min-w-0 flex-1 truncate text-slate-600 dark:text-slate-300">{w.url}</span>
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
              className="rounded-lg border border-slate-300 bg-white px-2 py-2 text-sm outline-none dark:border-slate-600 dark:bg-slate-900"
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
              className="min-w-0 flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            <button
              onClick={addWebhook}
              className="rounded-lg bg-[#1E2A44] px-5 py-2 text-sm font-semibold text-white hover:bg-[#27345a]"
            >
              {t('settings.add')}
            </button>
          </div>
        </section>
      </main>
    </div>
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
      <label className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{label}</label>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
      />
    </div>
  )
}
