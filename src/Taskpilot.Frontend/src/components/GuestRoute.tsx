import { Navigate, Outlet } from 'react-router-dom'
import { useAppSelector } from '../store/hooks'

/**
 * Route guard for guest-only pages (login, register).
 * If the user is already logged in, sends them to the home page instead of
 * showing the auth forms again.
 */
export default function GuestRoute() {
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated)

  return isAuthenticated ? <Navigate to="/" replace /> : <Outlet />
}
