// Types mirroring the backend forum DTOs.

export interface TopicListItem {
  id: string
  title: string
  authorId: string
  authorName: string
  authorAvatarUrl: string | null
  viewCount: number
  replyCount: number
  isPinned: boolean
  isLocked: boolean
  isSolved: boolean
  createdAt: string
  lastActivityAt: string
}

/** A group of the same emoji reaction on a reply. */
export interface ReplyReaction {
  emoji: string
  count: number
  mine: boolean
}

export interface Reply {
  id: string
  topicId: string
  authorId: string
  authorName: string
  authorAvatarUrl: string | null
  body: string
  parentReplyId: string | null
  isSolution: boolean
  isDeleted: boolean
  score: number
  myVote: number // -1, 0 or 1
  createdAt: string
  updatedAt: string | null
  reactions: ReplyReaction[]
}

export interface TopicDetail {
  id: string
  title: string
  body: string
  authorId: string
  authorName: string
  authorAvatarUrl: string | null
  viewCount: number
  isPinned: boolean
  isLocked: boolean
  isSubscribed: boolean
  createdAt: string
  updatedAt: string | null
  replies: Reply[]
}

export interface VoteResult {
  replyId: string
  score: number
  myVote: number
}
