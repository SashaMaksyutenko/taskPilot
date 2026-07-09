import api from '../lib/api'

export interface ApiKey {
  id: string
  name: string
  prefix: string
  createdAt: string
  lastUsedAt: string | null
}

/** Returned once on creation — includes the full raw key. */
export interface CreatedApiKey extends ApiKey {
  key: string
}

/** REST calls for personal API keys. */
export const apiKeyService = {
  list(): Promise<ApiKey[]> {
    return api.get<ApiKey[]>('/api/apikeys').then((r) => r.data)
  },

  create(name: string): Promise<CreatedApiKey> {
    return api.post<CreatedApiKey>('/api/apikeys', { name }).then((r) => r.data)
  },

  revoke(id: string): Promise<void> {
    return api.delete(`/api/apikeys/${id}`).then(() => undefined)
  },
}
