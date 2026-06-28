import api from '../lib/api'
import type { AdminUser, AuditLog, PagedResult } from '../types/admin'

/** Admin-only REST calls for user management. */
export const adminService = {
  getUsers(page = 1, pageSize = 20): Promise<PagedResult<AdminUser>> {
    return api
      .get<PagedResult<AdminUser>>('/api/admin/users', { params: { page, pageSize } })
      .then((r) => r.data)
  },

  getAudit(page: number, pageSize: number, action?: string): Promise<PagedResult<AuditLog>> {
    return api
      .get<PagedResult<AuditLog>>('/api/admin/audit', {
        params: { page, pageSize, ...(action ? { action } : {}) },
      })
      .then((r) => r.data)
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
