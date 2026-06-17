import axios from 'axios'

// Base URL of the backend API. Comes from the VITE_API_URL env var (.env),
// with a sensible dev fallback so the app still runs without a .env file.
const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/**
 * Shared axios instance for all backend calls.
 * A single configured client keeps headers, base URL and interceptors in one place.
 */
const api = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
})

// Request interceptor: attach the JWT access token (if we have one) to every request.
// For now the token lives in localStorage; this will be wired to the Redux store later.
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export default api
