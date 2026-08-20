import api from '../lib/api'

/** What a merged PR that closes a task does to it. */
export type GitHubMergeAction = 'None' | 'Review' | 'Done'

/** Whether a project is linked to a GitHub repo (secret is never returned here). */
export interface GitHubStatus {
  connected: boolean
  repo: string | null
  webhookUrl: string | null
  mergeAction: GitHubMergeAction
}

/** Returned once on connect — includes the secret to paste into the GitHub webhook. */
export interface GitHubConnectResult {
  repo: string
  webhookUrl: string
  webhookSecret: string
}

/** A commit or pull request that references a task. */
export interface GitHubTaskLink {
  kind: string
  externalId: string
  title: string
  url: string
  state: string
  createdAt: string
}

/** REST calls for the GitHub integration. */
export const gitHubService = {
  status(projectId: string): Promise<GitHubStatus> {
    return api.get<GitHubStatus>(`/api/projects/${projectId}/github`).then((r) => r.data)
  },
  connect(projectId: string, repo: string): Promise<GitHubConnectResult> {
    return api.post<GitHubConnectResult>(`/api/projects/${projectId}/github/connect`, { repo }).then((r) => r.data)
  },
  disconnect(projectId: string): Promise<void> {
    return api.delete(`/api/projects/${projectId}/github`).then(() => undefined)
  },
  setMergeAction(projectId: string, mergeAction: GitHubMergeAction): Promise<GitHubStatus> {
    return api
      .put<GitHubStatus>(`/api/projects/${projectId}/github/merge-action`, { mergeAction })
      .then((r) => r.data)
  },
  getTaskLinks(taskId: string): Promise<GitHubTaskLink[]> {
    return api.get<GitHubTaskLink[]>(`/api/tasks/${taskId}/github`).then((r) => r.data)
  },
}
