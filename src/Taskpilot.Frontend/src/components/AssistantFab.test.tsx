import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import AssistantFab from './AssistantFab'

// Isolate the button from routing, i18n and the network.
const { navigate, status, loc } = vi.hoisted(() => ({
  navigate: vi.fn(),
  status: vi.fn(),
  loc: { pathname: '/' },
}))

vi.mock('react-router-dom', () => ({ useNavigate: () => navigate, useLocation: () => loc }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))
vi.mock('../services/chatbotService', () => ({ chatbotService: { status } }))

describe('AssistantFab', () => {
  beforeEach(() => {
    navigate.mockReset()
    status.mockReset().mockResolvedValue({ enabled: true })
    loc.pathname = '/'
  })

  it('appears once the assistant is enabled and opens the assistant on click', async () => {
    render(<AssistantFab />)
    const btn = await screen.findByRole('button', { name: 'assistant.fab' })
    fireEvent.click(btn)
    expect(navigate).toHaveBeenCalledWith('/assistant')
  })

  it('renders nothing when the assistant is not configured', async () => {
    status.mockResolvedValue({ enabled: false })
    const { container } = render(<AssistantFab />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(container.querySelector('button')).toBeNull()
  })

  it('hides itself on the assistant page to avoid redundancy', async () => {
    loc.pathname = '/assistant'
    const { container } = render(<AssistantFab />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(container.querySelector('button')).toBeNull()
  })
})
