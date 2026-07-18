import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import FeatureSettings from './FeatureSettings'
import { FeaturesProvider } from './FeaturesProvider'
import { useFeatures } from '../hooks/useFeatures'
import { adminService, type OrganizationSettings } from '../services/adminService'
import { settingsService } from '../services/settingsService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('../services/adminService', () => ({
  adminService: { updateFeatures: vi.fn() },
}))
vi.mock('../services/settingsService', () => ({
  settingsService: { getFeatures: vi.fn() },
}))

const org = (over: Partial<OrganizationSettings> = {}): OrganizationSettings => ({
  maxUploadBytes: 10 * 1024 * 1024,
  storageQuotaBytes: 1024 * 1024 * 1024,
  storageUsedBytes: 0,
  marketplaceEnabled: true,
  forumEnabled: true,
  updatedAt: null,
  ...over,
})

const getFeatures = vi.mocked(settingsService.getFeatures)
const updateFeatures = vi.mocked(adminService.updateFeatures)

/** Surfaces the shared feature state so the test can watch the sidebar react. */
function Probe() {
  const f = useFeatures()
  return <div>flags:{String(f.marketplaceEnabled)}/{String(f.forumEnabled)}</div>
}

describe('FeatureSettings', () => {
  beforeEach(() => vi.clearAllMocks())

  it('loads and reflects the current flags in the checkboxes', async () => {
    getFeatures.mockResolvedValue({ marketplaceEnabled: true, forumEnabled: false })
    render(<FeatureSettings />)

    const forum = (await screen.findByLabelText('features.forum')) as HTMLInputElement
    const market = screen.getByLabelText('features.marketplace') as HTMLInputElement
    expect(forum.checked).toBe(false)
    expect(market.checked).toBe(true)
  })

  it('applies a toggle immediately and updates the shared nav (no Save button)', async () => {
    getFeatures.mockResolvedValue({ marketplaceEnabled: true, forumEnabled: true })
    updateFeatures.mockResolvedValue(org({ forumEnabled: false }))

    render(
      <FeaturesProvider>
        <FeatureSettings />
        <Probe />
      </FeaturesProvider>,
    )

    expect(await screen.findByText('flags:true/true')).toBeTruthy()
    fireEvent.click(await screen.findByLabelText('features.forum'))

    // The dedicated features endpoint is called with the new flags...
    await waitFor(() => expect(updateFeatures).toHaveBeenCalledWith(true, false))
    // ...and the shared nav flips without any reload.
    await waitFor(() => expect(screen.getByText('flags:true/false')).toBeTruthy())
  })

  it('rolls the toggle back if the save fails', async () => {
    getFeatures.mockResolvedValue({ marketplaceEnabled: true, forumEnabled: true })
    updateFeatures.mockRejectedValue(new Error('backend down'))

    render(
      <FeaturesProvider>
        <FeatureSettings />
        <Probe />
      </FeaturesProvider>,
    )

    expect(await screen.findByText('flags:true/true')).toBeTruthy()
    fireEvent.click(await screen.findByLabelText('features.forum'))

    await waitFor(() => expect(updateFeatures).toHaveBeenCalled())
    // Shared state returns to both-enabled instead of staying off.
    await waitFor(() => expect(screen.getByText('flags:true/true')).toBeTruthy())
  })

  it('fetches the flags exactly once (StrictMode-safe ref guard)', async () => {
    getFeatures.mockResolvedValue({ marketplaceEnabled: true, forumEnabled: true })
    render(<FeatureSettings />)
    await waitFor(() => expect(getFeatures).toHaveBeenCalled())
    expect(getFeatures).toHaveBeenCalledTimes(1)
  })
})
