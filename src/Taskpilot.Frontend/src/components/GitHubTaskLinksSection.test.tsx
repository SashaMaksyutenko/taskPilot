import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import GitHubTaskLinksSection from './GitHubTaskLinksSection'

const { status, getTaskLinks, success } = vi.hoisted(() => ({
  status: vi.fn(),
  getTaskLinks: vi.fn(),
  success: vi.fn(),
}))

vi.mock('../services/gitHubService', () => ({ gitHubService: { status, getTaskLinks } }))
vi.mock('../lib/toast', () => ({ notify: { success, error: vi.fn() } }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

describe('GitHubTaskLinksSection', () => {
  beforeEach(() => {
    status.mockReset()
    getTaskLinks.mockReset()
    success.mockReset()
  })

  it('renders nothing when the project is not connected to GitHub', async () => {
    status.mockResolvedValue({ connected: false, repo: null, webhookUrl: null })
    getTaskLinks.mockResolvedValue([])
    render(<GitHubTaskLinksSection taskId="t1" projectId="p1" />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(screen.queryByText('github.linksTitle')).toBeNull()
  })

  it('lists linked commits/PRs when connected', async () => {
    status.mockResolvedValue({ connected: true, repo: 'o/r', webhookUrl: 'u' })
    getTaskLinks.mockResolvedValue([
      { kind: 'PullRequest', externalId: '7', title: 'Fix login', url: 'https://x/pull/7', state: 'merged', createdAt: '2026-08-06T00:00:00Z' },
    ])
    render(<GitHubTaskLinksSection taskId="t1" projectId="p1" />)
    expect(await screen.findByText('github.linksTitle')).toBeTruthy()
    expect(screen.getByText('Fix login')).toBeTruthy()
  })

  it('copies a "Closes <taskId>" reference', async () => {
    status.mockResolvedValue({ connected: true, repo: 'o/r', webhookUrl: 'u' })
    getTaskLinks.mockResolvedValue([])
    const write = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: write }, configurable: true })

    render(<GitHubTaskLinksSection taskId="task-abc" projectId="p1" />)
    fireEvent.click(await screen.findByText('github.copyRef'))

    expect(write).toHaveBeenCalledWith('Closes task-abc')
    expect(success).toHaveBeenCalled()
  })
})
