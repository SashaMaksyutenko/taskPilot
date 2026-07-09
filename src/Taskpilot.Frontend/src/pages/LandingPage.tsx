import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import LangSwitch from '../components/LangSwitch'
import StatsPanel from '../components/StatsPanel'
import { statsService } from '../services/statsService'
import type { PublicStats } from '../types/stats'

// Feature cards (icon + i18n keys). Shown in a grid on the landing page.
const FEATURES = [
  { icon: '🗂️', key: 'projects' },
  { icon: '💬', key: 'chat' },
  { icon: '🛒', key: 'market' },
  { icon: '💡', key: 'forum' },
  { icon: '📝', key: 'notes' },
  { icon: '📅', key: 'calendar' },
] as const

/**
 * Public marketing landing page shown to non-authenticated visitors at "/".
 * Hero + feature overview + call-to-action + live public stats.
 */
export default function LandingPage() {
  const { t } = useTranslation()
  const [stats, setStats] = useState<PublicStats | null>(null)

  useEffect(() => {
    const refresh = () => statsService.getPublic().then(setStats).catch(() => {})
    refresh()
    const timer = setInterval(refresh, 10000)
    return () => clearInterval(timer)
  }, [])

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      {/* Top bar */}
      <header className="mx-auto flex max-w-5xl items-center gap-3 px-6 py-4">
        <img src="/logo.svg" alt="TaskPilot" className="h-9 w-9" />
        <span className="text-lg font-bold">TaskPilot</span>
        <div className="ml-auto flex items-center gap-3">
          <LangSwitch />
          <Link to="/login" className="text-sm font-semibold text-slate-600 hover:text-[#1E2A44] dark:text-slate-300 dark:hover:text-white">
            {t('landing.login')}
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-6">
        {/* Hero */}
        <section className="py-16 text-center">
          <img src="/logo.svg" alt="" className="mx-auto h-24 w-24" />
          <h1 className="mx-auto mt-6 max-w-2xl text-4xl font-extrabold tracking-tight">
            {t('landing.tagline')}
          </h1>
          <p className="mx-auto mt-4 max-w-xl text-slate-500 dark:text-slate-400">
            {t('landing.subtitle')}
          </p>
          <div className="mt-8 flex justify-center gap-3">
            <Link
              to="/register"
              className="rounded-lg bg-[#1E2A44] px-6 py-3 font-semibold text-white transition hover:bg-[#27345a]"
            >
              {t('landing.getStarted')}
            </Link>
            <Link
              to="/login"
              className="rounded-lg border border-slate-300 px-6 py-3 font-semibold transition hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('landing.login')}
            </Link>
          </div>
        </section>

        {/* Features */}
        <section className="pb-12">
          <h2 className="mb-6 text-center text-2xl font-bold">{t('landing.featuresTitle')}</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((f) => (
              <div key={f.key} className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
                <div className="text-3xl">{f.icon}</div>
                <h3 className="mt-3 font-bold">{t(`landing.f.${f.key}`)}</h3>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{t(`landing.f.${f.key}D`)}</p>
              </div>
            ))}
          </div>
        </section>

        {/* Live public stats */}
        {stats && (
          <section className="pb-16">
            <StatsPanel stats={stats} />
          </section>
        )}
      </main>

      <footer className="border-t border-slate-200 py-6 text-center text-sm text-slate-400 dark:border-slate-700">
        © {new Date().getFullYear()} TaskPilot
      </footer>
    </div>
  )
}
