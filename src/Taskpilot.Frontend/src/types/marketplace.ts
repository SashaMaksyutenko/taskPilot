// Types mirroring the backend marketplace DTOs.

export interface MarketTaskListItem {
  id: string
  title: string
  budget: number
  requiredSkills: string | null
  deadline: string | null
  status: string
  posterId: string
  posterName: string
  posterAvatarUrl: string | null
  applicationCount: number
  createdAt: string
}

export interface Application {
  id: string
  taskId: string
  applicantId: string
  applicantName: string
  applicantAvatarUrl: string | null
  coverLetter: string
  proposedRate: number
  status: string
  createdAt: string
}

export interface MarketTaskDetail {
  id: string
  title: string
  description: string
  budget: number
  requiredSkills: string | null
  deadline: string | null
  status: string
  posterId: string
  posterName: string
  posterAvatarUrl: string | null
  assigneeId: string | null
  assigneeName: string | null
  assigneeAvatarUrl: string | null
  paymentStatus: string
  paidAt: string | null
  createdAt: string
  applications: Application[]
}

export interface Review {
  id: string
  raterId: string
  raterName: string
  raterAvatarUrl: string | null
  rateeId: string
  stars: number
  comment: string | null
  createdAt: string
}
