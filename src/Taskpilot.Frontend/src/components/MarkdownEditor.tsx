import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Markdown from './Markdown'

type Props = {
  value: string
  onChange: (value: string) => void
  placeholder?: string
  rows?: number
  className?: string
}

/**
 * A textarea with a small Markdown formatting toolbar. Each button wraps the current
 * selection (or inserts a placeholder) with the corresponding Markdown markers.
 */
export default function MarkdownEditor({ value, onChange, placeholder, rows = 3, className }: Props) {
  const { t } = useTranslation()
  const ref = useRef<HTMLTextAreaElement>(null)
  const [preview, setPreview] = useState(false)

  // Wrap the selection with before/after markers (using a placeholder when empty).
  const wrap = (before: string, after: string, placeholderText: string) => {
    const el = ref.current
    if (!el) return
    const start = el.selectionStart
    const end = el.selectionEnd
    const selected = value.slice(start, end) || placeholderText
    const next = value.slice(0, start) + before + selected + after + value.slice(end)
    onChange(next)
    requestAnimationFrame(() => {
      el.focus()
      el.setSelectionRange(start + before.length, start + before.length + selected.length)
    })
  }

  // Prefix each line of the selection (or the current line) with a marker.
  const prefixLines = (marker: string) => {
    const el = ref.current
    if (!el) return
    const start = el.selectionStart
    const end = el.selectionEnd
    const lineStart = value.lastIndexOf('\n', start - 1) + 1
    const block = value.slice(lineStart, end)
    const replaced = block
      .split('\n')
      .map((l) => marker + l)
      .join('\n')
    const next = value.slice(0, lineStart) + replaced + value.slice(end)
    onChange(next)
    requestAnimationFrame(() => {
      el.focus()
      el.setSelectionRange(lineStart, lineStart + replaced.length)
    })
  }

  const btn = 'rounded px-2 py-1 text-xs font-semibold text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700'

  const tab = (active: boolean) =>
    `rounded px-2 py-1 text-xs font-semibold ${
      active ? 'bg-slate-200 text-[#1E2A44] dark:bg-slate-700 dark:text-white' : 'text-slate-500 dark:text-slate-400'
    }`

  return (
    <div>
      <div className="mb-1 flex flex-wrap items-center gap-1">
        {/* Write / Preview tabs */}
        <button type="button" onClick={() => setPreview(false)} className={tab(!preview)}>{t('mdToolbar.write')}</button>
        <button type="button" onClick={() => setPreview(true)} className={tab(preview)}>{t('mdToolbar.preview')}</button>
        <span className="mx-1 h-4 w-px bg-slate-200 dark:bg-slate-600" />

        {!preview && (
          <>
            <button type="button" title={t('mdToolbar.heading')} onClick={() => prefixLines('## ')} className={`${btn} font-bold`}>H</button>
            <button type="button" title={t('mdToolbar.bold')} onClick={() => wrap('**', '**', 'bold')} className={`${btn} font-bold`}>B</button>
            <button type="button" title={t('mdToolbar.italic')} onClick={() => wrap('*', '*', 'italic')} className={`${btn} italic`}>I</button>
            <button type="button" title={t('mdToolbar.strike')} onClick={() => wrap('~~', '~~', 'text')} className={`${btn} line-through`}>S</button>
            <button type="button" title={t('mdToolbar.code')} onClick={() => wrap('`', '`', 'code')} className={`${btn} font-mono`}>{'</>'}</button>
            <button type="button" title={t('mdToolbar.codeBlock')} onClick={() => wrap('\n```\n', '\n```\n', 'code')} className={`${btn} font-mono`}>{'{ }'}</button>
            <button type="button" title={t('mdToolbar.link')} onClick={() => wrap('[', '](https://)', 'text')} className={btn}>🔗</button>
            <button type="button" title={t('mdToolbar.list')} onClick={() => prefixLines('- ')} className={btn}>•</button>
            <button type="button" title={t('mdToolbar.numbered')} onClick={() => prefixLines('1. ')} className={btn}>1.</button>
            <button type="button" title={t('mdToolbar.quote')} onClick={() => prefixLines('> ')} className={`${btn} font-mono`}>&gt;</button>
          </>
        )}
      </div>

      {preview ? (
        <div className={`${className} min-h-[5rem] overflow-auto`}>
          {value.trim() ? <Markdown>{value}</Markdown> : <span className="text-slate-400">{t('mdToolbar.nothingToPreview')}</span>}
        </div>
      ) : (
        <textarea
          ref={ref}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          rows={rows}
          className={className}
        />
      )}
    </div>
  )
}
