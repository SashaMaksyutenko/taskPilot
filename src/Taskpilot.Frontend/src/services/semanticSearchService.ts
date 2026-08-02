import api from '../lib/api'

/** One semantic-search hit (mirrors SemanticSearchResultDto). */
export interface SemanticResult {
  sourceType: string
  sourceId: string
  title: string
  snippet: string
  url: string
  /** Cosine similarity 0–1. */
  score: number
}

export interface SemanticResponse {
  enabled: boolean
  results: SemanticResult[]
}

export interface SemanticStatus {
  enabled: boolean
  indexedCount: number
}

export interface ReindexResult {
  enabled: boolean
  indexed: number
}

export const semanticSearchService = {
  /** Whether semantic search is enabled and how many items are indexed. */
  status(): Promise<SemanticStatus> {
    return api.get<SemanticStatus>('/api/search/semantic/status').then((r) => r.data)
  },

  /** Meaning-based search over the user's tasks and notes. */
  search(q: string, limit = 10): Promise<SemanticResponse> {
    return api.get<SemanticResponse>('/api/search/semantic', { params: { q, limit } }).then((r) => r.data)
  },

  /** Rebuilds the user's semantic index from their current tasks and notes. */
  reindex(): Promise<ReindexResult> {
    return api.post<ReindexResult>('/api/search/semantic/reindex').then((r) => r.data)
  },
}
