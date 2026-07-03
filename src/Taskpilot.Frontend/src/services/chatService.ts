import api from '../lib/api'
import type { Conversation, Message, ReactionUpdate } from '../types/chat'

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

  /** POST /api/chat/conversations/{id}/read — mark a conversation as read. */
  markRead(conversationId: string): Promise<void> {
    return api.post(`/api/chat/conversations/${conversationId}/read`).then(() => undefined)
  },

  /** GET /api/chat/conversations/{id}/messages — message history. */
  getMessages(conversationId: string): Promise<Message[]> {
    return api
      .get<Message[]>(`/api/chat/conversations/${conversationId}/messages`)
      .then((r) => r.data)
  },

  /** POST /api/chat/messages — send a message, optionally with a file attachment. */
  sendMessage(conversationId: string, content: string, fileAttachmentId?: string): Promise<Message> {
    return api
      .post<Message>('/api/chat/messages', { conversationId, content, fileAttachmentId })
      .then((r) => r.data)
  },

  /** PUT /api/chat/messages/{id} — edit own message text. */
  editMessage(messageId: string, content: string): Promise<Message> {
    return api.put<Message>(`/api/chat/messages/${messageId}`, { content }).then((r) => r.data)
  },

  /** DELETE /api/chat/messages/{id} — delete own message. */
  deleteMessage(messageId: string): Promise<void> {
    return api.delete(`/api/chat/messages/${messageId}`).then(() => undefined)
  },

  /** POST /api/chat/messages/{id}/reactions — toggle an emoji reaction. */
  react(messageId: string, emoji: string): Promise<ReactionUpdate> {
    return api
      .post<ReactionUpdate>(`/api/chat/messages/${messageId}/reactions`, { emoji })
      .then((r) => r.data)
  },
}
