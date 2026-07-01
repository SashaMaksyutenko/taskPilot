import api from '../lib/api'
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  Session,
  User,
} from '../types/auth'

/** Header carrying the client's current refresh token, so the API can flag "this session". */
function currentTokenHeader() {
  const token = localStorage.getItem('refreshToken')
  return token ? { headers: { 'X-Refresh-Token': token } } : {}
}

/**
 * Thin wrapper around the backend auth endpoints.
 * Each method calls one endpoint and returns the typed response data.
 */
export const authService = {
  /** POST /api/auth/register — returns the new user's id. */
  register(data: RegisterRequest): Promise<{ id: string }> {
    return api.post<{ id: string }>('/api/auth/register', data).then((r) => r.data)
  },

  /** POST /api/auth/login — returns access + refresh tokens and basic user info. */
  login(data: LoginRequest): Promise<AuthResponse> {
    return api.post<AuthResponse>('/api/auth/login', data).then((r) => r.data)
  },

  /** POST /api/auth/refresh — exchanges a refresh token for new tokens. */
  refresh(refreshToken: string): Promise<AuthResponse> {
    return api
      .post<AuthResponse>('/api/auth/refresh', { refreshToken })
      .then((r) => r.data)
  },

  /** GET /api/auth/me — returns the current user (requires a valid access token). */
  getMe(): Promise<User> {
    return api.get<User>('/api/auth/me').then((r) => r.data)
  },

  /** GET /api/auth/sessions — active sessions (current one flagged). */
  getSessions(): Promise<Session[]> {
    return api.get<Session[]>('/api/auth/sessions', currentTokenHeader()).then((r) => r.data)
  },

  /** Revokes one session by id. */
  revokeSession(sessionId: string): Promise<void> {
    return api.post(`/api/auth/sessions/${sessionId}/revoke`).then(() => undefined)
  },

  /** Revokes all sessions except the current one. */
  revokeOtherSessions(): Promise<void> {
    return api.post('/api/auth/sessions/revoke-others', {}, currentTokenHeader()).then(() => undefined)
  },

  /** Starts 2FA enrollment; returns the secret + otpauth URI. */
  setupTwoFactor(): Promise<{ secret: string; otpauthUri: string }> {
    return api.post<{ secret: string; otpauthUri: string }>('/api/auth/2fa/setup').then((r) => r.data)
  },

  /** Enables 2FA after verifying a code. */
  enableTwoFactor(code: string): Promise<void> {
    return api.post('/api/auth/2fa/enable', { code }).then(() => undefined)
  },

  /** Disables 2FA after verifying a code. */
  disableTwoFactor(code: string): Promise<void> {
    return api.post('/api/auth/2fa/disable', { code }).then(() => undefined)
  },
}
