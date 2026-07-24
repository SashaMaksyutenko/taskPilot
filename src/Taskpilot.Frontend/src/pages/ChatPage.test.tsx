import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import ChatPage from './ChatPage'
import type { Conversation } from '../types/chat'

// Isolate the page from routing, the SignalR hub, the store, i18n and the network.
const { getConversations, getMessages, markRead, setMuted } = vi.hoisted(() => ({
  getConversations: vi.fn(),
  getMessages: vi.fn(),
  markRead: vi.fn(),
  setMuted: vi.fn(),
}))

vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useSearchParams: () => [new URLSearchParams(''), vi.fn()],
}))
vi.mock('../lib/chatHub', () => ({
  createChatConnection: () => ({
    on: vi.fn(),
    start: vi.fn(() => Promise.resolve()),
    stop: vi.fn(() => Promise.resolve()),
    invoke: vi.fn(() => Promise.resolve()),
  }),
}))
vi.mock('../services/chatService', () => ({
  chatService: { getConversations, getMessages, markRead, setMuted, startDirect: vi.fn() },
}))
vi.mock('../services/gifService', () => ({
  gifService: { search: vi.fn(() => Promise.resolve({ enabled: false, gifs: [] })) },
  isGifMessage: () => false,
}))
vi.mock('../services/bookmarkService', () => ({
  bookmarkService: { getMine: vi.fn(() => Promise.resolve([])), toggle: vi.fn() },
}))
vi.mock('../services/fileService', () => ({
  fileService: { download: vi.fn(), upload: vi.fn() },
}))
vi.mock('../services/userService', () => ({ userService: { searchUsers: vi.fn(() => Promise.resolve([])) } }))
vi.mock('../lib/toast', () => ({ notify: { success: vi.fn(), error: vi.fn() } }))
vi.mock('../store/authSlice', () => ({ fetchMe: vi.fn() }))
vi.mock('../store/hooks', () => ({
  useAppDispatch: () => vi.fn(),
  useAppSelector: (sel: (s: unknown) => unknown) => sel({ auth: { user: { id: 'me', name: 'Me' } } }),
}))
// Keep composer/menu/tooltip wrappers transparent so we test our own markup.
vi.mock('../components/ui/Tooltip', () => ({ default: ({ children }: { children: React.ReactNode }) => <>{children}</> }))
vi.mock('../components/MentionField', () => ({ default: () => <input aria-label="composer" /> }))
vi.mock('../components/menus/ActionsContextMenu', () => ({ default: ({ children }: { children: React.ReactNode }) => <>{children}</> }))
vi.mock('../components/menus/MessageContextMenu', () => ({ default: ({ children }: { children: React.ReactNode }) => <>{children}</> }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))

const conv = (over: Partial<Conversation>): Conversation => ({
  id: 'c1',
  type: 'Direct',
  name: null,
  createdAt: '2026-07-24T00:00:00Z',
  participants: [
    { userId: 'me', name: 'Me', avatarUrl: null, lastReadAt: null },
    { userId: 'u2', name: 'Bob', avatarUrl: null, lastReadAt: null },
  ],
  unreadCount: 0,
  muted: false,
  ...over,
})

describe('ChatPage mute', () => {
  beforeEach(() => {
    getMessages.mockReset().mockResolvedValue([])
    markRead.mockReset().mockResolvedValue(undefined)
    setMuted.mockReset().mockImplementation((_id: string, muted: boolean) => Promise.resolve(muted))
    getConversations.mockReset().mockResolvedValue([
      conv({ id: 'c1', muted: false }),
      conv({
        id: 'c2',
        muted: true,
        participants: [
          { userId: 'me', name: 'Me', avatarUrl: null, lastReadAt: null },
          { userId: 'u3', name: 'Carol', avatarUrl: null, lastReadAt: null },
        ],
      }),
    ])
  })

  it('shows a muted indicator in the list for muted conversations only', async () => {
    render(<ChatPage />)
    await screen.findByText('Carol')
    // The muted conversation (Carol) carries the muted indicator; there is exactly one.
    const indicators = screen.getAllByLabelText('chat.mutedLabel')
    expect(indicators).toHaveLength(1)
  })

  it('muting an unmuted conversation calls setMuted(id, true) and flips the header toggle', async () => {
    render(<ChatPage />)
    // Open the unmuted conversation (Bob) -> header shows the "mute" action.
    fireEvent.click(await screen.findByText('Bob'))
    const muteBtn = await screen.findByLabelText('chat.mute')

    fireEvent.click(muteBtn)

    await waitFor(() => expect(setMuted).toHaveBeenCalledWith('c1', true))
    // Optimistic flip: the header now offers "unmute".
    expect(await screen.findByLabelText('chat.unmute')).toBeTruthy()
  })

  it('unmuting a muted conversation calls setMuted(id, false)', async () => {
    render(<ChatPage />)
    fireEvent.click(await screen.findByText('Carol'))
    const unmuteBtn = await screen.findByLabelText('chat.unmute')

    fireEvent.click(unmuteBtn)

    await waitFor(() => expect(setMuted).toHaveBeenCalledWith('c2', false))
  })
})
