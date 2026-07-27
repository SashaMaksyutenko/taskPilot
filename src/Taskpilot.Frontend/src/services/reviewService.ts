import api from '../lib/api'

/** A review a user has received, with its context resolved for display (mirrors UserReviewDto). */
export interface UserReview {
  id: string
  /** "Marketplace" | "Project" | "Forum". */
  context: string
  contextId?: string | null
  /** Human-readable name of the context entity (project name, task/topic title). */
  contextLabel?: string | null
  /** In-app route to open the context entity. */
  contextLink?: string | null
  raterId: string
  raterName: string
  raterAvatarUrl?: string | null
  stars: number
  comment?: string | null
  createdAt: string
}

/** Input for leaving a peer review (mirrors LeaveReviewDto). */
export interface LeaveReview {
  rateeId: string
  stars: number
  comment?: string | null
}

export const reviewService = {
  /** All reviews a user has received, across every context. */
  getUserReviews(userId: string): Promise<UserReview[]> {
    return api.get<UserReview[]>(`/api/reviews/user/${userId}`).then((r) => r.data)
  },

  /** Leaves a review about a fellow member of a project. */
  leaveProjectReview(projectId: string, dto: LeaveReview): Promise<UserReview> {
    return api.post<UserReview>(`/api/reviews/project/${projectId}`, dto).then((r) => r.data)
  },
}
