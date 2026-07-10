/** Public site statistics (mirrors PublicStatsDto). */
export interface PublicStats {
  totalUsers: number
  newestUserName: string | null
  totalTopics: number
  totalForumPosts: number
  onlineUsers: number
  onlineUserNames: string[]
}

/** Per-day activity counts (mirrors DayActivityDto). */
export interface DayActivity {
  day: string
  signups: number
  topics: number
  tasks: number
}

/** Full statistics for admins (mirrors AdminStatsDto): public fields + analytics. */
export interface AdminStats extends PublicStats {
  activeUsers: number
  anonymousVisitorsToday: number
  anonymousVisitsTotal: number
  usersByRole: Record<string, number>
  usersByStatus: Record<string, number>
}
