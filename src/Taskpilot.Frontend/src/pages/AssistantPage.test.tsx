import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import AssistantPage from './AssistantPage'

// Isolate the page from routing, i18n and the network.
const { status, ask, locationState } = vi.hoisted(() => ({
  status: vi.fn(),
  ask: vi.fn(),
  locationState: { current: null as { prompt?: string } | null },
}))

vi.mock('react-router-dom', () => ({ useLocation: () => ({ state: locationState.current }) }))
vi.mock('../services/chatbotService', () => ({ chatbotService: { status, ask } }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))
vi.mock('../components/Markdown', () => ({ default: ({ children }: { children: string }) => <span>{children}</span> }))
vi.mock('../components/feedback/EmptyState', () => ({ default: ({ message }: { message: string }) => <p>{message}</p> }))

describe('AssistantPage', () => {
  beforeEach(() => {
    status.mockReset().mockResolvedValue({ enabled: true })
    ask.mockReset().mockResolvedValue('You have 2 tasks due.')
    locationState.current = null
  })

  it('auto-sends a prompt handed in via navigation state exactly once under StrictMode', async () => {
    locationState.current = { prompt: "What's due this week?" }

    // StrictMode double-invokes effects in dev — the guard must survive that, or the
    // question (and the paid API call) fires twice.
    render(
      <StrictMode>
        <AssistantPage />
      </StrictMode>,
    )

    await waitFor(() => expect(ask).toHaveBeenCalled())
    expect(ask).toHaveBeenCalledTimes(1)

    // Exactly one question and one answer end up on screen.
    expect(await screen.findByText('You have 2 tasks due.')).toBeTruthy()
    expect(screen.getAllByText("What's due this week?")).toHaveLength(1)
  })

  it('does not send anything when no prompt was handed in', async () => {
    render(<AssistantPage />)
    await waitFor(() => expect(status).toHaveBeenCalled())
    expect(ask).not.toHaveBeenCalled()
    expect(screen.getByText('assistant.empty')).toBeTruthy()
  })
})
