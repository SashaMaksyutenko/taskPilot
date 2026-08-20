import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import GitHubConnectPanel from './GitHubConnectPanel'

// Hoisted mocks for the service + router so the panel can be tested in isolation.
const { status, connect, repos, disconnect, connectUrl, setParams } = vi.hoisted(() => ({
  status: vi.fn(),
  connect: vi.fn(),
  repos: vi.fn(),
  disconnect: vi.fn(),
  connectUrl: vi.fn(),
  setParams: vi.fn(),
}))

vi.mock('react-router-dom', () => ({ useSearchParams: () => [new URLSearchParams(), setParams] }))
vi.mock('../services/githubConnectionService', () => ({
  githubConnectionService: { status, connect, repos, disconnect, connectUrl },
}))
vi.mock('../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))
vi.mock('../lib/apiError', () => ({ apiErrorMessage: () => 'err' }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

describe('GitHubConnectPanel', () => {
  beforeEach(() => {
    status.mockReset()
    connect.mockReset()
    repos.mockReset()
    disconnect.mockReset()
    connectUrl.mockReset()
  })

  it('renders nothing when the server has no GitHub integration configured', async () => {
    status.mockResolvedValue({ configured: false, connected: false, login: null, connectedAt: null })
    render(<GitHubConnectPanel />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(screen.queryByText('ghlink.title')).toBeNull()
  })

  it('offers Connect when configured but not linked', async () => {
    status.mockResolvedValue({ configured: true, connected: false, login: null, connectedAt: null })
    render(<GitHubConnectPanel />)
    expect(await screen.findByText('ghlink.connect')).toBeTruthy()
    expect(screen.getByText('ghlink.title')).toBeTruthy()
  })

  it('lists repositories on demand, then disconnects, when linked', async () => {
    status.mockResolvedValue({ configured: true, connected: true, login: 'octocat', connectedAt: null })
    repos.mockResolvedValue([{ fullName: 'octocat/hello', private: false }])
    disconnect.mockResolvedValue(undefined)
    render(<GitHubConnectPanel />)

    // Shows the linked login.
    expect(await screen.findByText('ghlink.connectedAs')).toBeTruthy()

    fireEvent.click(screen.getByText('ghlink.viewRepos'))
    await waitFor(() => expect(repos).toHaveBeenCalled())
    expect(await screen.findByText('octocat/hello')).toBeTruthy()

    fireEvent.click(screen.getByText('ghlink.disconnect'))
    await waitFor(() => expect(disconnect).toHaveBeenCalled())
    // After disconnecting the panel offers Connect again.
    expect(await screen.findByText('ghlink.connect')).toBeTruthy()
  })
})
