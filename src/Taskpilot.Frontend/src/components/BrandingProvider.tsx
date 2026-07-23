import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { settingsService } from '../services/settingsService'
import {
  BrandingContext,
  SetBrandingContext,
  defaultBrandingState,
  type BrandingState,
} from '../hooks/useBranding'

/**
 * Fetches the organization name once and shares it with the whole app — the sidebar, the
 * top bar and the sign-in/landing pages all show the same brand. Mounted at the app root
 * (above the auth guards) because the branding endpoint is public, so it works before a
 * user has signed in.
 *
 * Lives in its own file (apart from the {@link useBranding} hooks) so the hooks module stays
 * component-free — React Fast Refresh only works when a file exports components alone.
 */
export function BrandingProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<BrandingState>(defaultBrandingState)

  // A ref, not state: React StrictMode double-invokes this effect in dev and the state
  // guard would not have updated on the second pass, firing the request twice.
  const loaded = useRef(false)
  useEffect(() => {
    if (loaded.current) return
    loaded.current = true
    settingsService
      .getBranding()
      .then((b) => setState({ name: b.name }))
      // On failure keep the default brand rather than showing a blank.
      .catch(() => {})
  }, [])

  // Lets the admin settings page push the new name straight into the shared state, so the
  // shell updates the moment the change is saved — no page reload needed.
  const setName = useCallback((name: string) => setState({ name }), [])

  return (
    <BrandingContext.Provider value={state}>
      <SetBrandingContext.Provider value={setName}>{children}</SetBrandingContext.Provider>
    </BrandingContext.Provider>
  )
}
