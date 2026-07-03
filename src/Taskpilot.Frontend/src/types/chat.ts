// Types mirroring the backend chat DTOs.

export interface Participant {
  userId: string
  name: string
  avatarUrl: string | null
}

export interface Conversation {
  id: string
  type: string // "Direct" | "Group"
  name: string | null
  createdAt: string
  participants: Participant[]
  unreadCount: number
}

export interface Message {
  id: string
  conversationId: string
  senderId: string
  senderName: string
  senderAvatarUrl: string | null
  content: string
  createdAt: string
  editedAt: string | null
  isDeleted: boolean
  isPinned: boolean
  fileId: string | null
  fileName: string | null
  fileContentType: string | null
  reactions: Reaction[]
}

export interface Reaction {
  emoji: string
  count: number
  mine: boolean
}

export interface ReactionUpdate {
  messageId: string
  conversationId: string
  reactions: Reaction[]
}
