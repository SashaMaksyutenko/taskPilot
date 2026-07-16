import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import CalendarPage from './CalendarPage'

// Isolate the page from routing, i18n, the network and the context menu.
const { navigate, getTasks, reschedule } = vi.hoisted(() => ({
  navigate: vi.fn(),
  getTasks: vi.fn(),
  reschedule: vi.fn(),
}))

vi.mock('react-router-dom', () => ({ useNavigate: () => navigate }))
vi.mock('../services/calendarService', () => ({
  calendarService: { getTasks, exportIcs: vi.fn(), getFeedUrl: vi.fn(), regenerateFeedUrl: vi.fn() },
}))
vi.mock('../services/taskService', () => ({ taskService: { reschedule } }))
vi.mock('../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))
vi.mock('../components/menus/ActionsContextMenu', () => ({
  default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}))
// t() returns the key, except the month/weekday arrays which the page indexes into.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string) => {
      if (k === 'calendar.months') return ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
      if (k === 'calendar.weekdays') return ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
      return k
    },
  }),
}))

const daysBetween = (from: string, to: string) =>
  Math.round((new Date(to).getTime() - new Date(from).getTime()) / 86400000)

describe('CalendarPage views', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date(2026, 6, 16)) // 16 Jul 2026, local time
    navigate.mockReset()
    reschedule.mockReset()
    getTasks.mockReset().mockResolvedValue([
      { id: 't1', title: 'Ship release', projectId: 'p1', projectName: 'Nebula', status: 'InProgress', deadline: '2026-07-16T10:00:00Z' },
    ])
  })

  afterEach(() => vi.useRealTimers())

  it('opens on the month view and fetches the whole month', async () => {
    render(<CalendarPage />)
    await waitFor(() => expect(getTasks).toHaveBeenCalled())

    const [from, to] = getTasks.mock.calls[0]
    expect(from).toBe('2026-07-01')
    expect(to).toBe('2026-07-31')
    expect(await screen.findByText('Ship release')).toBeTruthy()
  })

  it('switching to the week view fetches exactly 7 days', async () => {
    render(<CalendarPage />)
    await waitFor(() => expect(getTasks).toHaveBeenCalled())

    fireEvent.click(screen.getByText('calendar.view.week'))

    await waitFor(() => {
      const [from, to] = getTasks.mock.calls[getTasks.mock.calls.length - 1]
      expect(daysBetween(from, to)).toBe(6) // inclusive span of 7 days
    })
  })

  it('switching to the day view fetches a single day', async () => {
    render(<CalendarPage />)
    await waitFor(() => expect(getTasks).toHaveBeenCalled())

    fireEvent.click(screen.getByText('calendar.view.day'))

    await waitFor(() => {
      const [from, to] = getTasks.mock.calls[getTasks.mock.calls.length - 1]
      expect(from).toBe('2026-07-16')
      expect(to).toBe('2026-07-16')
    })
  })

  it('the day view shows an empty state when nothing is due', async () => {
    getTasks.mockResolvedValue([])
    render(<CalendarPage />)
    fireEvent.click(screen.getByText('calendar.view.day'))

    expect(await screen.findByText('calendar.noTasks')).toBeTruthy()
  })

  it('"Today" returns the cursor to the current day', async () => {
    render(<CalendarPage />)
    await waitFor(() => expect(getTasks).toHaveBeenCalled())

    fireEvent.click(screen.getByText('calendar.view.day'))
    fireEvent.click(screen.getByLabelText('calendar.next')) // move to 17 Jul
    await waitFor(() => {
      const [from] = getTasks.mock.calls[getTasks.mock.calls.length - 1]
      expect(from).toBe('2026-07-17')
    })

    fireEvent.click(screen.getByText('calendar.today'))
    await waitFor(() => {
      const [from] = getTasks.mock.calls[getTasks.mock.calls.length - 1]
      expect(from).toBe('2026-07-16')
    })
  })
})
