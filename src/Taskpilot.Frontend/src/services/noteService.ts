import api from '../lib/api'
import type { Note, SaveNote } from '../types/note'

/** REST calls for the current user's personal notes. */
export const noteService = {
  getMine(): Promise<Note[]> {
    return api.get<Note[]>('/api/notes').then((r) => r.data)
  },

  create(data: SaveNote): Promise<Note> {
    return api.post<Note>('/api/notes', data).then((r) => r.data)
  },

  update(id: string, data: SaveNote): Promise<Note> {
    return api.put<Note>(`/api/notes/${id}`, data).then((r) => r.data)
  },

  remove(id: string): Promise<void> {
    return api.delete(`/api/notes/${id}`).then(() => undefined)
  },

  /** Downloads a note as a PDF blob. */
  exportPdf(id: string): Promise<Blob> {
    return api.get(`/api/notes/${id}/pdf`, { responseType: 'blob' }).then((r) => r.data as Blob)
  },
}
