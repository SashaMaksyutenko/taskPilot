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
  applicationCount: number
  createdAt: string
}

export interface Application {
  id: string
  taskId: string
  applicantId: string
  applicantName: string
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
  assigneeId: string | null
  assigneeName: string | null
  createdAt: string
  applications: Application[]
}
