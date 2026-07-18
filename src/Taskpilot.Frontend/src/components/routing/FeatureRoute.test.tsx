import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import FeatureRoute from './FeatureRoute'
import type { FeatureState } from '../../hooks/useFeatures'

// The guard's decision comes entirely from useFeatures, so mock it per test.
const mockFeatures = vi.fn<() => FeatureState>()
vi.mock('../../hooks/useFeatures', () => ({ useFeatures: () => mockFeatures() }))

/** Renders the guard around a "/marketplace" child, starting at that path. */
function renderAt(feature: 'marketplace' | 'forum') {
  return render(
    <MemoryRouter initialEntries={['/marketplace']}>
      <Routes>
        <Route element={<FeatureRoute feature={feature} />}>
          <Route path="/marketplace" element={<div>Feature page</div>} />
        </Route>
        <Route path="/" element={<div>Home</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('FeatureRoute', () => {
  it('renders the page when the feature is enabled', () => {
    mockFeatures.mockReturnValue({ marketplaceEnabled: true, forumEnabled: true, loaded: true })
    renderAt('marketplace')
    expect(screen.getByText('Feature page')).toBeTruthy()
  })

  it('redirects home when the feature is disabled', () => {
    mockFeatures.mockReturnValue({ marketplaceEnabled: false, forumEnabled: true, loaded: true })
    renderAt('marketplace')
    expect(screen.getByText('Home')).toBeTruthy()
    expect(screen.queryByText('Feature page')).toBeNull()
  })

  it('waits (no redirect) until the flags have loaded', () => {
    // Before loading, the disabled-looking default must not redirect an enabled page.
    mockFeatures.mockReturnValue({ marketplaceEnabled: false, forumEnabled: true, loaded: false })
    renderAt('marketplace')
    expect(screen.queryByText('Home')).toBeNull()
    expect(screen.queryByText('Feature page')).toBeNull()
  })

  it('gates each feature independently', () => {
    // Marketplace off must not affect a forum-gated route.
    mockFeatures.mockReturnValue({ marketplaceEnabled: false, forumEnabled: true, loaded: true })
    renderAt('forum')
    expect(screen.getByText('Feature page')).toBeTruthy()
  })
})
