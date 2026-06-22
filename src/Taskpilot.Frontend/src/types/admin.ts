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
