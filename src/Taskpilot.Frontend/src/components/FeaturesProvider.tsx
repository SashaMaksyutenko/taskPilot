import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { settingsService } from '../services/settingsService'
import {
  FeaturesContext,
  SetFeaturesContext,
  defaultFeatureState,
  type FeatureState,
} from '../hooks/useFeatures'

/**
 * Fetches the org-wide feature flags once and shares them with the app, so the sidebar,
 * command palette and route guards all hide/allow the same features. Mounted inside the
 * authenticated shell (the flags endpoint requires a signed-in user).
 *
 * The component lives in its own file (apart from the {@link useFeatures} hooks) so the
 * hooks module stays component-free — React Fast Refresh only works when a file exports
 * components alone.
 */
export function FeaturesProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<FeatureState>(defaultFeatureState)

  // A ref, not state: React StrictMode double-invokes this effect in dev and the state
  // guard would not have updated on the second pass, firing the request twice.
  const loaded = useRef(false)
  useEffect(() => {
    if (loaded.current) return
    loaded.current = true
    settingsService
      .getFeatures()
      .then((f) => setState({ ...f, loaded: true }))
      // On failure keep the defaults (features on) but mark loaded so guards stop waiting.
      .catch(() => setState((s) => ({ ...s, loaded: true })))
  }, [])

  // Lets the admin settings page push new flags straight into the shared state, so the
  // sidebar and palette update the moment the change is saved — no page reload needed.
  const setFlags = useCallback(
    (marketplaceEnabled: boolean, forumEnabled: boolean) =>
      setState((s) => ({ ...s, marketplaceEnabled, forumEnabled })),
    [],
  )

  return (
    <FeaturesContext.Provider value={state}>
      <SetFeaturesContext.Provider value={setFlags}>{children}</SetFeaturesContext.Provider>
    </FeaturesContext.Provider>
  )
}
