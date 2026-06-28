import api from '../lib/api'
import type { PagedResult } from '../types/common'
import type { Reply, TopicDetail, TopicListItem, VoteResult } from '../types/forum'

/** REST calls for the forum. */
export const forumService = {
  getTopics(
    params: { authorId?: string; page?: number; pageSize?: number } = {},
  ): Promise<PagedResult<TopicListItem>> {
    return api.get<PagedResult<TopicListItem>>('/api/forum/topics', { params }).then((r) => r.data)
  },

  getTopic(id: string): Promise<TopicDetail> {
    return api.get<TopicDetail>(`/api/forum/topics/${id}`).then((r) => r.data)
  },

  createTopic(data: { title: string; body: string }): Promise<TopicDetail> {
    return api.post<TopicDetail>('/api/forum/topics', data).then((r) => r.data)
  },

  deleteTopic(id: string): Promise<void> {
    return api.delete(`/api/forum/topics/${id}`).then(() => undefined)
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
