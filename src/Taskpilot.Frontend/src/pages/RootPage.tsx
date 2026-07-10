import AppShell from '../components/layout/AppShell'
import { useAppSelector } from '../store/hooks'
import HomePage from './HomePage'
import LandingPage from './LandingPage'

/**
 * The site root "/": shows the personal dashboard to logged-in users, and the
 * public marketing landing page to guests.
 */
export default function RootPage() {
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated)
  if (!isAuthenticated) return <LandingPage />
  return (
    <AppShell>
      <HomePage />
    </AppShell>
  )
}
