import api from '../lib/api'

export type BookmarkType = 'Task' | 'Topic' | 'Message'

/** A saved bookmark (mirrors BookmarkDto). */
export interface Bookmark {
  id: string
  type: BookmarkType
  entityId: string
  title: string
  link: string
  createdAt: string
}

/** REST calls for the current user's bookmarks. */
export const bookmarkService = {
  /** Lists the user's bookmarks, newest first. */
  getMine(): Promise<Bookmark[]> {
    return api.get<Bookmark[]>('/api/bookmarks').then((r) => r.data)
  },

  /** Adds or removes a bookmark; returns whether it is now bookmarked. */
  toggle(data: { type: BookmarkType; entityId: string; title: string; link: string }): Promise<boolean> {
    return api.post<{ bookmarked: boolean }>('/api/bookmarks/toggle', data).then((r) => r.data.bookmarked)
  },

  /** Removes a bookmark by id. */
  remove(bookmarkId: string): Promise<void> {
    return api.delete(`/api/bookmarks/${bookmarkId}`).then(() => undefined)
  },
}
