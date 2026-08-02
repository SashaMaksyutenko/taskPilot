import api from '../lib/api'

/** A user's week-in-review numbers (mirrors DigestDto). */
export interface Digest {
  weekStart: string
  completed: number
  created: number
  overdue: number
  dueSoon: number
  topCompleted: string[]
  topOverdue: string[]
  topDueSoon: string[]
}

/** An AI-written narrative of the week (mirrors DigestSummaryDto). */
export interface DigestSummary {
  enabled: boolean
  summary: string
}

export const digestService = {
  /** Week-in-review numbers (no LLM call). */
  weekly(): Promise<Digest> {
    return api.get<Digest>('/api/digest/weekly').then((r) => r.data)
  },

  /** An AI-written summary of the week (uses the LLM — call on demand). */
  summary(): Promise<DigestSummary> {
    return api.get<DigestSummary>('/api/digest/weekly/summary').then((r) => r.data)
  },
}
