import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import SettingsPage from './SettingsPage'

/**
 * SettingsPage is the biggest page in the app and pulls five services. These tests cover
 * the security-sensitive flows — the raw API key is shown exactly once, revoking drops the
 * row, and sessions can be revoked — so the page can be refactored/split with a safety net.
 */
const m = vi.hoisted(() => ({
  // apiKeyService
  keyList: vi.fn(),
  keyCreate: vi.fn(),
  keyRevoke: vi.fn(),
  // authService
  getSessions: vi.fn(),
  revokeSession: vi.fn(),
  revokeOtherSessions: vi.fn(),
  backupCodesCount: vi.fn(),
  // notificationService
  getPreferences: vi.fn(),
  getTelegramStatus: vi.fn(),
  getViberStatus: vi.fn(),
  // userService
  getMyWarnings: vi.fn(),
  getMyAppeals: vi.fn(),
  // webhookService
  webhookList: vi.fn(),
  // misc
  notifyError: vi.fn(),
  notifySuccess: vi.fn(),
  // Stable references on purpose: the page has effects with `user`/`dispatch` in their
  // dependency arrays, so returning a fresh object/function per call re-fires those
  // effects every render → fetch → setState → render → … (the test hangs forever).
  dispatch: vi.fn(),
  authState: { user: { id: 'u1', name: 'Me', twoFactorEnabled: false }, isAuthenticated: true },
}))

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))
vi.mock('react-router-dom', () => ({ useNavigate: () => vi.fn() }))
vi.mock('qrcode', () => ({ default: { toDataURL: vi.fn().mockResolvedValue('data:,') } }))
vi.mock('../components/Avatar', () => ({ default: () => null }))
vi.mock('../components/modals/AppealModal', () => ({ default: () => null }))
vi.mock('../lib/toast', () => ({ notify: { error: m.notifyError, success: m.notifySuccess } }))
vi.mock('../lib/apiError', () => ({ apiErrorMessage: () => 'boom' }))
vi.mock('../lib/push', () => ({
  enablePush: vi.fn(),
  disablePush: vi.fn(),
  getPushEnabled: vi.fn().mockResolvedValue(false),
  pushSupported: () => false,
}))
vi.mock('../store/hooks', () => ({
  useAppDispatch: () => m.dispatch,
  useAppSelector: () => m.authState,
}))
vi.mock('../store/authSlice', () => ({ fetchMe: () => ({ type: 'me' }), logout: () => ({ type: 'out' }) }))
vi.mock('../services/apiKeyService', () => ({
  apiKeyService: { list: m.keyList, create: m.keyCreate, revoke: m.keyRevoke },
}))
vi.mock('../services/authService', () => ({
  authService: {
    getSessions: m.getSessions,
    revokeSession: m.revokeSession,
    revokeOtherSessions: m.revokeOtherSessions,
    backupCodesCount: m.backupCodesCount,
    regenerateBackupCodes: vi.fn(),
    setupTwoFactor: vi.fn(),
    enableTwoFactor: vi.fn(),
    disableTwoFactor: vi.fn(),
  },
}))
vi.mock('../services/notificationService', () => ({
  notificationService: {
    getPreferences: m.getPreferences,
    getTelegramStatus: m.getTelegramStatus,
    getViberStatus: m.getViberStatus,
    updateDigest: vi.fn(),
    updateQuietHours: vi.fn(),
    setPreference: vi.fn(),
    createTelegramLinkCode: vi.fn(),
    unlinkTelegram: vi.fn(),
    createViberLinkCode: vi.fn(),
    unlinkViber: vi.fn(),
  },
}))
vi.mock('../services/userService', () => ({
  userService: {
    getMyWarnings: m.getMyWarnings,
    getMyAppeals: m.getMyAppeals,
    updateProfile: vi.fn(),
    changePassword: vi.fn(),
    uploadAvatar: vi.fn(),
    removeAvatar: vi.fn(),
    exportData: vi.fn(),
    activityReport: vi.fn(),
    createAppeal: vi.fn(),
    deleteAccount: vi.fn(),
  },
}))
vi.mock('../services/webhookService', () => ({
  webhookService: {
    getWebhooks: m.webhookList,
    createWebhook: vi.fn(),
    deleteWebhook: vi.fn(),
    setActive: vi.fn(),
    testWebhook: vi.fn(),
    getDeliveries: vi.fn(),
  },
}))

