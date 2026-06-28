import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Navbar from '../components/Navbar'
import RoleChart from '../components/RoleChart'
import StatsPanel from '../components/StatsPanel'
import UserContextMenu from '../components/UserContextMenu'
import { adminService } from '../services/adminService'
import { statsService } from '../services/statsService'
import { useAppSelector } from '../store/hooks'
import { ROLES, type AdminUser } from '../types/admin'
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

  const changeRole = async (id: string, role: string) => {
    await adminService.changeRole(id, role).catch(() => {})
    load(page)
  }

  const toggleBan = async (u: AdminUser) => {
    if (u.isActive) await adminService.ban(u.id).catch(() => {})
    else await adminService.unban(u.id).catch(() => {})
    load(page)
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

        <div className="mb-6 grid gap-4 lg:grid-cols-2">
          <StatsPanel stats={stats} />
          {stats && <RoleChart usersByRole={stats.usersByRole} />}
        </div>

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
                  onToggleBan={() => toggleBan(u)}
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
                  </td>
                  <td className="px-4 py-3 text-right">
                    {u.id !== currentUserId && (
                      <button
                        onClick={() => toggleBan(u)}
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
    </div>
  )
}
