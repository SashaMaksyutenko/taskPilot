import { Plus, Trash2 } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useWhiteboard, type Note } from '../hooks/useWhiteboard'

const NOTE_COLORS = ['#fde68a', '#bbf7d0', '#bfdbfe', '#fbcfe8', '#fecaca', '#ddd6fe']
const NOTE_W = 160
const NOTE_H = 120

interface WhiteboardCanvasProps {
  docId: string
  user: { name: string; color: string }
  canEdit: boolean
}

/**
 * A collaborative sticky-note whiteboard. Notes and cursors sync live over the CRDT collab hub
 * ({@link useWhiteboard}). Double-click to add a note, drag to move, click to edit, and you'll see
 * other editors' cursors move in real time.
 */
export default function WhiteboardCanvas({ docId, user, canEdit }: WhiteboardCanvasProps) {
  const { t } = useTranslation()
  const { notes, ready, peers, addNote, updateNote, removeNote, setCursor } = useWhiteboard({
    docId,
    user,
    enabled: true,
  })

  const areaRef = useRef<HTMLDivElement>(null)
  const [color, setColor] = useState(NOTE_COLORS[0])
  // Which note is being dragged, and the grab offset within it.
  const drag = useRef<{ id: string; dx: number; dy: number } | null>(null)

  const toCanvas = (e: { clientX: number; clientY: number }) => {
    const rect = areaRef.current?.getBoundingClientRect()
    return { x: e.clientX - (rect?.left ?? 0), y: e.clientY - (rect?.top ?? 0) }
  }

  const createNote = (x: number, y: number) => {
    if (!canEdit) return
    const note: Note = {
      id: crypto.randomUUID(),
      x: Math.max(0, x - NOTE_W / 2),
      y: Math.max(0, y - NOTE_H / 2),
      text: '',
      color,
    }
    addNote(note)
  }

  const onAreaDoubleClick = (e: React.MouseEvent) => {
    if (e.target !== areaRef.current) return // only on empty canvas
    const { x, y } = toCanvas(e)
    createNote(x, y)
  }

  const onPointerMove = (e: React.PointerEvent) => {
    const pos = toCanvas(e)
    setCursor(pos)
    if (drag.current) {
      updateNote(drag.current.id, {
        x: Math.max(0, pos.x - drag.current.dx),
        y: Math.max(0, pos.y - drag.current.dy),
      })
    }
  }

  const startDrag = (e: React.PointerEvent, note: Note) => {
    if (!canEdit) return
    const pos = toCanvas(e)
    drag.current = { id: note.id, dx: pos.x - note.x, dy: pos.y - note.y }
    ;(e.target as HTMLElement).setPointerCapture?.(e.pointerId)
  }
  const endDrag = () => {
    drag.current = null
  }

  return (
    <div className="flex h-full flex-col">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-3 border-b border-border px-4 py-2">
        <button
          onClick={() => {
            const rect = areaRef.current?.getBoundingClientRect()
            createNote((rect?.width ?? 400) / 2, (rect?.height ?? 300) / 2)
          }}
          disabled={!canEdit}
          className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-3 py-1.5 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-50"
        >
          <Plus className="h-4 w-4" />
          {t('whiteboard.addNote')}
        </button>

        <div className="flex items-center gap-1.5">
          {NOTE_COLORS.map((c) => (
            <button
              key={c}
              onClick={() => setColor(c)}
              aria-label={c}
              className={`h-6 w-6 rounded-full border transition ${color === c ? 'ring-2 ring-foreground ring-offset-1 ring-offset-surface' : 'border-border hover:scale-110'}`}
              style={{ background: c }}
            />
          ))}
        </div>

        {/* Presence */}
        <div className="ml-auto flex items-center gap-2 text-xs text-muted">
          {ready && (
            <span className="inline-flex items-center gap-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
              {peers.length > 0 ? t('whiteboard.editingNow', { count: peers.length }) : t('collab.live')}
            </span>
          )}
          <span className="flex -space-x-1.5">
            {peers.slice(0, 6).map((p) => (
              <span
                key={p.clientId}
                title={p.name}
                className="inline-flex h-6 w-6 items-center justify-center rounded-full border border-surface text-[10px] font-bold text-white"
                style={{ backgroundColor: p.color }}
              >
                {p.name.charAt(0).toUpperCase()}
              </span>
            ))}
          </span>
        </div>
      </div>

      {/* Canvas */}
      <div
        ref={areaRef}
        onDoubleClick={onAreaDoubleClick}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerLeave={() => setCursor(null)}
        className="relative flex-1 overflow-hidden bg-canvas"
        style={{ touchAction: 'none' }}
      >
        {!ready && (
          <div className="absolute inset-0 flex items-center justify-center text-sm text-muted">{t('whiteboard.loading')}</div>
        )}
        {ready && notes.length === 0 && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-sm text-muted">
            {t('whiteboard.empty')}
          </div>
        )}

        {notes.map((note) => (
          <div
            key={note.id}
            onPointerDown={(e) => startDrag(e, note)}
            className="group absolute flex flex-col rounded-md p-2 shadow-md"
            style={{ left: note.x, top: note.y, width: NOTE_W, height: NOTE_H, background: note.color, touchAction: 'none' }}
          >
            <textarea
              value={note.text}
              onChange={(e) => updateNote(note.id, { text: e.target.value })}
              onPointerDown={(e) => e.stopPropagation()} // let the caret work without starting a drag
              readOnly={!canEdit}
              placeholder={t('whiteboard.notePlaceholder')}
              className="h-full w-full resize-none bg-transparent text-sm text-neutral-900 outline-none placeholder:text-neutral-500"
            />
            {canEdit && (
              <button
                onClick={() => removeNote(note.id)}
                onPointerDown={(e) => e.stopPropagation()}
                className="absolute -right-2 -top-2 hidden h-5 w-5 items-center justify-center rounded-full bg-neutral-800 text-white group-hover:flex"
                aria-label={t('whiteboard.deleteNote')}
              >
                <Trash2 className="h-3 w-3" />
              </button>
            )}
          </div>
        ))}

        {/* Remote cursors */}
        {peers.map(
          (p) =>
            p.cursor && (
              <div
                key={`cursor-${p.clientId}`}
                className="pointer-events-none absolute z-10 -translate-x-1 -translate-y-1"
                style={{ left: p.cursor.x, top: p.cursor.y }}
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill={p.color} stroke="white" strokeWidth="1.5">
                  <path d="M5 3l14 7-6 2-2 6-6-15z" />
                </svg>
                <span
                  className="ml-3 rounded px-1 py-0.5 text-[10px] font-semibold text-white"
                  style={{ background: p.color }}
                >
                  {p.name}
                </span>
              </div>
            ),
        )}
      </div>
    </div>
  )
}
