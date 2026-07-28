import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Markdown from '../components/Markdown'
import PublicPageHeader from '../components/PublicPageHeader'
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
      <PublicPageHeader />

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
