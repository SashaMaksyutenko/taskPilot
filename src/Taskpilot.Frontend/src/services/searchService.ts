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

/** Global search across the user's projects/tasks and public forum/users. */
export const searchService = {
  search(q: string): Promise<SearchResults> {
    return api.get<SearchResults>('/api/search', { params: { q } }).then((r) => r.data)
  },
}
