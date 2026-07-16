import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Avatar from '../components/Avatar'
import RoleChart from '../components/charts/RoleChart'
import StatusChart from '../components/charts/StatusChart'
import SignupsChart from '../components/charts/SignupsChart'
import ActivityChart from '../components/charts/ActivityChart'
import StatsPanel from '../components/charts/StatsPanel'
import UserContextMenu from '../components/menus/UserContextMenu'
import WarnUserModal from '../components/modals/WarnUserModal'
import { adminService } from '../services/adminService'
import { forumService } from '../services/forumService'
import { statsService } from '../services/statsService'
import { useAppSelector } from '../store/hooks'
import { ROLES, type AdminUser, type Appeal } from '../types/admin'
import type { ForumReport } from '../types/forum'
import type { AdminStats, DayActivity } from '../types/stats'

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
  // Trend charts: activity data and the selected period (in days).
  const [activity, setActivity] = useState<DayActivity[]>([])
  const [activityDays, setActivityDays] = useState(30)
  // User-list filters and sort.
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [sort, setSort] = useState('newest')

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const load = (p: number) => {
    adminService
      .getUsers(p, PAGE_SIZE, { search: search.trim(), role: roleFilter, status: statusFilter, sort })
      .then((r) => {
        setUsers(r.items)
        setTotal(r.total)
      })
      .catch(() => {})
  }

  // Reload on page or filter change; debounce so typing a search isn't chatty.
  useEffect(() => {
    const id = setTimeout(() => load(page), 250)
    return () => clearTimeout(id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, roleFilter, statusFilter, sort])

  // Changing a filter jumps back to the first page.
  const changeFilter = (setter: (v: string) => void) => (value: string) => {
    setter(value)
    setPage(1)
  }

  // Fetch the activity trend whenever the selected period changes.
  useEffect(() => {
    statsService.getActivity(activityDays).then(setActivity).catch(() => {})
  }, [activityDays])

  // Refresh stats on mount and then periodically so "online now" stays live
  // as people connect/disconnect (heavy aggregates are cached server-side).
  useEffect(() => {
    const refresh = () => statsService.getAdmin().then(setStats).catch(() => {})
    refresh()
    const timer = setInterval(refresh, 10000)
    return () => clearInterval(timer)
  }, [])

  // Pending moderation appeals queue.
  const [appeals, setAppeals] = useState<Appeal[]>([])
  const loadAppeals = () => adminService.getAppeals('Pending').then(setAppeals).catch(() => {})

  // Organisation-wide marketplace report (admin only, enforced server-side too).
  const downloadMarketplaceReport = async (format: 'pdf' | 'xlsx') => {
    const blob = await (format === 'pdf'
      ? adminService.marketplaceReportPdf()
      : adminService.marketplaceReportXlsx()
    ).catch(() => null)
    if (!blob) return

    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `marketplace-report.${format}`
    a.click()
    URL.revokeObjectURL(url)
  }
  useEffect(() => {
    loadAppeals()
  }, [])

  const resolveAppeal = async (id: string, approve: boolean) => {
    await adminService.resolveAppeal(id, approve).catch(() => {})
    loadAppeals()
    load(page) // an approval may have lifted a warning / changed a ban
  }

  // Pending forum reports queue.
  const [reports, setReports] = useState<ForumReport[]>([])
  const loadReports = () => forumService.getReports('Pending').then(setReports).catch(() => {})
  useEffect(() => {
    loadReports()
  }, [])

  const resolveReport = async (id: string, dismiss: boolean) => {
    await forumService.resolveReport(id, dismiss).catch(() => {})
    loadReports()
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
    <>
    <div className="mx-auto max-w-5xl px-6 py-8">
        <div className="mb-6 flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-bold">{t('admin.usersTitle', { count: total })}</h1>
          <button
            onClick={() => downloadMarketplaceReport('pdf')}
            className="ml-auto rounded-lg border border-border px-3 py-1.5 text-sm font-semibold hover:bg-canvas"
          >
            {t('admin.marketReportPdf')}
          </button>
          <button
            onClick={() => downloadMarketplaceReport('xlsx')}
            className="rounded-lg border border-border px-3 py-1.5 text-sm font-semibold hover:bg-canvas"
          >
            {t('admin.marketReportXlsx')}
          </button>
          <Link
            to="/admin/audit"
            className="rounded-lg border border-border px-3 py-1.5 text-sm font-semibold hover:bg-canvas"
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
          {stats && <StatusChart usersByStatus={stats.usersByStatus} />}
        </div>

        {/* Trend charts with a shared period selector */}
        <div className="mb-6">
          <div className="mb-2 flex items-center gap-2">
            <span className="text-sm font-semibold text-muted">{t('admin.period')}</span>
            <select
              value={activityDays}
              onChange={(e) => setActivityDays(Number(e.target.value))}
              className="rounded-lg border border-border bg-canvas px-3 py-1.5 text-sm text-foreground outline-none focus:border-primary"
            >
              <option value={7}>{t('admin.period7')}</option>
              <option value={30}>{t('admin.period30')}</option>
              <option value={90}>{t('admin.period90')}</option>
              <option value={365}>{t('admin.period365')}</option>
            </select>
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <SignupsChart activity={activity} />
            <ActivityChart activity={activity} />
          </div>
        </div>

        {/* Pending appeals queue */}
        {appeals.length > 0 && (
          <section className="mb-6 rounded-xl border border-amber-300 bg-amber-50 p-5 dark:border-amber-700 dark:bg-amber-950/30">
            <h2 className="mb-3 font-bold text-amber-800 dark:text-amber-200">
              {t('appeal.queueTitle', { count: appeals.length })}
            </h2>
            <ul className="space-y-3">
              {appeals.map((a) => (
                <li key={a.id} className="rounded-lg bg-surface p-3 text-sm">
                  <div className="flex items-center gap-2">
                    <Link to={`/users/${a.userId}`} className="font-semibold hover:underline">{a.userName}</Link>
                    <span className="text-xs text-muted">{new Date(a.createdAt).toLocaleString()}</span>
                  </div>
                  {a.warningReason && (
                    <p className="mt-1 text-xs text-muted">
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

        {/* Pending forum reports queue */}
        {reports.length > 0 && (
          <section className="mb-6 rounded-xl border border-red-300 bg-red-50 p-5 dark:border-red-800 dark:bg-red-950/30">
            <h2 className="mb-3 font-bold text-red-800 dark:text-red-200">
              {t('report.queueTitle', { count: reports.length })}
            </h2>
            <ul className="space-y-3">
              {reports.map((r) => (
                <li key={r.id} className="rounded-lg bg-surface p-3 text-sm">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link to={`/users/${r.reporterId}`} className="font-semibold hover:underline">{r.reporterName}</Link>
                    <span className="text-xs text-muted">{t('report.reported')}</span>
                    <Link to={`/forum/${r.topicId}#reply-${r.replyId}`} className="text-xs text-primary hover:underline">
                      {r.topicTitle}
                    </Link>
                    <span className="ml-auto text-xs text-muted">{new Date(r.createdAt).toLocaleString()}</span>
                  </div>
                  <p className="mt-2 rounded border-l-4 border-border bg-canvas px-3 py-1.5 text-xs text-muted">
                    <span className="font-semibold">{r.replyAuthorName}:</span> {r.replyExcerpt}
                  </p>
                  {r.reason && (
                    <p className="mt-1 text-xs text-muted">
                      {t('report.reasonLabel')}: {r.reason}
                    </p>
                  )}
                  <div className="mt-3 flex gap-2">
                    <button
                      onClick={() => resolveReport(r.id, false)}
                      className="rounded-lg bg-green-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-green-700"
                    >
                      {t('report.resolve')}
                    </button>
                    <button
                      onClick={() => resolveReport(r.id, true)}
                      className="rounded-lg border border-border px-4 py-1.5 text-xs font-semibold text-foreground hover:bg-canvas"
                    >
                      {t('report.dismiss')}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* User-list filters */}
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <input
            value={search}
            onChange={(e) => changeFilter(setSearch)(e.target.value)}
            placeholder={t('admin.searchPlaceholder')}
            className="min-w-0 flex-1 rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
          />
          <select
            value={roleFilter}
            onChange={(e) => changeFilter(setRoleFilter)(e.target.value)}
            className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
          >
            <option value="">{t('admin.allRoles')}</option>
            {ROLES.map((r) => (
              <option key={r} value={r}>{t(`admin.roles.${r}`, r)}</option>
            ))}
          </select>
          <select
            value={statusFilter}
            onChange={(e) => changeFilter(setStatusFilter)(e.target.value)}
            className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
          >
            <option value="">{t('admin.allStatuses')}</option>
            <option value="active">{t('admin.statusActive')}</option>
            <option value="banned">{t('admin.statusBanned')}</option>
            <option value="muted">{t('admin.statusMuted')}</option>
          </select>
          <select
            value={sort}
            onChange={(e) => changeFilter(setSort)(e.target.value)}
            className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
          >
            <option value="newest">{t('admin.sortNewest')}</option>
            <option value="oldest">{t('admin.sortOldest')}</option>
            <option value="name">{t('admin.sortName')}</option>
            <option value="role">{t('admin.sortRole')}</option>
          </select>
        </div>

        <div className="overflow-hidden rounded-xl border border-border bg-surface">
          <table className="w-full text-sm">
            <thead className="bg-canvas text-left text-xs uppercase text-muted">
              <tr>
                <th className="px-4 py-3">{t('admin.name')}</th>
                <th className="px-4 py-3">{t('admin.email')}</th>
                <th className="px-4 py-3">{t('admin.role')}</th>
                <th className="px-4 py-3">{t('admin.status')}</th>
                <th className="px-4 py-3 text-right">{t('admin.actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
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
                  <td className="px-4 py-3 text-muted">{u.email}</td>
                  <td className="px-4 py-3">
                    <select
                      value={u.role}
                      onChange={(e) => changeRole(u.id, e.target.value)}
                      className="rounded-lg border border-border bg-canvas px-2 py-1 text-foreground outline-none"
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
                      <div className="mt-1 text-[11px] text-muted">
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
              className="rounded-lg border border-border px-4 py-1.5 font-semibold transition hover:bg-canvas disabled:opacity-40"
            >
              {t('audit.prev')}
            </button>
            <span className="text-muted">
              {t('audit.pageOf', { page, total: totalPages })}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="rounded-lg border border-border px-4 py-1.5 font-semibold transition hover:bg-canvas disabled:opacity-40"
            >
              {t('audit.next')}
            </button>
          </div>
        )}
      </div>

      {warnTarget && (
        <WarnUserModal
          userName={warnTarget.name}
          onClose={() => setWarnTarget(null)}
          onSubmit={submitWarning}
        />
      )}
    </>
  )
}
