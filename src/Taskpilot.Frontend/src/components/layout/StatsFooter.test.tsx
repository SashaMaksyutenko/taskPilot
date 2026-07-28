import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import StatsFooter from './StatsFooter'

const { getPublic } = vi.hoisted(() => ({ getPublic: vi.fn() }))

vi.mock('../../services/statsService', () => ({ statsService: { getPublic } }))
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))
vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }))

describe('StatsFooter', () => {
  beforeEach(() => {
    getPublic.mockReset().mockResolvedValue({
      totalUsers: 42,
      newestUserName: null,
      totalTopics: 7,
      totalForumPosts: 15,
      onlineUsers: 3,
      onlineUserNames: [],
    })
  })

  it('renders the public stat numbers with their labels', async () => {
    render(<StatsFooter />)

    // The four figures from the stats panel (no charts).
    expect(await screen.findByText('42')).toBeTruthy()
    expect(screen.getByText('7')).toBeTruthy()
    expect(screen.getByText('15')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
    expect(screen.getByText('stats.users')).toBeTruthy()
    expect(screen.getByText('stats.online')).toBeTruthy()
  })

  it('renders nothing until the stats load', () => {
    getPublic.mockReturnValue(new Promise(() => {})) // never resolves
    const { container } = render(<StatsFooter />)
    expect(container.querySelector('footer')).toBeNull()
  })
})
