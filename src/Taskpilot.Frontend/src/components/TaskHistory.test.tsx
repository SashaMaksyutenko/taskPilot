import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import TaskHistory from './TaskHistory'
import { taskService, type TaskHistoryEntry } from '../services/taskService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string, fallback?: string) => fallback ?? k }),
}))

vi.mock('../services/taskService', () => ({
  taskService: { getHistory: vi.fn() },
}))

const entry = (over: Partial<TaskHistoryEntry> = {}): TaskHistoryEntry => ({
  id: 'e1',
  action: 'task.status.changed',
  actorId: 'u1',
  actorName: 'Alice',
  details: 'Status: Backlog → Done',
  createdAt: '2026-07-17T10:00:00Z',
  ...over,
})

const mockedGetHistory = vi.mocked(taskService.getHistory)

describe('TaskHistory', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('does not fetch until the section is expanded', () => {
    mockedGetHistory.mockResolvedValue([entry()])
    render(<TaskHistory taskId="t1" />)

    // Collapsed by default: opening a task must not pay for this request.
    expect(mockedGetHistory).not.toHaveBeenCalled()
  })

  it('loads and renders the entries once expanded', async () => {
    mockedGetHistory.mockResolvedValue([entry()])
    render(<TaskHistory taskId="t1" />)

    fireEvent.click(screen.getByText('history.title'))

    expect(await screen.findByText('Status: Backlog → Done')).toBeTruthy()
    expect(mockedGetHistory).toHaveBeenCalledWith('t1')
    // The actor is shown by name; the server never sends an email here.
    expect(screen.getByText(/Alice/)).toBeTruthy()
  })

  it('fetches immediately when opened via defaultOpen', async () => {
    mockedGetHistory.mockResolvedValue([entry()])
    render(<TaskHistory taskId="t1" defaultOpen />)

    await waitFor(() => expect(mockedGetHistory).toHaveBeenCalledWith('t1'))
  })

  it('fetches exactly once under StrictMode AND still renders the result', async () => {
    // StrictMode double-invokes effects in dev; a state-based guard would not have
    // updated yet on the second pass, so the request must be guarded by a ref.
    mockedGetHistory.mockResolvedValue([entry()])
    render(
      <StrictMode>
        <TaskHistory taskId="t1" defaultOpen />
      </StrictMode>,
    )

    // Asserting the call count alone is not enough: an earlier version fired exactly
    // once and then dropped the response, leaving the section on "Loading…" forever.
    expect(await screen.findByText('Status: Backlog → Done')).toBeTruthy()
    expect(mockedGetHistory).toHaveBeenCalledTimes(1)
    expect(screen.queryByText('history.loading')).toBeNull()
  })

  it('shows an empty state when the task has no history', async () => {
    mockedGetHistory.mockResolvedValue([])
    render(<TaskHistory taskId="t1" defaultOpen />)

    expect(await screen.findByText('history.empty')).toBeTruthy()
  })

  it('shows a message when the history cannot be loaded', async () => {
    mockedGetHistory.mockRejectedValue(new Error('boom'))
    render(<TaskHistory taskId="t1" defaultOpen />)

    expect(await screen.findByText('history.failed')).toBeTruthy()
  })

  it('falls back to a system label when there is no actor', async () => {
    mockedGetHistory.mockResolvedValue([entry({ actorId: null, actorName: null })])
    render(<TaskHistory taskId="t1" defaultOpen />)

    expect(await screen.findByText(/history.system/)).toBeTruthy()
  })

  it('refetches when the modal is reused for another task', async () => {
    mockedGetHistory.mockResolvedValue([entry()])
    const { rerender } = render(<TaskHistory taskId="t1" defaultOpen />)
    await waitFor(() => expect(mockedGetHistory).toHaveBeenCalledWith('t1'))

    // The parent modal stays mounted and swaps the task, so the ref guard must
    // key on the task id rather than "have I ever loaded?".
    rerender(<TaskHistory taskId="t2" defaultOpen />)
    await waitFor(() => expect(mockedGetHistory).toHaveBeenCalledWith('t2'))
  })
})
