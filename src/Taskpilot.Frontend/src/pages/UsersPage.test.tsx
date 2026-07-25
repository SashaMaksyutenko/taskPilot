import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import UsersPage from './UsersPage'
import type { UserDirectoryItem } from '../services/userService'

const { getDirectory } = vi.hoisted(() => ({ getDirectory: vi.fn() }))

vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }))
vi.mock('../services/userService', () => ({ userService: { getDirectory } }))
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: unknown) =>
      opts && typeof opts === 'object' && 'page' in (opts as Record<string, unknown>)
        ? `${k}:${(opts as { page: number }).page}`
        : k,
  }),
}))

const user = (id: string, name: string): UserDirectoryItem => ({
  id, name, role: 'Developer', title: null, location: null, avatarUrl: null, memberSince: '2026-01-01T00:00:00Z',
})
const pageOf = (items: UserDirectoryItem[], total: number) => ({ items, total, page: 1, pageSize: 24 })

describe('UsersPage', () => {
  beforeEach(() => {
    getDirectory.mockReset().mockResolvedValue(pageOf([user('u1', 'Alice'), user('u2', 'Bob')], 2))
  })

  it('lists users from the directory', async () => {
    render(<UsersPage />)
    expect(await screen.findByText('Alice')).toBeTruthy()
    expect(screen.getByText('Bob')).toBeTruthy()
  })

  it('searches by name (debounced)', async () => {
    render(<UsersPage />)
    await screen.findByText('Alice')

    fireEvent.change(screen.getByPlaceholderText('users.searchPlaceholder'), { target: { value: 'bo' } })

    await waitFor(() => expect(getDirectory).toHaveBeenCalledWith(expect.objectContaining({ search: 'bo', page: 1 })))
  })

  it('paginates to the next page', async () => {
    getDirectory.mockResolvedValue(pageOf([user('u1', 'Alice')], 50)) // >1 page
    render(<UsersPage />)
    await screen.findByText('Alice')

    fireEvent.click(screen.getByText('audit.next'))

    await waitFor(() => expect(getDirectory).toHaveBeenCalledWith(expect.objectContaining({ page: 2 })))
  })
})
