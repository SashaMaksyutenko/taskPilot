// TypeScript types that mirror the backend auth DTOs.
// Keeping them in one place makes the API service strongly typed.

/** Body sent to POST /api/auth/register. */
export interface RegisterRequest {
  name: string
  email: string
  password: string
}

/** Body sent to POST /api/auth/login. */
export interface LoginRequest {
  email: string
  password: string
}

/** Response from login / refresh (mirrors AuthResponseDto on the backend). */
export interface AuthResponse {
  accessToken: string
  expiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  userId: string
  email: string
  role: string
}

/** Public user profile (mirrors UserDto, returned by GET /api/auth/me). */
export interface User {
  id: string
  name: string
  email: string
  role: string
  isActive: boolean
  createdAt: string
}
