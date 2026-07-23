import api from '../lib/api'

/** Which optional features are enabled org-wide (mirrors the backend FeatureFlagsDto). */
export interface FeatureFlags {
  marketplaceEnabled: boolean
  forumEnabled: boolean
}

/** The organization's public branding (mirrors the backend OrganizationBrandingDto). */
export interface OrganizationBranding {
  name: string
}

/** Read-only organization info. */
export const settingsService = {
  /** Reads the enabled/disabled state of the optional features (Marketplace, Forum). */
  getFeatures(): Promise<FeatureFlags> {
    return api.get<FeatureFlags>('/api/settings/features').then((r) => r.data)
  },

  /** Reads the organization name shown across the app. Open to anonymous callers. */
  getBranding(): Promise<OrganizationBranding> {
    return api.get<OrganizationBranding>('/api/settings/branding').then((r) => r.data)
  },
}
