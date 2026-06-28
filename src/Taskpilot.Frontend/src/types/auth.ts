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

/** Self user profile (mirrors UserDto, returned by GET /api/auth/me). */
export interface User {
  id: string
  name: string
  email: string
  role: string
  isActive: boolean
  avatarUrl: string | null
  title: string | null
  bio: string | null
  location: string | null
  website: string | null
  linkedIn: string | null
  github: string | null
  phone: string | null
  showEmail: boolean
  createdAt: string
}
