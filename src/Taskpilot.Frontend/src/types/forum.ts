// Types mirroring the backend forum DTOs.

export interface TopicListItem {
  id: string
  title: string
  authorId: string
  authorName: string
  viewCount: number
  replyCount: number
  isPinned: boolean
  isLocked: boolean
  createdAt: string
}

export interface Reply {
  id: string
  topicId: string
  authorId: string
  authorName: string
  body: string
  parentReplyId: string | null
  isSolution: boolean
  score: number
  myVote: number // -1, 0 or 1
  createdAt: string
  updatedAt: string | null
}

export interface TopicDetail {
  id: string
  title: string
  body: string
  authorId: string
  authorName: string
  viewCount: number
  isPinned: boolean
  isLocked: boolean
  createdAt: string
  updatedAt: string | null
  replies: Reply[]
}

export interface VoteResult {
  replyId: string
  score: number
  myVote: number
}
