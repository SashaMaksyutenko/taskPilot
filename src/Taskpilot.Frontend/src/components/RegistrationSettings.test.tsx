import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import RegistrationSettings from './RegistrationSettings'
import { adminService, type OrganizationSettings } from '../services/adminService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, o?: Record<string, unknown>) => (o ? `${k} ${JSON.stringify(o)}` : k),
  }),
}))

vi.mock('../services/adminService', () => ({
  adminService: { getSettings: vi.fn(), updateRegistration: vi.fn() },
}))

const org = (over: Partial<OrganizationSettings> = {}): OrganizationSettings => ({
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
const updateRegistration = vi.mocked(adminService.updateRegistration)

describe('RegistrationSettings', () => {
  beforeEach(() => vi.clearAllMocks())

  it('says registration is open when no domains are set', async () => {
    getSettings.mockResolvedValue(org({ allowedEmailDomains: '' }))
    render(<RegistrationSettings />)

    // The empty state must be explicit — an admin should never have to guess.
    // Partial match: the same line also reports seat usage.
    expect(await screen.findByText(/registration.openNow/)).toBeTruthy()
  })

  it('lists the domains in force when the allowlist is set', async () => {
    getSettings.mockResolvedValue(org({ allowedEmailDomains: 'acme.com, acme.io' }))
    render(<RegistrationSettings />)

    expect(await screen.findByText(/registration.restrictedNow.*acme.com, acme.io/)).toBeTruthy()
    expect(screen.getByDisplayValue('acme.com, acme.io')).toBeTruthy()
  })

  it('saves the edited domains and shows the value the server normalized', async () => {
    getSettings.mockResolvedValue(org({ allowedEmailDomains: '' }))
    // The server cleans "@Acme.COM" down to "acme.com".
    updateRegistration.mockResolvedValue(org({ allowedEmailDomains: 'acme.com' }))
    render(<RegistrationSettings />)

    const input = await screen.findByPlaceholderText('registration.placeholder')
    fireEvent.change(input, { target: { value: '@Acme.COM' } })
    fireEvent.click(screen.getByText('registration.save'))

    await waitFor(() =>
      expect(updateRegistration).toHaveBeenCalledWith({
        allowedEmailDomains: '@Acme.COM',
        blockedEmailDomains: '',
        maxMembers: 0,
      }),
    )
    // The input reflects the normalized value returned by the server.
    expect(await screen.findByDisplayValue('acme.com')).toBeTruthy()
    expect(screen.getByText('registration.saved')).toBeTruthy()
  })

  it('disables Save until the value actually changes', async () => {
    getSettings.mockResolvedValue(org({ allowedEmailDomains: 'acme.com' }))
    render(<RegistrationSettings />)

    const button = (await screen.findByText('registration.save')) as HTMLButtonElement
    expect(button.disabled).toBe(true)

    fireEvent.change(screen.getByDisplayValue('acme.com'), { target: { value: 'acme.io' } })
    expect(button.disabled).toBe(false)
  })

  it('shows and saves the blocked-domain denylist', async () => {
    getSettings.mockResolvedValue(org({ allowedEmailDomains: '', blockedEmailDomains: 'spam.example' }))
    updateRegistration.mockResolvedValue(
      org({ allowedEmailDomains: '', blockedEmailDomains: 'spam.example, junk.example' }),
    )
    render(<RegistrationSettings />)

    // Registration stays open, but the blocked list is reported alongside it.
    expect(await screen.findByText(/registration.openNow/)).toBeTruthy()
    expect(screen.getByText(/registration.blockedNow.*spam.example/)).toBeTruthy()

    fireEvent.change(screen.getByDisplayValue('spam.example'), {
      target: { value: 'spam.example, junk.example' },
    })
    fireEvent.click(screen.getByText('registration.save'))

    // Allowlist unchanged (empty), denylist sent as edited.
    await waitFor(() =>
      expect(updateRegistration).toHaveBeenCalledWith({
        allowedEmailDomains: '',
        blockedEmailDomains: 'spam.example, junk.example',
        maxMembers: 0,
      }),
    )
  })

  it('reports seat usage and saves the member limit', async () => {
    getSettings.mockResolvedValue(org({ maxMembers: 5, activeMembers: 3 }))
    updateRegistration.mockResolvedValue(org({ maxMembers: 10, activeMembers: 3 }))
    render(<RegistrationSettings />)

    // Usage is spelled out so the admin sees the headroom.
    expect(await screen.findByText(/registration.seatsNow.*"used":3.*"total":"5"/)).toBeTruthy()

    fireEvent.change(screen.getByDisplayValue('5'), { target: { value: '10' } })
    fireEvent.click(screen.getByText('registration.save'))

    await waitFor(() =>
      expect(updateRegistration).toHaveBeenCalledWith({
        allowedEmailDomains: '',
        blockedEmailDomains: '',
        maxMembers: 10,
      }),
    )
  })

  it('says the member count is unlimited when the limit is zero', async () => {
    getSettings.mockResolvedValue(org({ maxMembers: 0, activeMembers: 7 }))
    render(<RegistrationSettings />)

    expect(await screen.findByText(/registration.seatsUnlimited.*"used":7/)).toBeTruthy()
  })

  it('fetches exactly once under StrictMode', async () => {
    getSettings.mockResolvedValue(org())
    render(
      <StrictMode>
        <RegistrationSettings />
      </StrictMode>,
    )

    await waitFor(() => expect(getSettings).toHaveBeenCalled())
    expect(getSettings).toHaveBeenCalledTimes(1)
  })

  it('surfaces a save failure', async () => {
    getSettings.mockResolvedValue(org({ allowedEmailDomains: '' }))
    updateRegistration.mockRejectedValue(new Error('boom'))
    render(<RegistrationSettings />)

    fireEvent.change(await screen.findByPlaceholderText('registration.placeholder'), {
      target: { value: 'acme.com' },
    })
    fireEvent.click(screen.getByText('registration.save'))

    await waitFor(() => expect(updateRegistration).toHaveBeenCalled())
    expect(screen.queryByText('registration.saved')).toBeNull()
  })
})
