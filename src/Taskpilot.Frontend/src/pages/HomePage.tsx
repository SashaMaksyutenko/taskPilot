import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { fetchMe, logout } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

/**
 * Minimal authenticated home page. Shows the current user and a logout button.
 * Route protection (redirecting anonymous users away) is added in the next session.
 */
export default function HomePage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { user, isAuthenticated } = useAppSelector((s) => s.auth)

  // If we have a token but no loaded profile yet (e.g. after a page refresh),
  // fetch the current user.
  useEffect(() => {
    if (isAuthenticated && !user) {
      dispatch(fetchMe())
    }
  }, [isAuthenticated, user, dispatch])

  const handleLogout = () => {
    dispatch(logout())
    navigate('/login')
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 text-center shadow-lg">
        <img src="/logo.svg" alt="TaskPilot" className="mx-auto w-44" />

        <h1 className="mt-2 text-2xl font-bold text-[#1E2A44]">
          Welcome{user ? `, ${user.name}` : ''}!
        </h1>

        {user && (
          <div className="mt-4 space-y-1 text-sm text-slate-600">
            <p>Email: {user.email}</p>
            <p>Role: {user.role}</p>
          </div>
        )}

        <Link
          to="/projects"
          className="mt-6 block w-full rounded-lg bg-[#1E2A44] py-2.5 font-semibold text-white transition hover:bg-[#27345a]"
        >
          My projects
        </Link>

        <Link
          to="/chat"
          className="mt-3 block w-full rounded-lg bg-[#1E2A44] py-2.5 font-semibold text-white transition hover:bg-[#27345a]"
        >
          Open chat
        </Link>

        <button
          type="button"
          onClick={handleLogout}
          className="mt-3 w-full rounded-lg bg-[#F6BE2C] py-2.5 font-semibold text-[#1E2A44] transition hover:brightness-95"
        >
          Log out
        </button>
      </div>
    </div>
  )
}
