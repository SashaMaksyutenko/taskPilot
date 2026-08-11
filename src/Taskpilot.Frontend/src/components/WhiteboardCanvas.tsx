import { Plus, Trash2 } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { notify } from '../lib/toast'
import { useWhiteboard } from '../hooks/useWhiteboard'

const NOTE_COLORS = ['#fde68a', '#bbf7d0', '#bfdbfe', '#fbcfe8', '#fecaca', '#ddd6fe']
const NOTE_W = 160
const NOTE_H = 120

interface WhiteboardCanvasProps {
  projectId: string
  user: { id: string; name: string; color: string }
  canEdit: boolean
  /** The project owner may delete any note; everyone else only their own. */
  isOwner: boolean
}

/**
 * A collaborative sticky-note whiteboard. Notes are authoritative server records (see
 * {@link useWhiteboard}); create/move/edit/delete sync live, and you'll see other editors' cursors
 * move in real time. Deleting someone else's note is refused by the server.
 */
export default function WhiteboardCanvas({ projectId, user, canEdit, isOwner }: WhiteboardCanvasProps) {
  const { t } = useTranslation()
  const { notes, ready, peers, addNote, editText, moveNote, commitMove, removeNote, setCursor } = useWhiteboard({
    projectId,
    user,
    enabled: true,
  })

  const areaRef = useRef<HTMLDivElement>(null)
  const [color, setColor] = useState(NOTE_COLORS[0])
  // The note being dragged, its grab offset, and its latest position (committed on drop).
  const drag = useRef<{ id: string; dx: number; dy: number; x: number; y: number } | null>(null)

  const toCanvas = (e: { clientX: number; clientY: number }) => {
    const rect = areaRef.current?.getBoundingClientRect()
    return { x: e.clientX - (rect?.left ?? 0), y: e.clientY - (rect?.top ?? 0) }
  }

  const createNote = (x: number, y: number) => {
    if (!canEdit) return
    addNote(Math.max(0, x - NOTE_W / 2), Math.max(0, y - NOTE_H / 2), color)
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
      const x = Math.max(0, pos.x - drag.current.dx)
      const y = Math.max(0, pos.y - drag.current.dy)
      drag.current.x = x
      drag.current.y = y
      moveNote(drag.current.id, x, y)
    }
  }

  const startDrag = (e: React.PointerEvent, note: { id: string; x: number; y: number }) => {
    if (!canEdit) return
    const pos = toCanvas(e)
    drag.current = { id: note.id, dx: pos.x - note.x, dy: pos.y - note.y, x: note.x, y: note.y }
    ;(e.target as HTMLElement).setPointerCapture?.(e.pointerId)
  }
  const endDrag = () => {
    if (drag.current) commitMove(drag.current.id, drag.current.x, drag.current.y) // persist final position
    drag.current = null
  }

  const onDelete = async (id: string) => {
    const ok = await removeNote(id)
    if (!ok) notify.error(t('whiteboard.deleteForbidden'))
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
                key={p.connectionId}
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

        {notes.map((note) => {
          const editedByOther = note.editedById && note.editedById !== note.authorId
          // The author (or, for moderation, the project owner) may delete. The server enforces this
          // too — this just hides the button for others.
          const canDelete = canEdit && (note.authorId === user.id || isOwner)
          return (
            <div
              key={note.id}
              onPointerDown={(e) => startDrag(e, note)}
              className="group absolute flex flex-col rounded-md p-2 shadow-md"
              style={{ left: note.x, top: note.y, width: NOTE_W, height: NOTE_H, background: note.color, touchAction: 'none' }}
            >
              <textarea
                value={note.text}
                onChange={(e) => editText(note.id, e.target.value)}
                onPointerDown={(e) => e.stopPropagation()} // let the caret work without starting a drag
                readOnly={!canEdit}
                placeholder={t('whiteboard.notePlaceholder')}
                className="w-full flex-1 resize-none bg-transparent text-sm text-neutral-900 outline-none placeholder:text-neutral-500"
              />
              {/* Attribution: author, plus who last edited when that's someone else. */}
              {note.authorName && (
                <div className="flex items-center justify-between text-[10px] text-neutral-600">
                  <span className="truncate">{note.authorName}</span>
                  {editedByOther && <span className="ml-1 truncate italic">{t('whiteboard.editedBy', { name: note.editedByName })}</span>}
                </div>
              )}
              {canDelete && (
                <button
                  onClick={() => onDelete(note.id)}
                  onPointerDown={(e) => e.stopPropagation()}
                  className="absolute -right-2 -top-2 hidden h-5 w-5 items-center justify-center rounded-full bg-neutral-800 text-white group-hover:flex"
                  aria-label={t('whiteboard.deleteNote')}
                >
                  <Trash2 className="h-3 w-3" />
                </button>
              )}
            </div>
          )
        })}

        {/* Remote cursors */}
        {peers.map(
          (p) =>
            p.cursor && (
              <div
                key={`cursor-${p.connectionId}`}
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
