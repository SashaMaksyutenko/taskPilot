import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import UserProfilePage from './UserProfilePage'
import type { PublicProfile } from '../services/userService'

const { getPublicProfile, endorseSkill, getReputationHistory, getTopics, state } = vi.hoisted(() => ({
  getPublicProfile: vi.fn(),
  endorseSkill: vi.fn(),
  getReputationHistory: vi.fn(),
  getTopics: vi.fn(),
  state: { currentUserId: 'me' as string | undefined },
}))

vi.mock('react-router-dom', () => ({
  useParams: () => ({ userId: 'u1' }),
  Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a>,
}))
vi.mock('../services/userService', () => ({
  userService: { getPublicProfile, endorseSkill, getReputationHistory },
}))
vi.mock('../services/forumService', () => ({ forumService: { getTopics } }))
vi.mock('../store/hooks', () => ({
  useAppSelector: (sel: (s: unknown) => unknown) => sel({ auth: { user: { id: state.currentUserId } } }),
}))
vi.mock('../components/feedback/ResultState', () => ({ default: () => null }))
// t returns the key, but interpolates {{skill}} so per-skill aria-labels stay unique.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: { skill?: string; count?: number }) =>
      opts?.skill ? `${k}:${opts.skill}` : k,
  }),
}))

const profile = (over: Partial<PublicProfile> = {}): PublicProfile => ({
  id: 'u1',
  name: 'Bob',
  role: 'Developer',
  avatarUrl: null,
  title: null,
  bio: null,
  location: null,
  skills: ['React', 'Go'],
  skillEndorsements: [
    { skill: 'React', count: 2, endorsedByViewer: false },
    { skill: 'Go', count: 0, endorsedByViewer: false },
  ],
  email: null,
  website: null,
  linkedIn: null,
  github: null,
  phone: null,
  memberSince: '2026-01-01T00:00:00Z',
  averageRating: null,
  reviewCount: 0,
  reputationPoints: 0,
  badges: [],
  ...over,
})

describe('UserProfilePage skill endorsements', () => {
  beforeEach(() => {
    state.currentUserId = 'me'
    getReputationHistory.mockReset().mockResolvedValue({ entries: [], ledgerTotal: 0 })
    getTopics.mockReset().mockResolvedValue({ items: [] })
    endorseSkill.mockReset().mockResolvedValue({ skill: 'React', endorsed: true, count: 3 })
    getPublicProfile.mockReset().mockResolvedValue(profile())
  })

  it('endorsing a skill on someone else\'s profile calls the API and updates the count', async () => {
    render(<UserProfilePage />)
    const btn = await screen.findByLabelText('endorse.add:React')

    fireEvent.click(btn)

    await waitFor(() => expect(endorseSkill).toHaveBeenCalledWith('u1', 'React'))
    // The button flips to the "remove" state and the count becomes 3.
    expect(await screen.findByLabelText('endorse.remove:React')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
  })

  it('does not offer endorse buttons on your own profile', async () => {
    state.currentUserId = 'u1' // viewing myself
    render(<UserProfilePage />)
    await screen.findByText('React')

    expect(screen.queryByLabelText('endorse.add:React')).toBeNull()
    expect(screen.queryByLabelText('endorse.remove:React')).toBeNull()
    // The count is still shown.
    expect(screen.getByText('2')).toBeTruthy()
  })

  it('shows an already-endorsed skill as pressed', async () => {
    getPublicProfile.mockResolvedValue(
      profile({ skillEndorsements: [
        { skill: 'React', count: 1, endorsedByViewer: true },
        { skill: 'Go', count: 0, endorsedByViewer: false },
      ] }),
    )
    render(<UserProfilePage />)

    const btn = await screen.findByLabelText('endorse.remove:React')
    expect(btn.getAttribute('aria-pressed')).toBe('true')
  })
})
