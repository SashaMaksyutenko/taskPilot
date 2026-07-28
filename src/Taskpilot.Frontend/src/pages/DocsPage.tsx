import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Markdown from '../components/Markdown'
import { DOC_ARTICLES } from '../data/docsContent'

/**
 * Public docs/help site. A sidebar of topics on the left, the selected article (Markdown)
 * on the right. Bilingual — content follows the active language. No auth required.
 */
export default function DocsPage() {
  const { t, i18n } = useTranslation()
  const { slug } = useParams()
  const lang = i18n.language?.startsWith('uk') ? 'uk' : 'en'

  const article = DOC_ARTICLES.find((a) => a.slug === slug) ?? DOC_ARTICLES[0]

  return (
    <div className="min-h-screen gradient-hero text-foreground">
      <header className="mx-auto flex max-w-6xl items-center gap-3 px-6 py-5">
        <Link to="/" className="flex items-center gap-3">
          <img src="/logo-mark.svg" alt="" className="h-9 w-9" />
          <span className="text-lg font-bold tracking-tight">TaskPilot</span>
        </Link>
        <div className="ml-auto flex items-center gap-3">
          <Link to="/" className="text-sm font-semibold text-muted hover:text-foreground">
            ← {t('nav.home')}
          </Link>
          <Link to="/pricing" className="text-sm font-semibold text-muted hover:text-foreground">
            {t('pricing.nav')}
          </Link>
          <Link
            to="/register"
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover"
          >
            {t('landing.getStarted')}
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 pb-20">
        <h1 className="mb-8 text-3xl font-extrabold tracking-tight">{t('docs.title')}</h1>

        <div className="grid gap-8 lg:grid-cols-[220px_1fr]">
          {/* Topic navigation */}
          <nav className="lg:sticky lg:top-6 lg:self-start">
            <ul className="space-y-1">
              {DOC_ARTICLES.map((a) => {
                const active = a.slug === article.slug
                return (
                  <li key={a.slug}>
                    <Link
                      to={`/docs/${a.slug}`}
                      className={`block rounded-lg px-3 py-2 text-sm transition ${
                        active ? 'bg-primary text-white font-semibold' : 'text-muted hover:bg-surface hover:text-foreground'
                      }`}
                    >
                      {a.title[lang]}
                    </Link>
                  </li>
                )
              })}
            </ul>
          </nav>

          {/* Article */}
          <article className="min-w-0 rounded-2xl border border-border bg-surface p-6 text-sm leading-relaxed sm:p-8">
            <Markdown>{article.body[lang]}</Markdown>
          </article>
        </div>
      </main>
    </div>
  )
}
