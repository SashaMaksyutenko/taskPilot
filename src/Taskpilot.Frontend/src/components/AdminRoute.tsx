import { useEffect } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

/**
 * Route guard for admin-only pages. Waits for the profile to load, then allows
 * only users with the Admin role; everyone else is sent home.
 * (The backend also enforces this via [Authorize(Roles="Admin")].)
 */
export default function AdminRoute() {
  const dispatch = useAppDispatch()
  const user = useAppSelector((s) => s.auth.user)

  useEffect(() => {
    if (!user) dispatch(fetchMe())
  }, [user, dispatch])

  if (!user) {
    return <p className="p-8 text-muted">Loading…</p>
  }

  return user.role === 'Admin' ? <Outlet /> : <Navigate to="/" replace />
}
