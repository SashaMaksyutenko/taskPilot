import api from '../lib/api'

export interface SearchItem {
  id: string
  label: string
  sublabel: string | null
  avatarUrl: string | null
}

export interface SearchResults {
  projects: SearchItem[]
  tasks: SearchItem[]
  topics: SearchItem[]
  users: SearchItem[]
}

/** A saved search query (mirrors the backend SavedSearchDto). */
export interface SavedSearch {
  id: string
  name: string
  query: string
  createdAt: string
}

/** Global search across the user's projects/tasks and public forum/users. */
export const searchService = {
  search(q: string): Promise<SearchResults> {
    return api.get<SearchResults>('/api/search', { params: { q } }).then((r) => r.data)
  },

  /** Lists the user's saved searches (newest first). */
  getSaved(): Promise<SavedSearch[]> {
    return api.get<SavedSearch[]>('/api/search/saved').then((r) => r.data)
  },

  /** Saves a search query for quick re-running. */
  saveSearch(name: string, query: string): Promise<SavedSearch> {
    return api.post<SavedSearch>('/api/search/saved', { name, query }).then((r) => r.data)
  },

  /** Deletes a saved search. */
  deleteSaved(id: string): Promise<void> {
    return api.delete(`/api/search/saved/${id}`).then(() => undefined)
  },
}
