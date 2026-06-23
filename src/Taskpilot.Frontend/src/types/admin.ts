/** A user row for the admin table (mirrors AdminUserDto). */
export interface AdminUser {
  id: string
  name: string
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

/** A page of results plus total count (mirrors PagedResult<T>). */
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
