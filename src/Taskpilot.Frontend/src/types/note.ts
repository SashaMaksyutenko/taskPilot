/** A personal note (mirrors NoteDto). */
export interface Note {
  id: string
  title: string
  content: string
  color: string | null
  isPinned: boolean
  createdAt: string
  updatedAt: string | null
}

/** Input for creating/updating a note. */
export interface SaveNote {
  title: string
  content: string
  color?: string | null
  isPinned: boolean
}
