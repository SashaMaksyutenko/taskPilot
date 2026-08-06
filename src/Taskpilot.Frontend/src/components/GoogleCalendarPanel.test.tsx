import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import GoogleCalendarPanel from './GoogleCalendarPanel'

// Hoisted mocks for the service + router so the panel can be tested in isolation.
const { status, connect, sync, disconnect, connectUrl, setParams } = vi.hoisted(() => ({
  status: vi.fn(),
  connect: vi.fn(),
  sync: vi.fn(),
  disconnect: vi.fn(),
  connectUrl: vi.fn(),
  setParams: vi.fn(),
}))

vi.mock('react-router-dom', () => ({ useSearchParams: () => [new URLSearchParams(), setParams] }))
vi.mock('../services/googleCalendarService', () => ({
  googleCalendarService: { status, connect, sync, disconnect, connectUrl },
}))
vi.mock('../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))
vi.mock('../lib/apiError', () => ({ apiErrorMessage: () => 'err' }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

describe('GoogleCalendarPanel', () => {
  beforeEach(() => {
    status.mockReset()
    connect.mockReset()
    sync.mockReset()
    disconnect.mockReset()
    connectUrl.mockReset()
  })

  it('renders nothing when the server has no Google OAuth configured', async () => {
    status.mockResolvedValue({ configured: false, connected: false, lastSyncedUtc: null })
    render(<GoogleCalendarPanel />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(screen.queryByText('gcal.title')).toBeNull()
  })

  it('offers Connect when configured but not connected', async () => {
    status.mockResolvedValue({ configured: true, connected: false, lastSyncedUtc: null })
    render(<GoogleCalendarPanel />)
    expect(await screen.findByText('gcal.connect')).toBeTruthy()
    expect(screen.getByText('gcal.title')).toBeTruthy()
  })

  it('syncs, then disconnects, when connected', async () => {
    status.mockResolvedValue({ configured: true, connected: true, lastSyncedUtc: null })
    sync.mockResolvedValue({ created: 2, updated: 1, total: 3 })
    disconnect.mockResolvedValue(undefined)
    render(<GoogleCalendarPanel />)

    fireEvent.click(await screen.findByText('gcal.syncNow'))
    await waitFor(() => expect(sync).toHaveBeenCalled())

    fireEvent.click(screen.getByText('gcal.disconnect'))
    await waitFor(() => expect(disconnect).toHaveBeenCalled())
    // After disconnecting the panel offers Connect again.
    expect(await screen.findByText('gcal.connect')).toBeTruthy()
  })
})
