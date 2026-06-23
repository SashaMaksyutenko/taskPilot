import api from '../lib/api'
import type { AdminStats, PublicStats } from '../types/stats'

/** Site statistics: a public summary and the admin-only full version. */
export const statsService = {
  /** Public stats (no auth required) — safe to show to everyone. */
  getPublic(): Promise<PublicStats> {
    return api.get<PublicStats>('/api/stats').then((r) => r.data)
  },

  /** Full stats including anonymous-visitor analytics (admin only). */
  getAdmin(): Promise<AdminStats> {
    return api.get<AdminStats>('/api/admin/stats').then((r) => r.data)
  },
}
