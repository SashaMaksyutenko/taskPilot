import { Navigate, Outlet } from 'react-router-dom'
import { useFeatures } from '../../hooks/useFeatures'

/**
 * Route guard for an optional feature. Sends the user home when the admin has disabled
 * the feature — so typing /forum or /marketplace directly cannot reach a disabled page.
 * The backend also 403s the feature's API, so this is UX, not the security boundary.
 */
export default function FeatureRoute({ feature }: { feature: 'marketplace' | 'forum' }) {
  const features = useFeatures()

  // Wait for the flags before deciding, so an enabled page is never briefly redirected.
  if (!features.loaded) return <p className="p-8 text-muted">Loading…</p>

  const enabled = feature === 'marketplace' ? features.marketplaceEnabled : features.forumEnabled
  return enabled ? <Outlet /> : <Navigate to="/" replace />
}
