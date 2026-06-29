import api from '../lib/api'
import type { User } from '../types/auth'
import type { Appeal, Warning } from '../types/admin'

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
  avatarUrl?: string | null
}

/** Public profile of any user (mirrors PublicProfileDto). */
export interface PublicProfile {
  id: string
  name: string
  role: string
  avatarUrl?: string | null
  title?: string | null
  bio?: string | null
  location?: string | null
  email?: string | null
  website?: string | null
  linkedIn?: string | null
  github?: string | null
  phone?: string | null
  memberSince: string
  averageRating: number | null
  reviewCount: number
  reputationPoints: number
  badges: string[]
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

  getPublicProfile(userId: string): Promise<PublicProfile> {
    return api.get<PublicProfile>(`/api/users/${userId}`).then((r) => r.data)
  },

  changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return api
      .post('/api/users/me/change-password', { currentPassword, newPassword })
      .then(() => undefined)
  },

  /** Uploads/replaces the current user's avatar; returns the updated profile. */
  uploadAvatar(file: File): Promise<User> {
    const form = new FormData()
    form.append('file', file)
    return api
      .post<User>('/api/users/me/avatar', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      .then((r) => r.data)
  },

  /** Removes the current user's avatar; returns the updated profile. */
  removeAvatar(): Promise<User> {
    return api.delete<User>('/api/users/me/avatar').then((r) => r.data)
  },

  /** Lists the current user's moderation warnings (newest first). */
  getMyWarnings(): Promise<Warning[]> {
    return api.get<Warning[]>('/api/users/me/warnings').then((r) => r.data)
  },

  /** Lists the current user's appeals (newest first). */
  getMyAppeals(): Promise<Appeal[]> {
    return api.get<Appeal[]>('/api/users/me/appeals').then((r) => r.data)
  },

  /** Files an appeal against a warning. */
  createAppeal(data: { warningId?: string; message: string }): Promise<Appeal> {
    return api.post<Appeal>('/api/users/me/appeals', data).then((r) => r.data)
  },
}
