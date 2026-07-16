import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import CommandPalette from './CommandPalette'

// Isolate the palette from routing, redux, i18n and the network.
const { navigate, dispatch, search, status } = vi.hoisted(() => ({
  navigate: vi.fn(),
  dispatch: vi.fn(),
  search: vi.fn(),
  status: vi.fn(),
}))

vi.mock('react-router-dom', () => ({ useNavigate: () => navigate }))
vi.mock('../store/hooks', () => ({ useAppDispatch: () => dispatch }))
vi.mock('../services/searchService', () => ({ searchService: { search } }))
vi.mock('../services/chatbotService', () => ({ chatbotService: { status } }))
// t() returns the key, so labels are their i18n keys (e.g. "nav.projects").
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

const emptyResults = { projects: [], tasks: [], topics: [], users: [] }

describe('CommandPalette', () => {
  beforeEach(() => {
    navigate.mockReset()
    dispatch.mockReset()
    search.mockReset().mockResolvedValue(emptyResults)
    status.mockReset().mockResolvedValue({ enabled: true })
  })

  it('lists the navigation targets when open', () => {
    render(<CommandPalette open onClose={() => {}} />)
    expect(screen.getByText('nav.dashboard')).toBeTruthy()
    expect(screen.getByText('nav.projects')).toBeTruthy()
    expect(screen.getByText('nav.settings')).toBeTruthy()
  })

  it('renders nothing when closed', () => {
    render(<CommandPalette open={false} onClose={() => {}} />)
    expect(screen.queryByText('nav.dashboard')).toBeNull()
  })

  it('Enter activates the first item (dashboard → /)', () => {
    render(<CommandPalette open onClose={() => {}} />)
    fireEvent.keyDown(screen.getByPlaceholderText('cmd.placeholder'), { key: 'Enter' })
    expect(navigate).toHaveBeenCalledWith('/')
  })

  it('filters navigation as you type', () => {
    render(<CommandPalette open onClose={() => {}} />)
    fireEvent.change(screen.getByPlaceholderText('cmd.placeholder'), { target: { value: 'projects' } })
    expect(screen.getByText('nav.projects')).toBeTruthy()
    // A non-matching nav item is filtered out.
    expect(screen.queryByText('nav.calendar')).toBeNull()
  })

  it('ArrowDown then Enter activates the second item (projects)', () => {
    render(<CommandPalette open onClose={() => {}} />)
    const input = screen.getByPlaceholderText('cmd.placeholder')
    fireEvent.keyDown(input, { key: 'ArrowDown' })
    fireEvent.keyDown(input, { key: 'Enter' })
    expect(navigate).toHaveBeenCalledWith('/projects')
  })

  it('shows search results and navigates to one', async () => {
    search.mockResolvedValue({
      ...emptyResults,
      projects: [{ id: 'p1', label: 'Alpha project', sublabel: null, avatarUrl: null }],
    })
    const onClose = vi.fn()
    render(<CommandPalette open onClose={onClose} />)

    fireEvent.change(screen.getByPlaceholderText('cmd.placeholder'), { target: { value: 'alpha' } })

    const hit = await screen.findByText('Alpha project')
    fireEvent.click(hit)
    expect(navigate).toHaveBeenCalledWith('/projects/p1')
    expect(onClose).toHaveBeenCalled()
  })

  it('runs the log-out action', async () => {
    render(<CommandPalette open onClose={() => {}} />)
    // "actions" section is filtered by query; type to reveal log out.
    fireEvent.change(screen.getByPlaceholderText('cmd.placeholder'), { target: { value: 'nav.logout' } })
    fireEvent.click(await screen.findByText('nav.logout'))
    expect(dispatch).toHaveBeenCalled()
  })

  it('offers "Ask AI" for the typed query and opens the assistant with it', async () => {
    render(<CommandPalette open onClose={() => {}} />)
    fireEvent.change(screen.getByPlaceholderText('cmd.placeholder'), { target: { value: 'when is my next deadline' } })

    const askAi = await screen.findByText('cmd.askAi')
    fireEvent.click(askAi)
    expect(navigate).toHaveBeenCalledWith('/assistant', { state: { prompt: 'when is my next deadline' } })
  })

  it('hides "Ask AI" when the assistant is disabled', async () => {
    status.mockResolvedValue({ enabled: false })
    render(<CommandPalette open onClose={() => {}} />)
    fireEvent.change(screen.getByPlaceholderText('cmd.placeholder'), { target: { value: 'when is my next deadline' } })
    // Let the status promise resolve, then confirm the row never appears.
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(screen.queryByText('cmd.askAi')).toBeNull()
  })

  it('Escape closes the palette', () => {
    const onClose = vi.fn()
    render(<CommandPalette open onClose={onClose} />)
    fireEvent.keyDown(screen.getByPlaceholderText('cmd.placeholder'), { key: 'Escape' })
    expect(onClose).toHaveBeenCalled()
  })
})
