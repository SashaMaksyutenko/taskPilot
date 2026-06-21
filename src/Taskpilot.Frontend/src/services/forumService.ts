import api from '../lib/api'
import type { Reply, TopicDetail, TopicListItem, VoteResult } from '../types/forum'

/** REST calls for the forum. */
export const forumService = {
  getTopics(): Promise<TopicListItem[]> {
    return api.get<TopicListItem[]>('/api/forum/topics').then((r) => r.data)
  },

  getTopic(id: string): Promise<TopicDetail> {
    return api.get<TopicDetail>(`/api/forum/topics/${id}`).then((r) => r.data)
  },

  createTopic(data: { title: string; body: string }): Promise<TopicDetail> {
    return api.post<TopicDetail>('/api/forum/topics', data).then((r) => r.data)
  },

  addReply(data: { topicId: string; body: string; parentReplyId?: string }): Promise<Reply> {
    return api.post<Reply>('/api/forum/replies', data).then((r) => r.data)
  },

  vote(replyId: string, value: 1 | -1): Promise<VoteResult> {
    return api.post<VoteResult>(`/api/forum/replies/${replyId}/vote`, { value }).then((r) => r.data)
  },

  markSolution(replyId: string): Promise<void> {
    return api.post(`/api/forum/replies/${replyId}/solution`).then(() => undefined)
  },
}
