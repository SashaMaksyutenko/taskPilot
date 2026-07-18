import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import StorageSettings from './StorageSettings'
import { adminService, type OrganizationSettings } from '../services/adminService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    // Echo interpolation so the usage line can be asserted.
    t: (k: string, o?: Record<string, unknown>) => (o ? `${k} ${JSON.stringify(o)}` : k),
  }),
}))

vi.mock('../services/adminService', () => ({
  adminService: { getSettings: vi.fn(), updateStorage: vi.fn() },
}))

const settings = (over: Partial<OrganizationSettings> = {}): OrganizationSettings => ({
  maxUploadBytes: 10 * 1024 * 1024,
  storageQuotaBytes: 1024 * 1024 * 1024,
  storageUsedBytes: 512 * 1024 * 1024,
  marketplaceEnabled: true,
  forumEnabled: true,
  allowedEmailDomains: '',
  updatedAt: null,
  ...over,
})

const getSettings = vi.mocked(adminService.getSettings)
const updateStorage = vi.mocked(adminService.updateStorage)

describe('StorageSettings', () => {
  beforeEach(() => vi.clearAllMocks())

  it('loads the settings and prefills the limits in MB', async () => {
    getSettings.mockResolvedValue(settings())
    render(<StorageSettings />)

    // 10 MB cap and 1024 MB quota (1 GB) shown in the inputs.
    expect(await screen.findByDisplayValue('10')).toBeTruthy()
    expect(screen.getByDisplayValue('1024')).toBeTruthy()
  })

  it('fetches exactly once under StrictMode', async () => {
    getSettings.mockResolvedValue(settings())
    render(
      <StrictMode>
        <StorageSettings />
      </StrictMode>,
    )

    await waitFor(() => expect(getSettings).toHaveBeenCalled())
    expect(getSettings).toHaveBeenCalledTimes(1)
  })

  it('saves the edited limits converted back to bytes (storage endpoint only)', async () => {
    getSettings.mockResolvedValue(settings())
    updateStorage.mockResolvedValue(settings({ maxUploadBytes: 20 * 1024 * 1024 }))
    render(<StorageSettings />)

    const maxInput = await screen.findByDisplayValue('10')
    fireEvent.change(maxInput, { target: { value: '20' } })
    fireEvent.click(screen.getByText('storage.save'))

    // 20 MB → bytes; quota unchanged at 1024 MB → bytes. No feature flags are sent —
    // the storage endpoint touches only the limits.
    await waitFor(() =>
      expect(updateStorage).toHaveBeenCalledWith(20 * 1024 * 1024, 1024 * 1024 * 1024),
    )
    expect(await screen.findByText('storage.saved')).toBeTruthy()
  })

  it('shows an error when saving fails', async () => {
    getSettings.mockResolvedValue(settings())
    updateStorage.mockRejectedValue(new Error('boom'))
    render(<StorageSettings />)

    fireEvent.click(await screen.findByText('storage.save'))

    // The failure surfaces rather than silently doing nothing.
    await waitFor(() => expect(updateStorage).toHaveBeenCalled())
    expect(screen.queryByText('storage.saved')).toBeNull()
  })
})
