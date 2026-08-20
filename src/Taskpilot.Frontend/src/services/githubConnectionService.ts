import api from '../lib/api'

/** Whether the GitHub integration is available (server-configured) and linked for the user. */
export interface GitHubConnectionStatus {
  configured: boolean
  connected: boolean
  login: string | null
  connectedAt: string | null
}

/** A repository the linked GitHub account can access. */
export interface GitHubRepo {
  fullName: string
  private: boolean
}

/**
 * Per-user GitHub account link (outbound OAuth): connect a personal account, see status, disconnect,
 * and list the linked account's repositories.
 */
export const githubConnectionService = {
  status(): Promise<GitHubConnectionStatus> {
    return api.get<GitHubConnectionStatus>('/api/integrations/github/status').then((r) => r.data)
  },

  /** The GitHub authorize URL to redirect to; redirectUri must match the one used on connect. */
  connectUrl(redirectUri: string): Promise<string> {
    return api
      .get<{ url: string }>('/api/integrations/github/connect-url', { params: { redirectUri } })
      .then((r) => r.data.url)
  },

  /** Completes the link with the code GitHub returned (state is the CSRF token echoed back). */
  connect(code: string, redirectUri: string, state: string | null): Promise<GitHubConnectionStatus> {
    return api
      .post<GitHubConnectionStatus>('/api/integrations/github/connect', { code, redirectUri, state })
      .then((r) => r.data)
  },

  /** Lists the repositories the linked account can access. */
  repos(): Promise<GitHubRepo[]> {
    return api.get<GitHubRepo[]>('/api/integrations/github/repos').then((r) => r.data)
  },

  /** Unlinks the user's GitHub account. */
  disconnect(): Promise<void> {
    return api.delete('/api/integrations/github').then(() => undefined)
  },
}
