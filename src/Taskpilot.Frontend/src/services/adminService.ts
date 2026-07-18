import api from '../lib/api'
import type { AdminUser, Appeal, AuditLog, IssueWarningResult, PagedResult, Warning } from '../types/admin'

/** Admin-only REST calls for user management. */
export const adminService = {
  getUsers(
    page = 1,
    pageSize = 20,
    filters: { search?: string; role?: string; status?: string; sort?: string } = {},
  ): Promise<PagedResult<AdminUser>> {
    return api
      .get<PagedResult<AdminUser>>('/api/admin/users', {
        params: {
          page,
          pageSize,
          search: filters.search || undefined,
          role: filters.role || undefined,
          status: filters.status || undefined,
          sort: filters.sort || undefined,
        },
      })
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

  ban(userId: string, days?: number): Promise<void> {
    return api.post(`/api/admin/users/${userId}/ban`, { days }).then(() => undefined)
  },

  unban(userId: string): Promise<void> {
    return api.post(`/api/admin/users/${userId}/unban`).then(() => undefined)
  },

  mute(userId: string, days?: number): Promise<void> {
    return api.post(`/api/admin/users/${userId}/mute`, { days }).then(() => undefined)
  },

  unmute(userId: string): Promise<void> {
    return api.post(`/api/admin/users/${userId}/unmute`).then(() => undefined)
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

  /** Downloads the organisation-wide marketplace report as a PDF blob. */
  marketplaceReportPdf(): Promise<Blob> {
    return api
      .get('/api/admin/reports/marketplace/pdf', { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the marketplace report as an Excel (.xlsx) blob. */
  marketplaceReportXlsx(): Promise<Blob> {
    return api
      .get('/api/admin/reports/marketplace/xlsx', { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the audit log (most recent entries) as a PDF blob. */
  auditReportPdf(): Promise<Blob> {
    return api.get('/api/admin/reports/audit/pdf', { responseType: 'blob' }).then((r) => r.data as Blob)
  },

  /** Downloads the audit log as an Excel (.xlsx) blob. */
  auditReportXlsx(): Promise<Blob> {
    return api.get('/api/admin/reports/audit/xlsx', { responseType: 'blob' }).then((r) => r.data as Blob)
  },

  /** Reads the organization settings (storage limits) plus current usage. */
  getSettings(): Promise<OrganizationSettings> {
    return api.get<OrganizationSettings>('/api/admin/settings').then((r) => r.data)
  },

  /** Updates only the storage limits (the feature flags are left untouched). */
  updateStorage(maxUploadBytes: number, storageQuotaBytes: number): Promise<OrganizationSettings> {
    return api
      .put<OrganizationSettings>('/api/admin/settings/storage', { maxUploadBytes, storageQuotaBytes })
      .then((r) => r.data)
  },

  /** Updates only the feature flags (the storage limits are left untouched). */
  updateFeatures(marketplaceEnabled: boolean, forumEnabled: boolean): Promise<OrganizationSettings> {
    return api
      .put<OrganizationSettings>('/api/admin/settings/features', { marketplaceEnabled, forumEnabled })
      .then((r) => r.data)
  },

  /**
   * Updates only the registration domain controls (allowlist + denylist). The server
   * normalizes both values (lower-cases, strips "@", de-duplicates) and returns them.
   */
  updateRegistration(
    allowedEmailDomains: string,
    blockedEmailDomains: string,
  ): Promise<OrganizationSettings> {
    return api
      .put<OrganizationSettings>('/api/admin/settings/registration', {
        allowedEmailDomains,
        blockedEmailDomains,
      })
      .then((r) => r.data)
  },
}

/** Organization-wide settings (mirrors the backend OrganizationSettingsDto). */
export interface OrganizationSettings {
  maxUploadBytes: number
  storageQuotaBytes: number
  /** Bytes currently used by all stored files. */
  storageUsedBytes: number
  /** Whether the public task Marketplace is available. */
  marketplaceEnabled: boolean
  /** Whether the discussion Forum is available. */
  forumEnabled: boolean
  /** Comma-separated email domains allowed to register; empty means any domain. */
  allowedEmailDomains: string
  /** Comma-separated email domains barred from registering; empty blocks nothing. */
  blockedEmailDomains: string
  updatedAt: string | null
}
