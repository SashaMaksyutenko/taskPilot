import api from '../lib/api'
import type { User } from '../types/auth'

export interface UpdateProfileData {
  name: string
  title?: string
  bio?: string
  location?: string
  website?: string
  linkedIn?: string
  github?: string
  phone?: string
  showEmail: boolean
}

/** Minimal user info returned by the search endpoint. */
export interface UserSearchResult {
  id: string
  name: string
  title?: string | null
}

/** REST calls for the current user's account (profile, password). */
export const userService = {
  updateProfile(data: UpdateProfileData): Promise<User> {
    return api.put<User>('/api/users/me', data).then((r) => r.data)
  },

  searchUsers(query: string): Promise<UserSearchResult[]> {
    return api
      .get<UserSearchResult[]>('/api/users/search', { params: { q: query } })
      .then((r) => r.data)
  },

  changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return api
      .post('/api/users/me/change-password', { currentPassword, newPassword })
      .then(() => undefined)
  },
}
