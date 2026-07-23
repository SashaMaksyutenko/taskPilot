import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import TemplatesModal from './TemplatesModal'
import { projectTemplateService } from '../../services/projectTemplateService'
import type { ProjectTemplate, ProjectTemplateDetail } from '../../types/template'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    // Echo interpolation so assertions can see the count/name values.
    t: (k: string, o?: Record<string, unknown>) => (o && 'count' in o ? `${k}:${o.count}` : k),
  }),
}))

vi.mock('../../services/projectTemplateService', () => ({
  projectTemplateService: {
    getTemplates: vi.fn(),
    getTemplate: vi.fn(),
    createProjectFromTemplate: vi.fn(),
    deleteTemplate: vi.fn(),
  },
}))

vi.mock('../../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))

const template = (over: Partial<ProjectTemplate> = {}): ProjectTemplate => ({
  id: 'tpl1',
  name: 'Sprint plan',
  description: null,
  color: '#4F46E5',
  taskCount: 2,
  createdAt: '2026-07-23T10:00:00Z',
  ...over,
})

const mockedGet = vi.mocked(projectTemplateService.getTemplates)
const mockedGetOne = vi.mocked(projectTemplateService.getTemplate)
const mockedCreate = vi.mocked(projectTemplateService.createProjectFromTemplate)
const mockedDelete = vi.mocked(projectTemplateService.deleteTemplate)

describe('TemplatesModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders nothing while closed and does not fetch', () => {
    const { container } = render(
      <TemplatesModal open={false} onClose={vi.fn()} onProjectCreated={vi.fn()} />,
    )
    expect(container.firstChild).toBeNull()
    expect(mockedGet).not.toHaveBeenCalled()
  })

  it('lists templates when opened', async () => {
    mockedGet.mockResolvedValue([template()])
    render(<TemplatesModal open onClose={vi.fn()} onProjectCreated={vi.fn()} />)

    expect(await screen.findByText('Sprint plan')).toBeTruthy()
    // The task count is rendered through the interpolating stub.
    expect(screen.getByText(/templates.taskCount:2/)).toBeTruthy()
  })

  it('shows an empty state when there are no templates', async () => {
    mockedGet.mockResolvedValue([])
    render(<TemplatesModal open onClose={vi.fn()} onProjectCreated={vi.fn()} />)

    expect(await screen.findByText('templates.empty')).toBeTruthy()
  })

  it('creates a project from a template, then refreshes and closes', async () => {
    mockedGet.mockResolvedValue([template()])
    mockedCreate.mockResolvedValue({ id: 'p1' } as never)
    const onProjectCreated = vi.fn()
    const onClose = vi.fn()
    render(<TemplatesModal open onClose={onClose} onProjectCreated={onProjectCreated} />)
    await screen.findByText('Sprint plan')

    fireEvent.click(screen.getByText('templates.use'))

    await waitFor(() => expect(mockedCreate).toHaveBeenCalledWith('tpl1'))
    expect(onProjectCreated).toHaveBeenCalled()
    expect(onClose).toHaveBeenCalled()
  })

  it('expands a preview and lists the template tasks, indenting subtasks', async () => {
    mockedGet.mockResolvedValue([template()])
    const detail: ProjectTemplateDetail = {
      ...template(),
      tasks: [
        { id: 't1', title: 'Design', description: null, priority: 'High', deadlineOffsetDays: 5, parentTemplateTaskId: null, tags: [] },
        { id: 't2', title: 'Wireframe', description: null, priority: 'Medium', deadlineOffsetDays: null, parentTemplateTaskId: 't1', tags: [] },
      ],
    }
    mockedGetOne.mockResolvedValue(detail)
    render(<TemplatesModal open onClose={vi.fn()} onProjectCreated={vi.fn()} />)
    await screen.findByText('Sprint plan')

    // The task-count line doubles as the preview toggle.
    fireEvent.click(screen.getByText(/templates.taskCount:2/))

    expect(await screen.findByText(/Design/)).toBeTruthy()
    expect(mockedGetOne).toHaveBeenCalledWith('tpl1')
    // The subtask is prefixed with the indent marker.
    expect(screen.getByText(/↳ Wireframe/)).toBeTruthy()
  })

  it('removes a template optimistically and restores it when the server refuses', async () => {
    mockedGet.mockResolvedValue([template()])
    mockedDelete.mockRejectedValue(new Error('nope'))
    render(<TemplatesModal open onClose={vi.fn()} onProjectCreated={vi.fn()} />)
    await screen.findByText('Sprint plan')

    fireEvent.click(screen.getByLabelText('templates.delete'))

    // Gone immediately, then back after the failure.
    await waitFor(() => expect(mockedDelete).toHaveBeenCalledWith('tpl1'))
    await waitFor(() => expect(screen.getByText('Sprint plan')).toBeTruthy())
  })
})
