import { createContext, useContext } from 'react'

/** The organization name shown across the app. */
export interface BrandingState {
  name: string
}

// Default to the product name so the shell and sign-in pages render a brand immediately,
// before the branding fetch settles (and if it ever fails).
export const defaultBrandingState: BrandingState = {
  name: 'TaskPilot',
}

/** Shared organization branding; the BrandingProvider fills this in after fetching. */
export const BrandingContext = createContext<BrandingState>(defaultBrandingState)

/** Updates the shared name after an admin changes it, so the shell reacts without a reload. */
export const SetBrandingContext = createContext<(name: string) => void>(() => {})

/** Reads the current organization name (defaults to "TaskPilot" until loaded). */
export const useBranding = () => useContext(BrandingContext)

/** Returns a setter that updates the shared name (used by the admin settings page). */
export const useSetBranding = () => useContext(SetBrandingContext)
