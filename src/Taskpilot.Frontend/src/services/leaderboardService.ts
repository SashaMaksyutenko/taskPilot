import api from '../lib/api'

/** One ranked user on the leaderboard (mirrors LeaderboardEntryDto). */
export interface LeaderboardEntry {
  rank: number
  userId: string
  name: string
  avatarUrl: string | null
  score: number
  tasksCompleted: number
}

/** The leaderboard top plus the current user's standing (mirrors LeaderboardDto). */
export interface Leaderboard {
  entries: LeaderboardEntry[]
  me: LeaderboardEntry | null
}

export const leaderboardService = {
  /** Top users by reputation, plus the caller's own rank. */
  get(limit = 20): Promise<Leaderboard> {
    return api.get<Leaderboard>('/api/leaderboard', { params: { limit } }).then((r) => r.data)
  },
}
