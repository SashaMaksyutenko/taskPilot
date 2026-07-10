import api from '../lib/api'
import type { PagedResult } from '../types/common'
import type { Reply, ReplyReaction, TopicDetail, TopicListItem, VoteResult } from '../types/forum'

/** REST calls for the forum. */
export const forumService = {
  getTopics(
    params: {
      authorId?: string
      page?: number
      pageSize?: number
      search?: string
      solved?: boolean
      sort?: 'latest' | 'active' | 'top'
    } = {},
  ): Promise<PagedResult<TopicListItem>> {
    return api.get<PagedResult<TopicListItem>>('/api/forum/topics', { params }).then((r) => r.data)
  },

  getTopic(id: string): Promise<TopicDetail> {
    return api.get<TopicDetail>(`/api/forum/topics/${id}`).then((r) => r.data)
  },

  /** Counts one view of a topic (called once when the page opens). */
  incrementView(id: string): Promise<void> {
    return api.post(`/api/forum/topics/${id}/view`).then(() => undefined)
  },

  createTopic(data: { title: string; body: string }): Promise<TopicDetail> {
    return api.post<TopicDetail>('/api/forum/topics', data).then((r) => r.data)
  },

  /** Edits a topic's title and body (author or admin only). */
  editTopic(id: string, data: { title: string; body: string }): Promise<TopicDetail> {
    return api.put<TopicDetail>(`/api/forum/topics/${id}`, data).then((r) => r.data)
  },

  deleteTopic(id: string): Promise<void> {
    return api.delete(`/api/forum/topics/${id}`).then(() => undefined)
  },

  /** Soft-deletes a reply (author or admin only). */
  deleteReply(replyId: string): Promise<void> {
    return api.delete(`/api/forum/replies/${replyId}`).then(() => undefined)
  },

  addReply(data: { topicId: string; body: string; parentReplyId?: string }): Promise<Reply> {
    return api.post<Reply>('/api/forum/replies', data).then((r) => r.data)
  },

  /** Edits an existing reply's body (author or admin only). */
  editReply(replyId: string, body: string): Promise<Reply> {
    return api.put<Reply>(`/api/forum/replies/${replyId}`, { body }).then((r) => r.data)
  },

  vote(replyId: string, value: 1 | -1): Promise<VoteResult> {
    return api.post<VoteResult>(`/api/forum/replies/${replyId}/vote`, { value }).then((r) => r.data)
  },

  markSolution(replyId: string): Promise<void> {
    return api.post(`/api/forum/replies/${replyId}/solution`).then(() => undefined)
  },

  /** Toggles an emoji reaction on a reply; returns the updated reaction summary. */
  reactToReply(replyId: string, emoji: string): Promise<ReplyReaction[]> {
    return api.post<ReplyReaction[]>(`/api/forum/replies/${replyId}/reactions`, { emoji }).then((r) => r.data)
  },

  /** Pins or unpins a topic (admin only). */
  setPinned(topicId: string, value: boolean): Promise<void> {
    return api.post(`/api/forum/topics/${topicId}/pin`, { value }).then(() => undefined)
  },

  /** Locks or unlocks a topic (admin or author). */
  setLocked(topicId: string, value: boolean): Promise<void> {
    return api.post(`/api/forum/topics/${topicId}/lock`, { value }).then(() => undefined)
  },

  /** Toggles the current user's subscription to a topic; returns the new state. */
  toggleSubscription(topicId: string): Promise<boolean> {
    return api.post<{ subscribed: boolean }>(`/api/forum/topics/${topicId}/subscribe`).then((r) => r.data.subscribed)
  },
}