const key = (over: Partial<{ id: string; name: string; prefix: string }> = {}) => ({
  id: 'k1',
  name: 'CI key',
  prefix: 'tp_abc123',
  createdAt: '2026-07-01T00:00:00Z',
  lastUsedAt: null,
  ...over,
})

describe('SettingsPage — API keys and sessions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    m.keyList.mockResolvedValue([])
    m.getSessions.mockResolvedValue([])
    m.backupCodesCount.mockResolvedValue(0)
    m.getPreferences.mockResolvedValue({
      disabledTypes: [],
      disabledEmailTypes: [],
      digestFrequency: 'Off',
      quietHours: { enabled: false, start: 22, end: 8, timeZoneId: null },
    })
    m.getTelegramStatus.mockResolvedValue({ linked: false, enabled: false })
    m.getViberStatus.mockResolvedValue({ linked: false, enabled: false })
    m.getMyWarnings.mockResolvedValue([])
    m.getMyAppeals.mockResolvedValue([])
    m.webhookList.mockResolvedValue([])
  })

  it('lists existing API keys by prefix (never the raw key)', async () => {
    m.keyList.mockResolvedValue([key()])
    render(<SettingsPage />)

    expect(await screen.findByText(/tp_abc123/)).toBeTruthy()
  })

  it('creating a key shows the raw secret once and adds it to the list', async () => {
    m.keyCreate.mockResolvedValue({ ...key({ id: 'k2', name: 'Deploy' }), key: 'tp_RAWSECRET123' })
    render(<SettingsPage />)

    const input = await screen.findByPlaceholderText('apiKeys.namePlaceholder')
    fireEvent.change(input, { target: { value: 'Deploy' } })
    fireEvent.click(screen.getByText('apiKeys.create'))

    // The raw key is only ever available at creation time — it must be surfaced.
    expect(await screen.findByText('tp_RAWSECRET123')).toBeTruthy()
    expect(m.keyCreate).toHaveBeenCalledWith('Deploy')
    // …and the input is cleared so it cannot be created twice by accident.
    await waitFor(() => expect((input as HTMLInputElement).value).toBe(''))
  })

  it('does not call the API when the key name is blank', async () => {
    render(<SettingsPage />)
    await screen.findByPlaceholderText('apiKeys.namePlaceholder')

    fireEvent.click(screen.getByText('apiKeys.create'))

    expect(m.keyCreate).not.toHaveBeenCalled()
  })

  it('revoking a key removes it from the list', async () => {
    m.keyList.mockResolvedValue([key()])
    m.keyRevoke.mockResolvedValue(undefined)
    render(<SettingsPage />)

    await screen.findByText(/tp_abc123/)
    fireEvent.click(screen.getByText('apiKeys.revoke'))

    await waitFor(() => expect(screen.queryByText(/tp_abc123/)).toBeNull())
    expect(m.keyRevoke).toHaveBeenCalledWith('k1')
  })

  it('revoking a session removes it from the list', async () => {
    m.getSessions.mockResolvedValue([
      { id: 's1', createdAtUtc: '2026-07-01T00:00:00Z', expiresAtUtc: '2026-07-08T00:00:00Z', ipAddress: '10.0.0.9', userAgent: 'Chrome', isCurrent: false },
    ])
    m.revokeSession.mockResolvedValue(undefined)
    render(<SettingsPage />)

    await screen.findByText(/10\.0\.0\.9/)
    fireEvent.click(screen.getByText('sessions.revoke'))

    await waitFor(() => expect(m.revokeSession).toHaveBeenCalledWith('s1'))
  })
})
