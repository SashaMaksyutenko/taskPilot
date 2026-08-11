import api from '../lib/api'

/** A sticky note (mirrors WhiteboardNoteDto). */
export interface Note {
  id: string
  x: number
  y: number
  text: string
  color: string
  authorId: string
  authorName: string
  editedById: string | null
  editedByName: string | null
}

export interface CreateNote {
  x: number
  y: number
  text?: string
  color?: string
}

export interface UpdateNote {
  x?: number
  y?: number
  text?: string
  color?: string
}

export const whiteboardService = {
  getNotes(projectId: string): Promise<Note[]> {
    return api.get<Note[]>(`/api/projects/${projectId}/whiteboard/notes`).then((r) => r.data)
  },

  createNote(projectId: string, note: CreateNote): Promise<Note> {
    return api.post<Note>(`/api/projects/${projectId}/whiteboard/notes`, note).then((r) => r.data)
  },

  updateNote(noteId: string, patch: UpdateNote): Promise<Note> {
    return api.put<Note>(`/api/whiteboard/notes/${noteId}`, patch).then((r) => r.data)
  },

  /** Deletes a note. Resolves true on success, false when the server forbids it (not your note). */
  deleteNote(noteId: string): Promise<boolean> {
    return api
      .delete(`/api/whiteboard/notes/${noteId}`)
      .then(() => true)
      .catch((e) => {
        if (e?.response?.status === 403) return false
        throw e
      })
  },
}
