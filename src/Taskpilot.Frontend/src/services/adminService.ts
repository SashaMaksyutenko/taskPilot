import api from '../lib/api'
import type { AdminUser } from '../types/admin'

/** Admin-only REST calls for user management. */
export const adminService = {
  getUsers(): Promise<AdminUser[]> {
    return api.get<AdminUser[]>('/api/admin/users').then((r) => r.data)
  },

  changeRole(userId: string, role: string): Promise<void> {
    return api.put(`/api/admin/users/${userId}/role`, { role }).then(() => undefined)
  },

  ban(userId: string): Promise<void> {
    return api.post(`/api/admin/users/${userId}/ban`).then(() => undefined)
  },

  unban(userId: string): Promise<void> {
    return api.post(`/api/admin/users/${userId}/unban`).then(() => undefined)
  },
}
