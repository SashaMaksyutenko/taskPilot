import { createContext, useContext } from 'react'

/** The organization branding shown across the app. */
export interface BrandingState {
  name: string
  /** URL of the custom logo, or null to use the built-in mark. */
  logoUrl: string | null
}

// Default to the product brand so the shell and sign-in pages render immediately, before
// the branding fetch settles (and if it ever fails).
export const defaultBrandingState: BrandingState = {
  name: 'TaskPilot',
  logoUrl: null,
}

/** Shared organization branding; the BrandingProvider fills this in after fetching. */
export const BrandingContext = createContext<BrandingState>(defaultBrandingState)

/**
 * Updates the shared branding after an admin changes it, so the shell reacts without a
 * reload. Takes a partial so the name and the logo can be updated independently.
 */
export const SetBrandingContext = createContext<(update: Partial<BrandingState>) => void>(() => {})

/** Reads the current organization branding (defaults to "TaskPilot", no logo, until loaded). */
export const useBranding = () => useContext(BrandingContext)

/** Returns a setter that updates the shared branding (used by the admin settings page). */
export const useSetBranding = () => useContext(SetBrandingContext)
