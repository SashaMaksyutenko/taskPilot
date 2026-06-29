import api from '../lib/api'
import type { AdminUser, Appeal, AuditLog, IssueWarningResult, PagedResult, Warning } from '../types/admin'

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

  issueWarning(userId: string, reason: string): Promise<IssueWarningResult> {
    return api
      .post<IssueWarningResult>(`/api/admin/users/${userId}/warnings`, { reason })
      .then((r) => r.data)
  },

  getUserWarnings(userId: string): Promise<Warning[]> {
    return api.get<Warning[]>(`/api/admin/users/${userId}/warnings`).then((r) => r.data)
  },

  getAppeals(status?: string): Promise<Appeal[]> {
    return api
      .get<Appeal[]>('/api/admin/appeals', { params: status ? { status } : {} })
      .then((r) => r.data)
  },

  resolveAppeal(appealId: string, approve: boolean, note?: string): Promise<Appeal> {
    return api
      .post<Appeal>(`/api/admin/appeals/${appealId}/resolve`, { approve, note })
      .then((r) => r.data)
  },
}
