import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import EmptyState from '../components/EmptyState'
import NoteContextMenu from '../components/NoteContextMenu'
import ConfirmDialog from '../components/ConfirmDialog'
import { noteService } from '../services/noteService'
import { notify } from '../lib/toast'
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
  const [tags, setTags] = useState<string[]>([])
  const [tagInput, setTagInput] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  // Filters over the notes grid.
  const [search, setSearch] = useState('')
  const [activeTags, setActiveTags] = useState<string[]>([])
  // Note awaiting delete confirmation.
  const [deletingNote, setDeletingNote] = useState<Note | null>(null)

  const load = () => {
    noteService.getMine().then(setNotes).catch(() => {})
  }
  useEffect(load, [])

  const resetForm = () => {
    setTitle('')
    setContent('')
    setColor(COLORS[0])
    setTags([])
    setTagInput('')
    setEditingId(null)
  }

  const addTag = () => {
    const tag = tagInput.trim()
    if (!tag) return
    // Case-insensitive dedupe; cap the list at 15.
    if (!tags.some((x) => x.toLowerCase() === tag.toLowerCase()) && tags.length < 15) {
      setTags([...tags, tag.slice(0, 30)])
    }
    setTagInput('')
  }

  const removeTag = (tag: string) => setTags(tags.filter((x) => x !== tag))

  const save = async () => {
    if (!title.trim() && !content.trim()) return
    const data = { title: title.trim(), content: content.trim(), color, isPinned: false, tags }
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
    setTags(note.tags ?? [])
  }

  const togglePin = async (note: Note) => {
    await noteService
      .update(note.id, {
        title: note.title,
        content: note.content,
        color: note.color,
        isPinned: !note.isPinned,
        tags: note.tags,
      })
      .catch(() => {})
    load()
  }

  const remove = async (id: string) => {
    await noteService.remove(id).catch(() => {})
    if (editingId === id) resetForm()
    load()
  }

  const exportPdf = async (note: Note) => {
    const blob = await noteService.exportPdf(note.id).catch(() => null)
    if (!blob) {
      notify.error(t('toast.actionFailed'))
      return
    }
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${note.title.trim() || 'note'}.pdf`
    a.click()
    URL.revokeObjectURL(url)
    notify.success(t('toast.pdfReady'))
  }

  // All distinct tags across notes, alphabetical, for the filter bar.
  const allTags = Array.from(new Set(notes.flatMap((n) => n.tags))).sort((a, b) => a.localeCompare(b))

  const toggleFilterTag = (tag: string) =>
    setActiveTags((prev) => (prev.includes(tag) ? prev.filter((x) => x !== tag) : [...prev, tag]))

  // Notes shown after applying the text search and tag filter.
  const q = search.trim().toLowerCase()
  const visibleNotes = notes.filter((n) => {
    const matchesText = !q || n.title.toLowerCase().includes(q) || n.content.toLowerCase().includes(q)
    const matchesTags = activeTags.length === 0 || n.tags.some((tag) => activeTags.includes(tag))
    return matchesText && matchesTags
  })

  return (
    <div className="mx-auto max-w-5xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">{t('notes.title')}</h1>

        {/* Create / edit form */}
        <div className="mb-8 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t('notes.titlePlaceholder')}
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-medium outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
          />
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder={t('notes.contentPlaceholder')}
            rows={3}
            className="mb-3 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
          />
          {/* Tags */}
          <div className="mb-3">
            {tags.length > 0 && (
              <div className="mb-2 flex flex-wrap gap-1.5">
                {tags.map((tag) => (
                  <span
                    key={tag}
                    className="flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary dark:bg-slate-700 dark:text-slate-200"
                  >
                    {tag}
                    <button onClick={() => removeTag(tag)} className="text-slate-400 hover:text-red-600">
                      ✕
                    </button>
                  </span>
                ))}
              </div>
            )}
            <input
              value={tagInput}
              onChange={(e) => setTagInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ',') {
                  e.preventDefault()
                  addTag()
                } else if (e.key === 'Backspace' && !tagInput && tags.length > 0) {
                  removeTag(tags[tags.length - 1])
                }
              }}
              onBlur={addTag}
              placeholder={t('notes.tagsPlaceholder')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
            />
          </div>

          <div className="flex items-center gap-3">
            <div className="flex gap-1.5">
              {COLORS.map((c) => (
                <button
                  key={c}
                  onClick={() => setColor(c)}
                  className={`h-6 w-6 rounded-full border-2 ${color === c ? 'border-primary dark:border-white' : 'border-transparent'}`}
                  style={{ background: c }}
                  aria-label={c}
                />
              ))}
            </div>
            <button
              onClick={save}
              className="ml-auto rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-hover"
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

        {/* Filters: text search + tag chips */}
        {notes.length > 0 && (
          <div className="mb-5 space-y-2">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('notes.searchPlaceholder')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-800"
            />
            {allTags.length > 0 && (
              <div className="flex flex-wrap items-center gap-1.5">
                {allTags.map((tag) => {
                  const active = activeTags.includes(tag)
                  return (
                    <button
                      key={tag}
                      onClick={() => toggleFilterTag(tag)}
                      className={`rounded-full px-2 py-0.5 text-xs font-medium transition ${
                        active
                          ? 'bg-primary text-white dark:bg-slate-200 dark:text-slate-900'
                          : 'bg-primary/10 text-primary hover:bg-primary/20 dark:bg-slate-700 dark:text-slate-200'
                      }`}
                    >
                      {tag}
                    </button>
                  )
                })}
                {activeTags.length > 0 && (
                  <button
                    onClick={() => setActiveTags([])}
                    className="ml-1 text-xs font-semibold text-slate-400 hover:text-red-600 hover:underline"
                  >
                    {t('notes.clearFilter')}
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        {/* Notes grid */}
        {notes.length === 0 ? (
          <EmptyState message={t('notes.empty')} />
        ) : visibleNotes.length === 0 ? (
          <EmptyState message={t('notes.noMatches')} />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {visibleNotes.map((note) => (
              <NoteContextMenu
                key={note.id}
                isPinned={note.isPinned}
                content={note.content}
                onTogglePin={() => togglePin(note)}
                onEdit={() => startEdit(note)}
                onExportPdf={() => exportPdf(note)}
                onDelete={() => setDeletingNote(note)}
              >
              <div
                className="flex flex-col rounded-xl border border-slate-200 p-4 shadow-sm dark:border-slate-700"
                style={{ background: note.color ?? undefined }}
              >
                <div className="mb-1 flex items-start gap-2">
                  {note.title && <h3 className="flex-1 font-bold text-primary">{note.title}</h3>}
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
                {note.tags.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-1">
                    {note.tags.map((tag) => (
                      <span key={tag} className="rounded-full bg-black/10 px-1.5 py-0.5 text-[10px] font-medium text-primary">
                        {tag}
                      </span>
                    ))}
                  </div>
                )}
                <div className="mt-3 flex gap-3 text-xs font-semibold">
                  <button onClick={() => startEdit(note)} className="text-primary hover:underline">
                    {t('notes.edit')}
                  </button>
                  <button onClick={() => exportPdf(note)} className="text-primary hover:underline">
                    {t('notes.exportPdf')}
                  </button>
                  <button onClick={() => setDeletingNote(note)} className="text-red-600 hover:underline">
                    {t('notes.delete')}
                  </button>
                </div>
              </div>
              </NoteContextMenu>
            ))}
          </div>
        )}

        {/* Note delete confirmation */}
        <ConfirmDialog
          open={!!deletingNote}
          title={t('notes.deleteTitle')}
          message={t('notes.deleteConfirm')}
          onConfirm={() => {
            if (deletingNote) remove(deletingNote.id)
            setDeletingNote(null)
          }}
          onCancel={() => setDeletingNote(null)}
        />
      </div>
  )
}
