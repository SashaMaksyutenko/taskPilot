import api from '../lib/api'
import type { Conversation, Message } from '../types/chat'

/**
 * Wrapper around the backend chat REST endpoints.
 */
export const chatService = {
  /** GET /api/chat/conversations — the current user's conversations. */
  getConversations(): Promise<Conversation[]> {
    return api.get<Conversation[]>('/api/chat/conversations').then((r) => r.data)
  },

  /** POST /api/chat/conversations/direct — open/create a 1:1 conversation. */
  startDirect(otherUserId: string): Promise<Conversation> {
    return api
      .post<Conversation>('/api/chat/conversations/direct', { otherUserId })
      .then((r) => r.data)
  },

  /** GET /api/chat/conversations/{id}/messages — message history. */
  getMessages(conversationId: string): Promise<Message[]> {
    return api
      .get<Message[]>(`/api/chat/conversations/${conversationId}/messages`)
      .then((r) => r.data)
  },

  /** POST /api/chat/messages — send a message. */
  sendMessage(conversationId: string, content: string): Promise<Message> {
    return api
      .post<Message>('/api/chat/messages', { conversationId, content })
      .then((r) => r.data)
  },

  /** DELETE /api/chat/messages/{id} — delete own message. */
  deleteMessage(messageId: string): Promise<void> {
    return api.delete(`/api/chat/messages/${messageId}`).then(() => undefined)
  },
}
