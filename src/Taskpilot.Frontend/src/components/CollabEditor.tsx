import { useEffect, useLayoutEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useCollab } from '../hooks/useCollab'

interface CollabEditorProps {
  /** Document id, e.g. `"task:{guid}"`. Null renders a plain (non-collaborative) field. */
  docId: string | null
  /** Current saved text (seeds the CRDT on first open, and the field when collaboration is off). */
  initialText: string
  /** Local user's presence identity. */
  user: { name: string; color: string }
  /** Whether this user may edit (Viewers get a read-only field). */
  canEdit: boolean
  /** Debounced plain-text persist to the task's description column (reuses the REST save). */
  onSave: (text: string) => void
  placeholder?: string
  rows?: number
}

const SAVE_DEBOUNCE_MS = 1200

/**
 * A textarea for a task description that edits collaboratively in real time when a docId is given:
 * text merges live via a Yjs CRDT (see {@link useCollab}) and other editors show as presence chips.
 * Plain text is still saved to the task's description column (debounced) so lists, search, exports
 * and Viewers see it.
 */
export default function CollabEditor({
  docId,
  initialText,
  user,
  canEdit,
  onSave,
  placeholder,
  rows = 4,
}: CollabEditorProps) {
  const { t } = useTranslation()
  const enabled = canEdit && !!docId
  const { text, applyLocal, setCursor, ready, peers } = useCollab({ docId, initialText, user, enabled })

  const areaRef = useRef<HTMLTextAreaElement>(null)
  const selRef = useRef<{ start: number; end: number }>({ start: 0, end: 0 })
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Preserve the caret across re-renders (a remote edit changes `value` and would otherwise
  // bounce the caret to the end).
  useLayoutEffect(() => {
    const el = areaRef.current
    if (el && document.activeElement === el) {
      const len = el.value.length
      el.setSelectionRange(Math.min(selRef.current.start, len), Math.min(selRef.current.end, len))
    }
  }, [text])

  useEffect(() => {
    return () => {
      if (saveTimer.current) clearTimeout(saveTimer.current)
    }
  }, [])

  const rememberSelection = (el: HTMLTextAreaElement) => {
    selRef.current = { start: el.selectionStart, end: el.selectionEnd }
    setCursor(el.selectionStart, el.selectionEnd)
  }

  const onChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = e.target.value
    rememberSelection(e.target)
    applyLocal(value)
    if (!canEdit) return
    if (saveTimer.current) clearTimeout(saveTimer.current)
    saveTimer.current = setTimeout(() => onSave(value), SAVE_DEBOUNCE_MS)
  }

  return (
    <div>
      <textarea
        ref={areaRef}
        value={text}
        onChange={onChange}
        onSelect={(e) => rememberSelection(e.currentTarget)}
        readOnly={!canEdit}
        rows={rows}
        placeholder={placeholder}
        className="w-full rounded-lg border border-border bg-canvas px-3 py-2 outline-none focus:border-primary read-only:opacity-70"
      />
      {enabled && ready && (
        <div className="mt-1 flex items-center gap-2 text-xs text-muted">
          {peers.length > 0 ? (
            <>
              <span className="flex -space-x-1.5">
                {peers.slice(0, 5).map((p) => (
                  <span
                    key={p.clientId}
                    title={p.name}
                    className="inline-flex h-5 w-5 items-center justify-center rounded-full border border-surface text-[10px] font-bold text-white"
                    style={{ backgroundColor: p.color }}
                  >
                    {p.name.charAt(0).toUpperCase()}
                  </span>
                ))}
              </span>
              <span>{t('collab.editingNow', { count: peers.length })}</span>
            </>
          ) : (
            <span className="inline-flex items-center gap-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
              {t('collab.live')}
            </span>
          )}
        </div>
      )}
    </div>
  )
}
