import { describe, expect, it, vi } from 'vitest'
import { AxiosError } from 'axios'
import { configureStore } from '@reduxjs/toolkit'
import authReducer, { fetchMe } from './authSlice'

const { getMe } = vi.hoisted(() => ({ getMe: vi.fn() }))
vi.mock('../services/authService', () => ({ authService: { getMe } }))
vi.mock('../lib/tokenStorage', () => ({
  tokenStorage: { getAccess: () => 'tok', getRefresh: () => 'ref', clear: vi.fn(), save: vi.fn(), update: vi.fn() },
}))

const makeStore = () => configureStore({ reducer: { auth: authReducer } })
const axiosErrorWith = (status: number) =>
  new AxiosError('failed', String(status), undefined, null, { status } as never)

describe('fetchMe rejection handling', () => {

  it('signs the user out on a genuine 401 (the interceptor already tried to refresh)', async () => {
    getMe.mockImplementation(async () => { throw axiosErrorWith(401) })
    const store = makeStore()

    await store.dispatch(fetchMe())

    expect(store.getState().auth.isAuthenticated).toBe(false)
    expect(store.getState().auth.user).toBeNull()
  })

  it.each([429, 500, 503])('keeps the session on a transient %i', async (status) => {
    getMe.mockImplementation(async () => { throw axiosErrorWith(status) })
    const store = makeStore()
    const before = store.getState().auth.isAuthenticated

    await store.dispatch(fetchMe())

    // A blip must not blank the app / bounce the user to the login page.
    expect(store.getState().auth.isAuthenticated).toBe(before)
  })

  it('keeps the session when the request fails with no response at all (offline)', async () => {
    getMe.mockImplementation(async () => { throw new AxiosError('Network Error', 'ERR_NETWORK') })
    const store = makeStore()
    const before = store.getState().auth.isAuthenticated

    await store.dispatch(fetchMe())

    expect(store.getState().auth.isAuthenticated).toBe(before)
  })
})
