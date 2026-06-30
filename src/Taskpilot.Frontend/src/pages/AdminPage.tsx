import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Navbar from '../components/Navbar'
import RoleChart from '../components/RoleChart'
import StatsPanel from '../components/StatsPanel'
import UserContextMenu from '../components/UserContextMenu'
import WarnUserModal from '../components/WarnUserModal'
import { adminService } from '../services/adminService'
import { statsService } from '../services/statsService'
import { useAppSelector } from '../store/hooks'
import { ROLES, type AdminUser, type Appeal } from '../types/admin'
import type { AdminStats } from '../types/stats'

/**
 * Admin user management: list users, change roles and ban/unban accounts.
 * Only reachable by admins (guarded by AdminRoute; backend enforces RBAC too).
 */
export default function AdminPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const PAGE_SIZE = 20
  const [users, setUsers] = useState<AdminUser[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [stats, setStats] = useState<AdminStats | null>(null)

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const load = (p: number) => {
    adminService
      .getUsers(p, PAGE_SIZE)
      .then((r) => {
        setUsers(r.items)
        setTotal(r.total)
      })
      .catch(() => {})
  }

  useEffect(() => load(page), [page])

  useEffect(() => {
    statsService.getAdmin().then(setStats).catch(() => {})
  }, [])

  // Pending moderation appeals queue.
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const loadAppeals = () => adminService.getAppeals('Pending').then(setAppeals).catch(() => {})
  useEffect(() => {
    loadAppeals()
  }, [])

  const resolveAppeal = async (id: string, approve: boolean) => {
    await adminService.resolveAppeal(id, approve).catch(() => {})
    loadAppeals()
    load(page) // an approval may have lifted a warning / changed a ban
  }

  const changeRole = async (id: string, role: string) => {
    await adminService.changeRole(id, role).catch(() => {})
    load(page)
  }

  const banUser = async (u: AdminUser, days?: number) => {
    await adminService.ban(u.id, days).catch(() => {})
    load(page)
  }

  const unbanUser = async (u: AdminUser) => {
    await adminService.unban(u.id).catch(() => {})
    load(page)
  }

  const isMuted = (u: AdminUser) => u.mutedUntil != null && new Date(u.mutedUntil) > new Date()

  const muteUser = async (u: AdminUser, days?: number) => {
    await adminService.mute(u.id, days).catch(() => {})
    load(page)
  }

  const unmuteUser = async (u: AdminUser) => {
    await adminService.unmute(u.id).catch(() => {})
    load(page)
  }

  // Warn flow: target user for the modal + transient feedback message.
  const [warnTarget, setWarnTarget] = useState<AdminUser | null>(null)
  const [warnMsg, setWarnMsg] = useState('')

  const submitWarning = async (reason: string) => {
    if (!warnTarget) return
    const target = warnTarget
    const res = await adminService.issueWarning(target.id, reason).catch(() => null)
    setWarnTarget(null)
    if (res) {
      setWarnMsg(
        res.autoBanned
          ? t('warn.autoBanned', { name: target.name, count: res.warningCount })
          : t('warn.issued', { name: target.name, count: res.warningCount }),
      )
      load(page) // reflect a possible auto-ban in the table
      setTimeout(() => setWarnMsg(''), 6000)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-6 py-8">
        <div className="mb-6 flex items-center gap-3">
          <h1 className="text-2xl font-bold">{t('admin.usersTitle', { count: total })}</h1>
          <Link
            to="/admin/audit"
            className="ml-auto rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
          >
            {t('admin.auditLog')}
          </Link>
        </div>

        {warnMsg && (
          <div className="mb-6 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-700 dark:bg-amber-950/30 dark:text-amber-200">
            {warnMsg}
          </div>
        )}

        <div className="mb-6 grid gap-4 lg:grid-cols-2">
          <StatsPanel stats={stats} />
          {stats && <RoleChart usersByRole={stats.usersByRole} />}
        </div>

        {/* Pending appeals queue */}
        {appeals.length > 0 && (
          <section className="mb-6 rounded-xl border border-amber-300 bg-amber-50 p-5 dark:border-amber-700 dark:bg-amber-950/30">
            <h2 className="mb-3 font-bold text-amber-800 dark:text-amber-200">
              {t('appeal.queueTitle', { count: appeals.length })}
            </h2>
            <ul className="space-y-3">
              {appeals.map((a) => (
                <li key={a.id} className="rounded-lg bg-white p-3 text-sm dark:bg-slate-800">
                  <div className="flex items-center gap-2">
                    <Link to={`/users/${a.userId}`} className="font-semibold hover:underline">{a.userName}</Link>
                    <span className="text-xs text-slate-400">{new Date(a.createdAt).toLocaleString()}</span>
                  </div>
                  {a.warningReason && (
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                      {t('appeal.warningLabel')}: {a.warningReason}
                    </p>
                  )}
                  <p className="mt-1 whitespace-pre-wrap">{a.message}</p>
                  <div className="mt-3 flex gap-2">
                    <button
                      onClick={() => resolveAppeal(a.id, true)}
                      className="rounded-lg bg-green-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-green-700"
                    >
                      {t('appeal.approve')}
                    </button>
                    <button
                      onClick={() => resolveAppeal(a.id, false)}
                      className="rounded-lg border border-red-300 px-4 py-1.5 text-xs font-semibold text-red-600 hover:bg-red-50 dark:border-red-700 dark:hover:bg-red-950"
                    >
                      {t('appeal.reject')}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}

        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500 dark:bg-slate-700/50 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3">{t('admin.name')}</th>
                <th className="px-4 py-3">{t('admin.email')}</th>
                <th className="px-4 py-3">{t('admin.role')}</th>
                <th className="px-4 py-3">{t('admin.status')}</th>
                <th className="px-4 py-3 text-right">{t('admin.actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
              {users.map((u) => (
                <UserContextMenu
                  key={u.id}
                  isActive={u.isActive}
                  canModerate={u.id !== currentUserId}
                  onViewProfile={() => navigate(`/users/${u.id}`)}
                  onChangeRole={(role) => changeRole(u.id, role)}
                  onBan={(days) => banUser(u, days)}
                  onUnban={() => unbanUser(u)}
                  onWarn={() => setWarnTarget(u)}
                  isMuted={isMuted(u)}
                  onMute={(days) => muteUser(u, days)}
                  onUnmute={() => unmuteUser(u)}
                >
                <tr>
                  <td className="px-4 py-3 font-medium">
                    <Link to={`/users/${u.id}`} className="flex items-center gap-2 hover:underline">
                      <Avatar name={u.name} src={u.avatarUrl} size={28} />
                      {u.name}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{u.email}</td>
                  <td className="px-4 py-3">
                    <select
                      value={u.role}
                      onChange={(e) => changeRole(u.id, e.target.value)}
                      className="rounded-lg border border-slate-300 bg-white px-2 py-1 outline-none dark:border-slate-600 dark:bg-slate-900"
                    >
                      {ROLES.map((r) => (
                        <option key={r} value={r}>
                          {t(`admin.roles.${r}`, r)}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                        u.isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'
                      }`}
                    >
                      {u.isActive ? t('admin.active') : t('admin.banned')}
                    </span>
                    {!u.isActive && u.bannedUntil && (
                      <div className="mt-1 text-[11px] text-slate-400">
                        {t('admin.bannedUntil', { date: new Date(u.bannedUntil).toLocaleDateString() })}
                      </div>
                    )}
                    {isMuted(u) && (
                      <div className="mt-1 text-[11px] font-semibold text-amber-600">
                        🔇 {t('admin.mutedUntil', { date: new Date(u.mutedUntil!).toLocaleDateString() })}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right">
                    {u.id !== currentUserId && (
                      <button
                        onClick={() => (u.isActive ? banUser(u) : unbanUser(u))}
                        className={`rounded-lg px-3 py-1 text-xs font-semibold ${
                          u.isActive
                            ? 'border border-red-300 text-red-600 hover:bg-red-50 dark:border-red-700 dark:hover:bg-red-950'
                            : 'border border-green-300 text-green-600 hover:bg-green-50 dark:border-green-700 dark:hover:bg-green-950'
                        }`}
                      >
                        {u.isActive ? t('admin.ban') : t('admin.unban')}
                      </button>
                    )}
                  </td>
                </tr>
                </UserContextMenu>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="mt-6 flex items-center justify-center gap-4 text-sm">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="rounded-lg border border-slate-300 px-4 py-1.5 font-semibold transition hover:bg-white disabled:opacity-40 dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('audit.prev')}
            </button>
            <span className="text-slate-500 dark:text-slate-400">
              {t('audit.pageOf', { page, total: totalPages })}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="rounded-lg border border-slate-300 px-4 py-1.5 font-semibold transition hover:bg-white disabled:opacity-40 dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('audit.next')}
            </button>
          </div>
        )}
      </main>

      {warnTarget && (
        <WarnUserModal
          userName={warnTarget.name}
          onClose={() => setWarnTarget(null)}
          onSubmit={submitWarning}
        />
      )}
    </div>
  )
}
