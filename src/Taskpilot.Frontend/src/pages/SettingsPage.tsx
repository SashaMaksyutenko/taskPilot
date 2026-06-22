import { useEffect, useState } from 'react'
import { AxiosError } from 'axios'
import Navbar from '../components/Navbar'
import { userService, type UpdateProfileData } from '../services/userService'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

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
  const { user, isAuthenticated } = useAppSelector((s) => s.auth)

  const [form, setForm] = useState<UpdateProfileData>(emptyForm)
  const [profileMsg, setProfileMsg] = useState('')
  const [saving, setSaving] = useState(false)

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [pwMsg, setPwMsg] = useState('')

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
      setProfileMsg('Profile saved.')
    } catch {
      setProfileMsg('Could not save profile.')
    } finally {
      setSaving(false)
    }
  }

  const changePassword = async () => {
    setPwMsg('')
    try {
      await userService.changePassword(current, next)
      setPwMsg('Password changed.')
      setCurrent('')
      setNext('')
    } catch (e) {
      const msg = e instanceof AxiosError ? (e.response?.data?.error ?? 'Failed.') : 'Failed.'
      setPwMsg(msg)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-2xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">Settings</h1>

        {/* Profile */}
        <section className="mb-8 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-4 font-bold">Profile</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Name" value={form.name} onChange={(v) => set('name', v)} />
            <Field label="Title" value={form.title ?? ''} onChange={(v) => set('title', v)} />
            <Field label="Location" value={form.location ?? ''} onChange={(v) => set('location', v)} />
            <Field label="Phone" value={form.phone ?? ''} onChange={(v) => set('phone', v)} />
            <Field label="Website" value={form.website ?? ''} onChange={(v) => set('website', v)} />
            <Field label="LinkedIn" value={form.linkedIn ?? ''} onChange={(v) => set('linkedIn', v)} />
            <Field label="GitHub" value={form.github ?? ''} onChange={(v) => set('github', v)} />
          </div>

          <label className="mb-1 mt-4 block text-sm font-medium text-slate-700 dark:text-slate-300">Bio</label>
          <textarea
            value={form.bio ?? ''}
            onChange={(e) => set('bio', e.target.value)}
            rows={3}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />

          <label className="mt-4 flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.showEmail} onChange={(e) => set('showEmail', e.target.checked)} />
            Show my email on my public profile
          </label>

          <div className="mt-5 flex items-center gap-3">
            <button
              onClick={saveProfile}
              disabled={saving}
              className="rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
            >
              Save profile
            </button>
            {profileMsg && <span className="text-sm text-slate-500 dark:text-slate-400">{profileMsg}</span>}
          </div>
        </section>

        {/* Change password */}
        <section className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-4 font-bold">Change password</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Current password" type="password" value={current} onChange={setCurrent} />
            <Field label="New password" type="password" value={next} onChange={setNext} />
          </div>
          <div className="mt-5 flex items-center gap-3">
            <button
              onClick={changePassword}
              className="rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a]"
            >
              Change password
            </button>
            {pwMsg && <span className="text-sm text-slate-500 dark:text-slate-400">{pwMsg}</span>}
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
