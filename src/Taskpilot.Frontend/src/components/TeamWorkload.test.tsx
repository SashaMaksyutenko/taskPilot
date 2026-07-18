import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import TeamWorkload from './TeamWorkload'
import { taskService, type TeamMemberWorkload } from '../services/taskService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    // Handles both t(key, { count }) and t(key, defaultValueString).
    t: (k: string, o?: unknown) =>
      o && typeof o === 'object' && 'count' in o ? `${(o as { count: number }).count} ${k}` : k,
  }),
}))

vi.mock('../services/taskService', () => ({
  taskService: { getTeamWorkload: vi.fn() },
}))

// Stub Avatar so the member's name appears only once (from the name label, not the avatar).
vi.mock('./Avatar', () => ({ default: () => <span data-testid="avatar" /> }))

const member = (over: Partial<TeamMemberWorkload> = {}): TeamMemberWorkload => ({
  userId: 'u1',
  name: 'Alice',
  avatarUrl: null,
  isOwner: false,
  tasks: [],
  ...over,
})

const getTeamWorkload = vi.mocked(taskService.getTeamWorkload)

describe('TeamWorkload', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders each member with their tasks', async () => {
    getTeamWorkload.mockResolvedValue([
      member({
        userId: 'o1',
        name: 'Owner',
        isOwner: true,
        tasks: [
          { id: 't1', title: 'Ship it', projectId: 'p1', projectName: 'P', status: 'InProgress', priority: 'High', deadline: '2026-07-20T12:00:00Z' },
        ],
      }),
      member({ userId: 'a1', name: 'Alice', tasks: [] }),
    ])

    render(<TeamWorkload projectId="p1" />)

    expect(await screen.findByText('Owner')).toBeTruthy()
    expect(screen.getByText('Ship it')).toBeTruthy()
    // The owner badge shows.
    expect(screen.getByText('team.owner')).toBeTruthy()
    // A member with no tasks shows the "free" state.
    expect(screen.getByText('team.free')).toBeTruthy()
  })

  it('fetches exactly once under StrictMode', async () => {
    getTeamWorkload.mockResolvedValue([member()])
    render(
      <StrictMode>
        <TeamWorkload projectId="p1" />
      </StrictMode>,
    )

    await waitFor(() => expect(getTeamWorkload).toHaveBeenCalled())
    expect(getTeamWorkload).toHaveBeenCalledTimes(1)
  })

  it('refetches when the project changes', async () => {
    getTeamWorkload.mockResolvedValue([member()])
    const { rerender } = render(<TeamWorkload projectId="p1" />)
    await waitFor(() => expect(getTeamWorkload).toHaveBeenCalledWith('p1'))

    rerender(<TeamWorkload projectId="p2" />)
    await waitFor(() => expect(getTeamWorkload).toHaveBeenCalledWith('p2'))
  })

  it('shows a message when the workload cannot be loaded', async () => {
    getTeamWorkload.mockRejectedValue(new Error('boom'))
    render(<TeamWorkload projectId="p1" />)

    expect(await screen.findByText('team.failed')).toBeTruthy()
  })
})
