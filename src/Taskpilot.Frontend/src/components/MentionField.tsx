import { useRef, useState, type KeyboardEvent } from 'react'
import Avatar from './Avatar'

export type MentionCandidate = { id: string; name: string; avatarUrl?: string | null }

type FieldEl = HTMLInputElement | HTMLTextAreaElement

type Props = {
  value: string
  onChange: (value: string) => void
  candidates: MentionCandidate[]
  onKeyDown?: (e: KeyboardEvent<FieldEl>) => void
  placeholder?: string
  className?: string
  /** Render a multi-line textarea (default) or a single-line input. */
  multiline?: boolean
  rows?: number
}

// The @token currently being typed, if the caret sits right after it.
const ACTIVE_TOKEN = /@([A-Za-z0-9_]*)$/

/**
 * A text field (textarea or input) with @mention autocomplete. As the user types
 * "@…", a dropdown of matching candidates appears; selecting one inserts "@Name"
 * (name without spaces, matching how the backend resolves mentions).
 */
export default function MentionField({
  value,
  onChange,
  candidates,
  onKeyDown,
  placeholder,
  className,
  multiline = true,
  rows,
}: Props) {
  const ref = useRef<FieldEl>(null)
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)

  const matches = open
    ? candidates
        .filter((c) => c.name.replace(/\s+/g, '').toLowerCase().startsWith(query.toLowerCase()))
        .slice(0, 6)
    : []

  const detect = (text: string, caret: number) => {
    const m = text.slice(0, caret).match(ACTIVE_TOKEN)
    if (m) {
      setQuery(m[1])
      setActive(0)
      setOpen(true)
    } else {
      setOpen(false)
    }
  }

  const handleChange = (e: React.ChangeEvent<FieldEl>) => {
    onChange(e.target.value)
    detect(e.target.value, e.target.selectionStart ?? e.target.value.length)
  }

  const insert = (c: MentionCandidate) => {
    const el = ref.current
    const caret = el?.selectionStart ?? value.length
    const before = value.slice(0, caret).replace(ACTIVE_TOKEN, `@${c.name.replace(/\s+/g, '')} `)
    const next = before + value.slice(caret)
    onChange(next)
    setOpen(false)
    requestAnimationFrame(() => {
      el?.focus()
      el?.setSelectionRange(before.length, before.length)
    })
  }

  const handleKeyDown = (e: KeyboardEvent<FieldEl>) => {
    if (open && matches.length > 0) {
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        setActive((a) => (a + 1) % matches.length)
        return
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault()
        setActive((a) => (a - 1 + matches.length) % matches.length)
        return
      }
      if (e.key === 'Tab' || (e.key === 'Enter' && !e.ctrlKey && !e.metaKey)) {
        e.preventDefault()
        insert(matches[active])
        return
      }
      if (e.key === 'Escape') {
        setOpen(false)
        return
      }
    }
    onKeyDown?.(e)
  }

  const shared = {
    value,
    onChange: handleChange,
    onKeyDown: handleKeyDown,
    onBlur: () => setOpen(false),
    placeholder,
    className,
  }

  return (
    <div className="relative flex-1">
      {multiline ? (
        <textarea ref={ref as React.RefObject<HTMLTextAreaElement>} rows={rows} {...shared} />
      ) : (
        <input ref={ref as React.RefObject<HTMLInputElement>} {...shared} />
      )}
      {open && matches.length > 0 && (
        <ul className="absolute bottom-full left-0 z-30 mb-1 max-h-56 w-full overflow-y-auto rounded-lg border border-border bg-canvas shadow-lg">
          {matches.map((c, i) => (
            <li key={c.id}>
              <button
                type="button"
                // onMouseDown so the click lands before the field blur.
                onMouseDown={(e) => {
                  e.preventDefault()
                  insert(c)
                }}
                className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm ${
                  i === active ? 'bg-canvas' : 'hover:bg-canvas/60'
                }`}
              >
                <Avatar name={c.name} src={c.avatarUrl} size={22} />
                <span className="truncate">{c.name}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
