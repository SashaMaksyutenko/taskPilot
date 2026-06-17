import { Navigate, Outlet } from 'react-router-dom'
import { useAppSelector } from '../store/hooks'

/**
 * Route guard for authenticated-only pages.
 * If the user is logged in, renders the matched child route (<Outlet />);
 * otherwise redirects to the login page.
 *
 * Note: if the stored token is expired, the page's fetchMe() will fail and the
 * auth slice flips isAuthenticated to false, which bounces the user here on the
 * next render.
 */
export default function ProtectedRoute() {
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated)

  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}
