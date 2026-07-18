import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { settingsService } from '../services/settingsService'

/** Feature flags plus whether they have been loaded from the server yet. */
export interface FeatureState {
  marketplaceEnabled: boolean
  forumEnabled: boolean
  /** False until the first fetch settles; guards against flashing/hiding before we know. */
  loaded: boolean
}

// Default fail-OPEN (everything on) to match the backend default and avoid hiding a
// feature that is actually enabled while the flags are still loading.
const defaultState: FeatureState = { marketplaceEnabled: true, forumEnabled: true, loaded: false }

const FeaturesContext = createContext<FeatureState>(defaultState)

/** Updates the shared flags after an admin changes them, so the nav reacts without a reload. */
const SetFeaturesContext = createContext<(marketplaceEnabled: boolean, forumEnabled: boolean) => void>(
  () => {},
)

/**
 * Fetches the org-wide feature flags once and shares them with the app, so the sidebar,
 * command palette and route guards all hide/allow the same features. Mounted inside the
 * authenticated shell (the flags endpoint requires a signed-in user).
 */
export function FeaturesProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<FeatureState>(defaultState)

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

/** Reads the current feature flags (defaults to everything enabled until loaded). */
export const useFeatures = () => useContext(FeaturesContext)

/** Returns a setter that updates the shared flags (used by the admin settings page). */
export const useSetFeatures = () => useContext(SetFeaturesContext)
