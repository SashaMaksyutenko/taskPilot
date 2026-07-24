import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import ProjectsPage from './ProjectsPage'
import type { Project } from '../types/project'

// Isolate the page from routing, the network, i18n and the store.
const { getProjects, setMuted } = vi.hoisted(() => ({ getProjects: vi.fn(), setMuted: vi.fn() }))

vi.mock('react-router-dom', () => ({
  Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a>,
}))
vi.mock('../services/projectService', () => ({
  projectService: {
    getProjects,
    setMuted,
    duplicate: vi.fn(),
    archive: vi.fn(),
    restore: vi.fn(),
    remove: vi.fn(),
    updateProject: vi.fn(),
    createProject: vi.fn(),
  },
}))
vi.mock('../services/projectTemplateService', () => ({ projectTemplateService: { saveAsTemplate: vi.fn() } }))
vi.mock('../services/taskService', () => ({ taskService: { exportCsv: vi.fn() } }))
vi.mock('../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))
vi.mock('../store/hooks', () => ({
  useAppSelector: (sel: (s: unknown) => unknown) => sel({ auth: { user: { id: 'me' } } }),
}))
vi.mock('../components/modals/TemplatesModal', () => ({ default: () => null }))
vi.mock('../components/modals/ConfirmDialog', () => ({ default: () => null }))
// EmptyState pulls in lottie-web, which needs a real canvas — stub it out.
vi.mock('../components/feedback/EmptyState', () => ({ default: () => null }))
// Expose the mute affordance as a plain button so we don't have to drive Radix's context menu.
vi.mock('../components/menus/ProjectContextMenu', () => ({
  default: ({ children, canMute, muted, onToggleMute }: {
    children: React.ReactNode; canMute?: boolean; muted?: boolean; onToggleMute?: () => void
  }) => (
    <div>
      {children}
      {canMute && (
        <button aria-label={muted ? 'ctx-unmute' : 'ctx-mute'} onClick={onToggleMute}>toggle</button>
      )}
    </div>
  ),
}))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

const project = (over: Partial<Project>): Project => ({
  id: 'p1',
  name: 'Alpha',
  description: null,
  color: null,
  ownerId: 'me',
  ownerName: 'Me',
  taskCount: 0,
  completedTaskCount: 0,
  memberCount: 0,
  isArchived: false,
  muted: false,
  createdAt: '2026-07-24T00:00:00Z',
  archivedAt: null,
  ...over,
})

describe('ProjectsPage mute', () => {
  beforeEach(() => {
    setMuted.mockReset().mockImplementation((_id: string, muted: boolean) => Promise.resolve(muted))
    getProjects.mockReset().mockResolvedValue([
      project({ id: 'own', name: 'Owned', ownerId: 'me' }),
      project({ id: 'mem', name: 'Shared', ownerId: 'other', muted: false }),
    ])
  })

  it('offers the mute action only for projects the user does not own', async () => {
    render(<ProjectsPage />)
    await screen.findByText('Shared')
    // Exactly one project (the member one) exposes the mute affordance.
    const buttons = screen.getAllByLabelText('ctx-mute')
    expect(buttons).toHaveLength(1)
  })

  it('muting a member project calls setMuted(id, true) and flips the action', async () => {
    render(<ProjectsPage />)
    await screen.findByText('Shared')

    fireEvent.click(screen.getByLabelText('ctx-mute'))

    await waitFor(() => expect(setMuted).toHaveBeenCalledWith('mem', true))
    // Optimistic flip: the action now reads "unmute".
    expect(await screen.findByLabelText('ctx-unmute')).toBeTruthy()
  })

  it('shows a muted indicator and unmutes an already-muted member project', async () => {
    getProjects.mockResolvedValue([project({ id: 'mem', name: 'Shared', ownerId: 'other', muted: true })])
    render(<ProjectsPage />)
    await screen.findByText('Shared')
    // The muted card carries the indicator.
    expect(screen.getByLabelText('projects.mutedLabel')).toBeTruthy()

    fireEvent.click(screen.getByLabelText('ctx-unmute'))

    await waitFor(() => expect(setMuted).toHaveBeenCalledWith('mem', false))
  })
})
