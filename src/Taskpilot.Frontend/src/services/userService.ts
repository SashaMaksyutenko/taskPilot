import api from '../lib/api'
import type { User } from '../types/auth'
import type { Appeal, Warning } from '../types/admin'

export interface UpdateProfileData {
  name: string
  title?: string
  bio?: string
  location?: string
  /** Skill tags; the server normalizes and de-duplicates them. */
  skills?: string[]
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
  /** Skill tags shown on the profile. */
  skills: string[]
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

/** One line in the reputation history. */
export interface ReputationEntry {
  id: string
  delta: number
  reason: string
  description: string
  createdAt: string
}

/** Reputation history plus the ledger's running total. */
export interface ReputationHistory {
  entries: ReputationEntry[]
  ledgerTotal: number
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

  /** The user's reputation history (ledger entries + running total). */
  getReputationHistory(userId: string): Promise<ReputationHistory> {
    return api.get<ReputationHistory>(`/api/users/${userId}/reputation/history`).then((r) => r.data)
  },

  /** Downloads a user's activity report (yourself, or anyone if you're an admin). */
  activityReport(userId: string, format: 'pdf' | 'xlsx'): Promise<Blob> {
    return api
      .get(`/api/users/${userId}/activity-report/${format}`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
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

  /** Downloads all of the user's personal data as a JSON blob (GDPR export). */
  exportData(): Promise<Blob> {
    return api.get('/api/users/me/export', { responseType: 'blob' }).then((r) => r.data as Blob)
  },

  /** Closes (anonymizes) the account after password confirmation. */
  deleteAccount(password: string): Promise<void> {
    return api.post('/api/users/me/delete', { password }).then(() => undefined)
  },
}
