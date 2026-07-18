import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { FeaturesProvider } from '../components/FeaturesProvider'
import { useFeatures } from './useFeatures'
import { settingsService } from '../services/settingsService'

vi.mock('../services/settingsService', () => ({
  settingsService: { getFeatures: vi.fn() },
}))

const getFeatures = vi.mocked(settingsService.getFeatures)

/** Shows the current flags so tests can assert on them. */
function Probe() {
  const f = useFeatures()
  return (
    <div>
      market:{String(f.marketplaceEnabled)} forum:{String(f.forumEnabled)} loaded:{String(f.loaded)}
    </div>
  )
}

describe('useFeatures / FeaturesProvider', () => {
  beforeEach(() => vi.clearAllMocks())

  it('exposes the fetched flags', async () => {
    getFeatures.mockResolvedValue({ marketplaceEnabled: false, forumEnabled: true })
    render(
      <FeaturesProvider>
        <Probe />
      </FeaturesProvider>,
    )

    expect(await screen.findByText(/market:false forum:true loaded:true/)).toBeTruthy()
  })

  it('fetches exactly once under StrictMode', async () => {
    getFeatures.mockResolvedValue({ marketplaceEnabled: true, forumEnabled: true })
    render(
      <StrictMode>
        <FeaturesProvider>
          <Probe />
        </FeaturesProvider>
      </StrictMode>,
    )

    await waitFor(() => expect(getFeatures).toHaveBeenCalled())
    expect(getFeatures).toHaveBeenCalledTimes(1)
  })

  it('keeps features enabled (fail-open) when the fetch fails', async () => {
    getFeatures.mockRejectedValue(new Error('boom'))
    render(
      <FeaturesProvider>
        <Probe />
      </FeaturesProvider>,
    )

    // Marked loaded so guards stop waiting, but flags stay on rather than hiding features.
    expect(await screen.findByText(/market:true forum:true loaded:true/)).toBeTruthy()
  })
})
