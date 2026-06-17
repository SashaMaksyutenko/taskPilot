import api from '../lib/api'
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  User,
} from '../types/auth'

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
}
