import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import axios, { AxiosError } from 'axios'
import api from './api'
import { tokenStorage } from './tokenStorage'

/**
 * Drives the real interceptors on the shared `api` instance via a fake adapter, so the
 * actual 401 → refresh → retry path runs.
 *
 * NOTE: a custom axios adapter is responsible for settling the response itself — axios
 * does NOT apply validateStatus to whatever an adapter resolves. So a non-2xx must be
 * thrown as an AxiosError carrying `response`, or the interceptor never sees an error.
 */
type Call = { url?: string; auth?: unknown }
const calls: Call[] = []

const respond = (status: number, config: unknown) => {
  const response = { status, statusText: '', data: status < 300 ? { ok: true } : {}, headers: {}, config }
  if (status < 300) return response
  throw new AxiosError('Request failed', String(status), config as never, null, response as never)
}

const adapter = vi.fn(async (config: never) => {
  const cfg = config as unknown as { url?: string; headers: Record<string, unknown> }
  calls.push({ url: cfg.url, auth: cfg.headers.Authorization })
  // Anything still carrying the stale token is rejected, exactly like an expired JWT.
  return respond(cfg.headers.Authorization === 'Bearer stale' ? 401 : 200, config) as never
})

describe('api 401 → refresh → retry', () => {
  beforeEach(() => {
    calls.length = 0
    localStorage.clear()
    sessionStorage.clear()
    tokenStorage.save('stale', 'refresh-1', true)
    api.defaults.adapter = adapter as never
    adapter.mockClear()
  })

  afterEach(() => vi.restoreAllMocks())

  it('refreshes the expired token and replays the request', async () => {
    const post = vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'fresh', refreshToken: 'refresh-2' },
    } as never)

    const res = await api.get('/api/projects')

    expect(res.status).toBe(200)
    expect(post).toHaveBeenCalledTimes(1)
    // First attempt carried the stale token; the replay carried the fresh one.
    expect(calls.map((c) => c.auth)).toEqual(['Bearer stale', 'Bearer fresh'])
    // The new pair is persisted for subsequent requests.
    expect(tokenStorage.getAccess()).toBe('fresh')
    expect(tokenStorage.getRefresh()).toBe('refresh-2')
  })

  it('a burst of 401s triggers only ONE refresh call', async () => {
    const post = vi.spyOn(axios, 'post').mockImplementation(
      () =>
        new Promise((r) =>
          setTimeout(() => r({ data: { accessToken: 'fresh', refreshToken: 'refresh-2' } } as never), 10),
        ),
    )

    const results = await Promise.all([api.get('/a'), api.get('/b'), api.get('/c')])

    expect(results.every((r) => r.status === 200)).toBe(true)
    expect(post).toHaveBeenCalledTimes(1) // not three
  })

  it('gives up and clears the session when the refresh token is rejected', async () => {
    vi.spyOn(axios, 'post').mockRejectedValue(new Error('refresh expired'))

    await expect(api.get('/api/projects')).rejects.toBeTruthy()
    expect(tokenStorage.getAccess()).toBeNull()
    expect(tokenStorage.getRefresh()).toBeNull()
  })

  it('passes non-401 errors straight through without refreshing', async () => {
    const post = vi.spyOn(axios, 'post')
    api.defaults.adapter = (async (config: never) => respond(500, config) as never) as never

    await expect(api.get('/api/boom')).rejects.toBeTruthy()
    expect(post).not.toHaveBeenCalled()
  })
})
