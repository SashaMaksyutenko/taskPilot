/** A user row for the admin table (mirrors AdminUserDto). */
export interface AdminUser {
  id: string
  name: string
  avatarUrl: string | null
  email: string
  role: string
  isActive: boolean
  createdAt: string
}

/** Available roles for the role selector. */
export const ROLES = ['Developer', 'Manager', 'Admin', 'Viewer'] as const

/** One audit-trail entry (mirrors AuditLogDto). */
export interface AuditLog {
  id: string
  actorId: string | null
  actorEmail: string | null
  action: string
  entityType: string | null
  entityId: string | null
  details: string | null
  ipAddress: string | null
  createdAt: string
}

// Re-exported from the shared module so existing imports keep working.
export type { PagedResult } from './common'
