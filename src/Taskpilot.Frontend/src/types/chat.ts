// Types mirroring the backend chat DTOs.

export interface Participant {
  userId: string
  name: string
}

export interface Conversation {
  id: string
  type: string // "Direct" | "Group"
  name: string | null
  createdAt: string
  participants: Participant[]
}

export interface Message {
  id: string
  conversationId: string
  senderId: string
  senderName: string
  content: string
  createdAt: string
  editedAt: string | null
  isDeleted: boolean
  fileId: string | null
  fileName: string | null
  fileContentType: string | null
}
