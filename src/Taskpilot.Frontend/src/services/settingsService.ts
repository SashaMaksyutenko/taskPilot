import api from '../lib/api'

/** Which optional features are enabled org-wide (mirrors the backend FeatureFlagsDto). */
export interface FeatureFlags {
  marketplaceEnabled: boolean
  forumEnabled: boolean
}

/** Read-only organization info any signed-in user may see. */
export const settingsService = {
  /** Reads the enabled/disabled state of the optional features (Marketplace, Forum). */
  getFeatures(): Promise<FeatureFlags> {
    return api.get<FeatureFlags>('/api/settings/features').then((r) => r.data)
  },
}
