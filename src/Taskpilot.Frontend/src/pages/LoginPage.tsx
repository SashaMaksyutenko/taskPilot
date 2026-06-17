import { Link } from 'react-router-dom'

/**
 * Placeholder login page. The full login form is built in the next session.
 */
export default function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 text-center shadow-lg">
        <img src="/logo.svg" alt="TaskPilot" className="mx-auto w-44" />
        <h1 className="mt-2 text-2xl font-bold text-[#1E2A44]">Log in</h1>
        <p className="mt-4 text-slate-500">The login form is coming in the next session.</p>
        <p className="mt-6 text-sm text-slate-600">
          Need an account?{' '}
          <Link to="/register" className="font-semibold text-[#1E2A44] hover:underline">
            Sign up
          </Link>
        </p>
      </div>
    </div>
  )
}
