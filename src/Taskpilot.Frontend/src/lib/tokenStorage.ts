/**
 * Where the auth tokens live depends on the login-time "Remember me" choice:
 * - remember → localStorage (survives a browser restart)
 * - don't remember → sessionStorage (cleared when the tab/browser closes)
 *
 * Reads check both stores so an existing session in either is honoured. Writes go to
 * the chosen store and clear the other, so the two never disagree.
 */
const ACCESS = 'accessToken'
const REFRESH = 'refreshToken'

function readSide(key: string): string | null {
  return sessionStorage.getItem(key) ?? localStorage.getItem(key)
}

export const tokenStorage = {
  getAccess: (): string | null => readSide(ACCESS),
  getRefresh: (): string | null => readSide(REFRESH),

  /** Persists tokens in localStorage (remember) or sessionStorage (session-only). */
  save(access: string, refresh: string, remember: boolean) {
    const primary = remember ? localStorage : sessionStorage
    const other = remember ? sessionStorage : localStorage
    primary.setItem(ACCESS, access)
    primary.setItem(REFRESH, refresh)
    other.removeItem(ACCESS)
    other.removeItem(REFRESH)
  },

  /** Updates tokens after a refresh, keeping them in whichever store holds the session. */
  update(access: string, refresh: string) {
    const store = localStorage.getItem(ACCESS) !== null ? localStorage : sessionStorage
    store.setItem(ACCESS, access)
    store.setItem(REFRESH, refresh)
  },

  /** Removes tokens from both stores (sign out). */
  clear() {
    for (const store of [localStorage, sessionStorage]) {
      store.removeItem(ACCESS)
      store.removeItem(REFRESH)
    }
  },
}
