import { AxiosError } from 'axios'

/**
 * Extracts a human-readable message from a failed API call. The backend returns
 * `{ error: "..." }` on handled failures (e.g. muted/locked); fall back otherwise.
 */
export function apiErrorMessage(e: unknown, fallback = 'Something went wrong.'): string {
  if (e instanceof AxiosError) {
    const data = e.response?.data as { error?: string } | undefined
    if (data?.error) return data.error
  }
  return fallback
}
