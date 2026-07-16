import { useEffect } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { fetchMe } from '../../store/authSlice'
import { useAppDispatch, useAppSelector } from '../../store/hooks'

/**
 * Route guard for authenticated-only pages.
 * If the user is logged in, renders the matched child route (<Outlet />);
 * otherwise redirects to the login page.
 *
 * On a fresh load (e.g. after F5) the token is present but the profile is not yet
 * in the store, so we fetch it here — this keeps the navbar avatar and per-page
 * owner checks correct on every protected route, not just Dashboard/Settings.
 */
export default function ProtectedRoute() {
  const dispatch = useAppDispatch()
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated)
  const user = useAppSelector((s) => s.auth.user)

  useEffect(() => {
    if (isAuthenticated && !user) dispatch(fetchMe())
  }, [isAuthenticated, user, dispatch])

  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}
