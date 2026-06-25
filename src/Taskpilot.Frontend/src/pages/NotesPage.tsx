import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Navbar from '../components/Navbar'
import { noteService } from '../services/noteService'
import type { Note } from '../types/note'

// A small palette for colour-tagging notes.
const COLORS = ['#FDE68A', '#BFDBFE', '#BBF7D0', '#FBCFE8', '#E5E7EB']

/**
 * Personal notes: a grid of colour-tagged, pinnable cards with create/edit/delete.
 */
export default function NotesPage() {
  const { t } = useTranslation()
  const [notes, setNotes] = useState<Note[]>([])
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [color, setColor] = useState<string | null>(COLORS[0])
  const [editingId, setEditingId] = useState<string | null>(null)

  const load = () => {
    noteService.getMine().then(setNotes).catch(() => {})
  }
  useEffect(load, [])

  const resetForm = () => {
    setTitle('')
    setContent('')
    setColor(COLORS[0])
    setEditingId(null)
  }

  const save = async () => {
    if (!title.trim() && !content.trim()) return
    const data = { title: title.trim(), content: content.trim(), color, isPinned: false }
    if (editingId) {
      // Keep the note's pinned state when editing.
      const current = notes.find((n) => n.id === editingId)
      await noteService.update(editingId, { ...data, isPinned: current?.isPinned ?? false }).catch(() => {})
    } else {
      await noteService.create(data).catch(() => {})
    }
    resetForm()
    load()
  }

  const startEdit = (note: Note) => {
    setEditingId(note.id)
    setTitle(note.title)
    setContent(note.content)
    setColor(note.color ?? COLORS[0])
  }

  const togglePin = async (note: Note) => {
    await noteService
      .update(note.id, {
        title: note.title,
        content: note.content,
        color: note.color,
        isPinned: !note.isPinned,
      })
      .catch(() => {})
    load()
  }

  const remove = async (id: string) => {
    await noteService.remove(id).catch(() => {})
    if (editingId === id) resetForm()
    load()
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">{t('notes.title')}</h1>

        {/* Create / edit form */}
        <div className="mb-8 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t('notes.titlePlaceholder')}
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-medium outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder={t('notes.contentPlaceholder')}
            rows={3}
            className="mb-3 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />
          <div className="flex items-center gap-3">
            <div className="flex gap-1.5">
              {COLORS.map((c) => (
                <button
                  key={c}
                  onClick={() => setColor(c)}
                  className={`h-6 w-6 rounded-full border-2 ${color === c ? 'border-[#1E2A44] dark:border-white' : 'border-transparent'}`}
                  style={{ background: c }}
                  aria-label={c}
                />
              ))}
            </div>
            <button
              onClick={save}
              className="ml-auto rounded-lg bg-[#1E2A44] px-5 py-2 text-sm font-semibold text-white hover:bg-[#27345a]"
            >
              {editingId ? t('notes.save') : t('notes.add')}
            </button>
            {editingId && (
              <button onClick={resetForm} className="text-sm font-semibold text-slate-500 hover:underline">
                {t('notes.cancel')}
              </button>
            )}
          </div>
        </div>

        {/* Notes grid */}
        {notes.length === 0 ? (
          <p className="text-slate-400">{t('notes.empty')}</p>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {notes.map((note) => (
              <div
                key={note.id}
                className="flex flex-col rounded-xl border border-slate-200 p-4 shadow-sm dark:border-slate-700"
                style={{ background: note.color ?? undefined }}
              >
                <div className="mb-1 flex items-start gap-2">
                  {note.title && <h3 className="flex-1 font-bold text-[#1E2A44]">{note.title}</h3>}
                  <button
                    onClick={() => togglePin(note)}
                    title="Pin"
                    className={`flex-none text-sm ${note.isPinned ? '' : 'opacity-40'}`}
                  >
                    📌
                  </button>
                </div>
                {note.content && (
                  <p className="flex-1 whitespace-pre-wrap break-words text-sm text-slate-700">{note.content}</p>
                )}
                <div className="mt-3 flex gap-3 text-xs font-semibold">
                  <button onClick={() => startEdit(note)} className="text-[#1E2A44] hover:underline">
                    {t('notes.edit')}
                  </button>
                  <button onClick={() => remove(note.id)} className="text-red-600 hover:underline">
                    {t('notes.delete')}
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  )
}
