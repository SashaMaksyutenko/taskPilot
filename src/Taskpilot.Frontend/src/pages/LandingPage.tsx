import {
  Bot,
  Calendar,
  FolderKanban,
  MessageSquare,
  MessagesSquare,
  NotebookPen,
  ShoppingBag,
  Sparkles,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import FadeIn from '../components/FadeIn'
import LangSwitch from '../components/LangSwitch'
import StatsPanel from '../components/StatsPanel'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import { statsService } from '../services/statsService'
import type { PublicStats } from '../types/stats'

const FEATURES = [
  { key: 'projects', icon: FolderKanban, color: 'bg-indigo-500/10 text-indigo-600' },
  { key: 'chat', icon: MessageSquare, color: 'bg-sky-500/10 text-sky-600' },
  { key: 'market', icon: ShoppingBag, color: 'bg-orange-500/10 text-orange-600' },
  { key: 'forum', icon: MessagesSquare, color: 'bg-violet-500/10 text-violet-600' },
  { key: 'notes', icon: NotebookPen, color: 'bg-emerald-500/10 text-emerald-600' },
  { key: 'calendar', icon: Calendar, color: 'bg-amber-500/10 text-amber-600' },
] as const

/**
 * Public marketing landing page shown to non-authenticated visitors at "/".
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
    <div className="min-h-screen gradient-hero text-foreground">
      <header className="mx-auto flex max-w-6xl items-center gap-3 px-6 py-5">
        <img src="/logo-mark.svg" alt="" className="h-9 w-9" />
        <span className="text-lg font-bold tracking-tight">TaskPilot</span>
        <div className="ml-auto flex items-center gap-3">
          <LangSwitch />
          <Link to="/login" className="text-sm font-semibold text-muted hover:text-foreground">
            {t('landing.login')}
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 pb-20">
        <FadeIn>
          <section className="grid items-center gap-12 py-12 lg:grid-cols-2 lg:py-20">
            <div>
              <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-primary/20 bg-primary-muted px-3 py-1 text-xs font-semibold text-primary">
                <Sparkles className="h-3.5 w-3.5" />
                {t('landing.tagline')}
              </div>
              <h1 className="text-4xl font-extrabold leading-tight tracking-tight sm:text-5xl">
                {t('landing.heroTitle', 'Ship work faster with your team')}
              </h1>
              <p className="mt-4 max-w-lg text-lg text-muted">{t('landing.subtitle')}</p>
              <div className="mt-8 flex flex-wrap gap-3">
                <Link to="/register">
                  <Button size="lg">{t('landing.getStarted')}</Button>
                </Link>
                <Link to="/login">
                  <Button variant="secondary" size="lg">
                    {t('landing.login')}
                  </Button>
                </Link>
              </div>
            </div>

            {/* Product preview mockup */}
            <div className="relative">
              <div className="absolute -inset-4 rounded-3xl bg-gradient-to-br from-primary/20 to-accent/10 blur-2xl" />
              <Card className="relative overflow-hidden p-0 shadow-elevated">
                <div className="flex items-center gap-2 border-b border-border bg-canvas px-4 py-3">
                  <span className="h-2.5 w-2.5 rounded-full bg-red-400" />
                  <span className="h-2.5 w-2.5 rounded-full bg-amber-400" />
                  <span className="h-2.5 w-2.5 rounded-full bg-emerald-400" />
                  <span className="ml-2 text-xs text-muted">taskpilot.app / projects</span>
                </div>
                <div className="grid grid-cols-4 gap-2 p-4">
                  {['Backlog', 'In Progress', 'Review', 'Done'].map((col, i) => (
                    <div key={col} className="rounded-lg bg-canvas p-2">
                      <div className="mb-2 text-[10px] font-bold uppercase tracking-wide text-muted">{col}</div>
                      {(i < 3 ? [1, 2] : [1]).map((n) => (
                        <div key={n} className="mb-2 rounded-md border border-border bg-surface p-2 shadow-soft">
                          <div className="h-2 w-3/4 rounded bg-foreground/10" />
                          <div className="mt-2 h-1.5 w-1/2 rounded bg-primary/30" />
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              </Card>
            </div>
          </section>
        </FadeIn>

        <section className="pb-16">
          <h2 className="mb-8 text-center text-2xl font-bold">{t('landing.featuresTitle')}</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((f, i) => (
              <FadeIn key={f.key} delay={i * 0.05}>
                <Card hover className="p-5">
                  <div className={`mb-4 inline-flex rounded-xl p-2.5 ${f.color}`}>
                    <f.icon className="h-5 w-5" strokeWidth={2} />
                  </div>
                  <h3 className="font-bold">{t(`landing.f.${f.key}`)}</h3>
                  <p className="mt-1.5 text-sm leading-relaxed text-muted">{t(`landing.f.${f.key}D`)}</p>
                </Card>
              </FadeIn>
            ))}
          </div>
        </section>

        <section className="pb-8">
          <Card className="flex flex-col items-center gap-4 p-8 text-center sm:flex-row sm:text-left">
            <div className="rounded-2xl bg-primary-muted p-4 text-primary">
              <Bot className="h-8 w-8" />
            </div>
            <div className="flex-1">
              <h3 className="text-lg font-bold">{t('landing.aiTitle', 'AI assistant built in')}</h3>
              <p className="mt-1 text-sm text-muted">
                {t('landing.aiDesc', 'Ask about deadlines, tasks and team status — right inside the app.')}
              </p>
            </div>
            <Link to="/register">
              <Button variant="accent">{t('landing.tryFree', 'Try for free')}</Button>
            </Link>
          </Card>
        </section>

        {stats && (
          <section className="pb-16">
            <StatsPanel stats={stats} />
          </section>
        )}
      </main>

      <footer className="border-t border-border py-8 text-center text-sm text-muted">
        © {new Date().getFullYear()} TaskPilot
      </footer>
    </div>
  )
}
