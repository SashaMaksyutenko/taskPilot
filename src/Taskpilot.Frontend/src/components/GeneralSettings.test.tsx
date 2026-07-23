import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import GeneralSettings from './GeneralSettings'
import { adminService, type OrganizationSettings } from '../services/adminService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('../services/adminService', () => ({
  adminService: { getSettings: vi.fn(), updateGeneral: vi.fn() },
}))

// Capture the shared-branding setter so we can assert the shell is updated on save.
const setBranding = vi.fn()
vi.mock('../hooks/useBranding', () => ({ useSetBranding: () => setBranding }))

const org = (over: Partial<OrganizationSettings> = {}): OrganizationSettings => ({
  name: 'TaskPilot',
  maxUploadBytes: 10 * 1024 * 1024,
  storageQuotaBytes: 1024 * 1024 * 1024,
  storageUsedBytes: 0,
  marketplaceEnabled: true,
  forumEnabled: true,
  allowedEmailDomains: '',
  blockedEmailDomains: '',
  maxMembers: 0,
  activeMembers: 0,
  updatedAt: null,
  ...over,
})

const getSettings = vi.mocked(adminService.getSettings)
const updateGeneral = vi.mocked(adminService.updateGeneral)

describe('GeneralSettings', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows the current organization name', async () => {
    getSettings.mockResolvedValue(org({ name: 'Acme Corp' }))
    render(<GeneralSettings />)

    expect(await screen.findByDisplayValue('Acme Corp')).toBeTruthy()
  })

  it('saves a new name and pushes it into the shared branding', async () => {
    getSettings.mockResolvedValue(org({ name: 'TaskPilot' }))
    updateGeneral.mockResolvedValue(org({ name: 'Acme Corp' }))
    render(<GeneralSettings />)

    fireEvent.change(await screen.findByDisplayValue('TaskPilot'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByText('general.save'))

    await waitFor(() => expect(updateGeneral).toHaveBeenCalledWith('Acme Corp'))
    // The shell must update immediately, without a reload.
    expect(setBranding).toHaveBeenCalledWith('Acme Corp')
    expect(await screen.findByText('general.saved')).toBeTruthy()
  })

  it('trims the name before sending it', async () => {
    getSettings.mockResolvedValue(org({ name: 'TaskPilot' }))
    updateGeneral.mockResolvedValue(org({ name: 'Acme Corp' }))
    render(<GeneralSettings />)

    fireEvent.change(await screen.findByDisplayValue('TaskPilot'), { target: { value: '  Acme Corp  ' } })
    fireEvent.click(screen.getByText('general.save'))

    await waitFor(() => expect(updateGeneral).toHaveBeenCalledWith('Acme Corp'))
  })

  it('disables Save until the name changes, and for a blank name', async () => {
    getSettings.mockResolvedValue(org({ name: 'Acme' }))
    render(<GeneralSettings />)

    const button = (await screen.findByText('general.save')) as HTMLButtonElement
    const input = screen.getByDisplayValue('Acme')
    expect(button.disabled).toBe(true)   // unchanged

    fireEvent.change(input, { target: { value: '   ' } })
    expect(button.disabled).toBe(true)   // blank is refused

    fireEvent.change(input, { target: { value: 'Acme Corp' } })
    expect(button.disabled).toBe(false)
  })

  it('fetches exactly once under StrictMode', async () => {
    getSettings.mockResolvedValue(org())
    render(
      <StrictMode>
        <GeneralSettings />
      </StrictMode>,
    )

    await waitFor(() => expect(getSettings).toHaveBeenCalled())
    expect(getSettings).toHaveBeenCalledTimes(1)
  })

  it('surfaces a save failure and does not update the branding', async () => {
    getSettings.mockResolvedValue(org({ name: 'TaskPilot' }))
    updateGeneral.mockRejectedValue(new Error('boom'))
    render(<GeneralSettings />)

    fireEvent.change(await screen.findByDisplayValue('TaskPilot'), { target: { value: 'Acme' } })
    fireEvent.click(screen.getByText('general.save'))

    await waitFor(() => expect(updateGeneral).toHaveBeenCalled())
    expect(setBranding).not.toHaveBeenCalled()
    expect(screen.queryByText('general.saved')).toBeNull()
  })
})
