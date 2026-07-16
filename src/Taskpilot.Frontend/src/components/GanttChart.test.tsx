import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import GanttChart from './GanttChart'
import type { Task } from '../types/project'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string, o?: { n?: number }) => (o?.n !== undefined ? `${k}:${o.n}` : k) }),
}))

const task = (over: Partial<Task>): Task => ({
  id: 'id',
  projectId: 'p1',
  title: 'Task',
  description: null,
  status: 'Backlog',
  priority: 'Medium',
  assigneeId: null,
  assigneeName: null,
  creatorId: 'u1',
  creatorName: 'Me',
  parentTaskId: null,
  deadline: null,
  createdAt: '2026-07-01T00:00:00Z',
  updatedAt: null,
  completedAt: null,
  tags: [],
  timeSpentSeconds: 0,
  timerStartedAt: null,
  ...over,
})

describe('GanttChart', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date('2026-07-10T00:00:00Z'))
  })

  it('shows an empty state when no task has a deadline', () => {
    render(<GanttChart tasks={[task({ id: 'a' })]} />)
    expect(screen.getByText('gantt.empty')).toBeTruthy()
  })

  it('draws a bar per scheduled task and counts the unscheduled ones', () => {
    const { container } = render(
      <GanttChart
        tasks={[
          task({ id: 'a', title: 'Has deadline', createdAt: '2026-07-01T00:00:00Z', deadline: '2026-07-11T00:00:00Z' }),
          task({ id: 'b', title: 'No deadline' }),
        ]}
      />,
    )

    expect(screen.getByText('Has deadline')).toBeTruthy()
    // The unscheduled task is counted, not drawn.
    expect(screen.queryByText('No deadline')).toBeNull()
    expect(screen.getByText('gantt.unscheduled:1')).toBeTruthy()
    expect(container.querySelectorAll('button')).toHaveLength(1) // one bar
  })

  it('positions the bar within the padded range and clicking it selects the task', () => {
    const onSelect = vi.fn()
    const scheduled = task({ id: 'a', title: 'Ship', createdAt: '2026-07-01T00:00:00Z', deadline: '2026-07-11T00:00:00Z' })
    const { container } = render(<GanttChart tasks={[scheduled]} onSelect={onSelect} />)

    const bar = container.querySelector('button') as HTMLElement
    // Range is padded by one day either side (30 Jun → 12 Jul = 12 days); the 10-day
    // bar starts one day in, so ~8.3% from the left and ~83% wide.
    expect(parseFloat(bar.style.left)).toBeCloseTo(100 / 12, 1)
    expect(parseFloat(bar.style.width)).toBeCloseTo((10 / 12) * 100, 1)

    fireEvent.click(bar)
    expect(onSelect).toHaveBeenCalledWith(scheduled)
  })

  it('still draws a left-to-right bar when the deadline predates creation', () => {
    const { container } = render(
      <GanttChart tasks={[task({ id: 'a', createdAt: '2026-07-11T00:00:00Z', deadline: '2026-07-01T00:00:00Z' })]} />,
    )
    const bar = container.querySelector('button') as HTMLElement
    expect(parseFloat(bar.style.width)).toBeGreaterThan(0)
    expect(parseFloat(bar.style.left)).toBeGreaterThanOrEqual(0)
  })
})
