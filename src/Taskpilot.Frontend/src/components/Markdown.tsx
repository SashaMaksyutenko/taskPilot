import ReactMarkdown from 'react-markdown'

/**
 * Renders user Markdown safely (react-markdown outputs React elements, not raw HTML)
 * with Tailwind-styled elements. Supports bold/italic, lists, links, code, quotes and
 * small headings — enough for forum posts.
 */
export default function Markdown({ children }: { children: string }) {
  return (
    <div className="text-sm leading-relaxed text-slate-700 dark:text-slate-200">
      <ReactMarkdown
        components={{
          p: ({ children }) => <p className="mb-2 whitespace-pre-wrap break-words last:mb-0">{children}</p>,
          strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
          em: ({ children }) => <em className="italic">{children}</em>,
          a: ({ children, href }) => (
            <a href={href} target="_blank" rel="noreferrer" className="text-[#1E2A44] underline hover:no-underline dark:text-sky-300">
              {children}
            </a>
          ),
          ul: ({ children }) => <ul className="mb-2 ml-5 list-disc space-y-0.5 last:mb-0">{children}</ul>,
          ol: ({ children }) => <ol className="mb-2 ml-5 list-decimal space-y-0.5 last:mb-0">{children}</ol>,
          li: ({ children }) => <li>{children}</li>,
          h1: ({ children }) => <h1 className="mb-2 text-lg font-bold">{children}</h1>,
          h2: ({ children }) => <h2 className="mb-2 text-base font-bold">{children}</h2>,
          h3: ({ children }) => <h3 className="mb-1 font-bold">{children}</h3>,
          blockquote: ({ children }) => (
            <blockquote className="mb-2 border-l-2 border-slate-300 pl-3 text-slate-500 dark:border-slate-600 dark:text-slate-400">
              {children}
            </blockquote>
          ),
          code: ({ children }) => (
            <code className="rounded bg-slate-100 px-1 py-0.5 font-mono text-[0.85em] dark:bg-slate-700">{children}</code>
          ),
          pre: ({ children }) => (
            <pre className="mb-2 overflow-x-auto rounded-lg bg-slate-100 p-3 font-mono text-xs dark:bg-slate-900">{children}</pre>
          ),
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  )
}
