import { createContext, useContext } from 'react'

/** Feature flags plus whether they have been loaded from the server yet. */
export interface FeatureState {
  marketplaceEnabled: boolean
  forumEnabled: boolean
  /** False until the first fetch settles; guards against flashing/hiding before we know. */
  loaded: boolean
}

// Default fail-OPEN (everything on) to match the backend default and avoid hiding a
// feature that is actually enabled while the flags are still loading.
export const defaultFeatureState: FeatureState = {
  marketplaceEnabled: true,
  forumEnabled: true,
  loaded: false,
}

/** Shared feature flags; the FeaturesProvider fills these in after fetching. */
export const FeaturesContext = createContext<FeatureState>(defaultFeatureState)

/** Updates the shared flags after an admin changes them, so the nav reacts without a reload. */
export const SetFeaturesContext = createContext<(marketplaceEnabled: boolean, forumEnabled: boolean) => void>(
  () => {},
)

/** Reads the current feature flags (defaults to everything enabled until loaded). */
export const useFeatures = () => useContext(FeaturesContext)

/** Returns a setter that updates the shared flags (used by the admin settings page). */
export const useSetFeatures = () => useContext(SetFeaturesContext)
