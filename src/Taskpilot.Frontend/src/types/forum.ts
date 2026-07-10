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
  tags: string[]
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
  tags: string[]
  replies: Reply[]
}

/** A moderation report on a forum reply (mirrors ForumReportDto). */
export interface ForumReport {
  id: string
  replyId: string
  topicId: string
  topicTitle: string
  replyExcerpt: string
  replyAuthorName: string
  reporterId: string
  reporterName: string
  reason: string | null
  status: string
  createdAt: string
}

export interface VoteResult {
  replyId: string
  score: number
  myVote: number
}
