/** Public site statistics (mirrors PublicStatsDto). */
export interface PublicStats {
  totalUsers: number
  newestUserName: string | null
  totalTopics: number
  totalForumPosts: number
  onlineUsers: number
  onlineUserNames: string[]
}

/** Full statistics for admins (mirrors AdminStatsDto): public fields + analytics. */
export interface AdminStats extends PublicStats {
  activeUsers: number
  anonymousVisitorsToday: number
  anonymousVisitsTotal: number
}
