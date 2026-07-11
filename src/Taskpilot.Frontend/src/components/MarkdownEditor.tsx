import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fileService } from '../services/fileService'
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
  const fileRef = useRef<HTMLInputElement>(null)
  const [preview, setPreview] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [uploadPct, setUploadPct] = useState(0)

  // Insert text at the caret (or replace the selection).
  const insertAtCursor = (text: string) => {
    const el = ref.current
    const start = el?.selectionStart ?? value.length
    const end = el?.selectionEnd ?? value.length
    const next = value.slice(0, start) + text + value.slice(end)
    onChange(next)
    requestAnimationFrame(() => {
      el?.focus()
      const pos = start + text.length
      el?.setSelectionRange(pos, pos)
    })
  }

  const onPickImage = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = '' // allow re-selecting the same file
    if (!file) return
    setUploading(true)
    setUploadPct(0)
    try {
      const uploaded = await fileService.upload(file, setUploadPct)
      insertAtCursor(`![${file.name}](/api/files/${uploaded.id})\n`)
    } catch {
      // ignore upload failures
    } finally {
      setUploading(false)
    }
  }

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

  const btn = 'rounded px-2 py-1 text-xs font-semibold text-muted hover:bg-canvas'

  const tab = (active: boolean) =>
    `rounded px-2 py-1 text-xs font-semibold ${
      active ? 'bg-border text-primary dark:text-white' : 'text-muted'
    }`

  return (
    <div>
      <div className="mb-1 flex flex-wrap items-center gap-1">
        {/* Write / Preview tabs */}
        <button type="button" onClick={() => setPreview(false)} className={tab(!preview)}>{t('mdToolbar.write')}</button>
        <button type="button" onClick={() => setPreview(true)} className={tab(preview)}>{t('mdToolbar.preview')}</button>
        <span className="mx-1 h-4 w-px bg-border" />

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
            <button type="button" title={t('mdToolbar.image')} onClick={() => fileRef.current?.click()} disabled={uploading} className={`${btn} tabular-nums disabled:opacity-50`}>
              {uploading ? `${uploadPct}%` : '🖼️'}
            </button>
            <input ref={fileRef} type="file" accept="image/*" className="hidden" onChange={onPickImage} />
          </>
        )}
      </div>

      {preview ? (
        <div className={`${className} min-h-[5rem] overflow-auto`}>
          {value.trim() ? <Markdown>{value}</Markdown> : <span className="text-muted">{t('mdToolbar.nothingToPreview')}</span>}
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
