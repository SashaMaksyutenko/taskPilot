import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { tokenStorage } from './tokenStorage'

// Base URL of the backend API. Comes from the VITE_API_URL env var (.env),
// with a sensible dev fallback so the app still runs without a .env file.
const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/** Base URL of the backend, exported for building absolute asset URLs (e.g. avatars in <img> tags). */
export const apiBaseUrl = baseURL

/**
 * Shared axios instance for all backend calls.
 * A single configured client keeps headers, base URL and interceptors in one place.
 */
const api = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
})

// Request interceptor: attach the JWT access token (if we have one) to every request.
api.interceptors.request.use((config) => {
  const token = tokenStorage.getAccess()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

/**
 * The access token only lives ~15 minutes, so any session left idle (switching to
 * another app, a long read) comes back to a wall of 401s. The refresh token is good
 * for days, so a 401 is not "logged out" — it just means "swap the access token".
 *
 * Shared in-flight refresh: a burst of 401s (a page firing several requests at once)
 * must trigger exactly ONE refresh call, with everyone awaiting the same promise.
 */
let refreshing: Promise<RefreshResult> | null = null

/**
 * `fatal` means the server rejected the refresh token itself — the session is genuinely
 * over. A network blip, a 429 or a 5xx is transient and must NOT destroy a valid session.
 */
type RefreshResult = { token: string | null; fatal: boolean }

async function refreshAccessToken(): Promise<RefreshResult> {
  const refreshToken = tokenStorage.getRefresh()
  if (!refreshToken) return { token: null, fatal: true }
  try {
    // A bare axios call on purpose: going through `api` would re-enter this interceptor.
    const { data } = await axios.post(
      `${baseURL}/api/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } },
    )
    tokenStorage.update(data.accessToken, data.refreshToken)
    return { token: data.accessToken as string, fatal: false }
  } catch (error) {
    const status = axios.isAxiosError(error) ? error.response?.status : undefined
    return { token: null, fatal: status === 400 || status === 401 }
  }
}

// Response interceptor: on a 401, refresh once and replay the original request.
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined

    // Only a genuine 401, only once per request (a second 401 after refreshing means
    // the new token is not the problem — don't loop).
    if (error.response?.status !== 401 || !original || original._retried) {
      return Promise.reject(error)
    }
    original._retried = true

    const pending = (refreshing ??= refreshAccessToken().finally(() => {
      refreshing = null
    }))
    const { token, fatal } = await pending

    if (!token) {
      // Only a rejected refresh token ends the session. On a transient failure we simply
      // let the caller see the error — dropping the tokens would log the user out over a
      // network blip.
      if (fatal) {
        tokenStorage.clear()
        if (!window.location.pathname.startsWith('/login')) window.location.assign('/login')
      }
      return Promise.reject(error)
    }

    original.headers.Authorization = `Bearer ${token}`
    return api(original)
  },
)

export default api
