import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import GitHubModal from './GitHubModal'

// Hoisted service mocks so the modal can be tested in isolation.
const { status, connect, disconnect, setMergeAction } = vi.hoisted(() => ({
  status: vi.fn(),
  connect: vi.fn(),
  disconnect: vi.fn(),
  setMergeAction: vi.fn(),
}))

vi.mock('../../services/gitHubService', () => ({
  gitHubService: { status, connect, disconnect, setMergeAction },
}))
vi.mock('../../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))
vi.mock('../../lib/apiError', () => ({ apiErrorMessage: () => 'err' }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

describe('GitHubModal merge action', () => {
  beforeEach(() => {
    status.mockReset()
    connect.mockReset()
    disconnect.mockReset()
    setMergeAction.mockReset()
  })

  it('shows the merge-action selector defaulting to the project value', async () => {
    status.mockResolvedValue({ connected: true, repo: 'o/r', webhookUrl: 'u', mergeAction: 'Review' })
    render(<GitHubModal projectId="p1" onClose={() => {}} />)

    const select = (await screen.findByLabelText('github.mergeActionTitle')) as HTMLSelectElement
    expect(select.value).toBe('Review')
  })

  it('persists a new choice via setMergeAction', async () => {
    status.mockResolvedValue({ connected: true, repo: 'o/r', webhookUrl: 'u', mergeAction: 'Review' })
    setMergeAction.mockResolvedValue({ connected: true, repo: 'o/r', webhookUrl: 'u', mergeAction: 'Done' })
    render(<GitHubModal projectId="p1" onClose={() => {}} />)

    const select = await screen.findByLabelText('github.mergeActionTitle')
    fireEvent.change(select, { target: { value: 'Done' } })

    await waitFor(() => expect(setMergeAction).toHaveBeenCalledWith('p1', 'Done'))
    expect((select as HTMLSelectElement).value).toBe('Done')
  })

  it('hides the selector when the project is not connected', async () => {
    status.mockResolvedValue({ connected: false, repo: null, webhookUrl: null, mergeAction: 'Review' })
    render(<GitHubModal projectId="p1" onClose={() => {}} />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(screen.queryByLabelText('github.mergeActionTitle')).toBeNull()
  })
})
