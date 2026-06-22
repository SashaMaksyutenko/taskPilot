import { useEffect, useState } from 'react'
import Navbar from '../components/Navbar'
import { adminService } from '../services/adminService'
import { useAppSelector } from '../store/hooks'
import { ROLES, type AdminUser } from '../types/admin'

/**
 * Admin user management: list users, change roles and ban/unban accounts.
 * Only reachable by admins (guarded by AdminRoute; backend enforces RBAC too).
 */
export default function AdminPage() {
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [users, setUsers] = useState<AdminUser[]>([])

  const load = () => {
    adminService.getUsers().then(setUsers).catch(() => {})
  }

  useEffect(load, [])

  const changeRole = async (id: string, role: string) => {
    await adminService.changeRole(id, role).catch(() => {})
    load()
  }

  const toggleBan = async (u: AdminUser) => {
    if (u.isActive) await adminService.ban(u.id).catch(() => {})
    else await adminService.unban(u.id).catch(() => {})
    load()
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">Admin · Users ({users.length})</h1>

        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500 dark:bg-slate-700/50 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">Email</th>
                <th className="px-4 py-3">Role</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
              {users.map((u) => (
                <tr key={u.id}>
                  <td className="px-4 py-3 font-medium">{u.name}</td>
                  <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{u.email}</td>
                  <td className="px-4 py-3">
                    <select
                      value={u.role}
                      onChange={(e) => changeRole(u.id, e.target.value)}
                      className="rounded-lg border border-slate-300 bg-white px-2 py-1 outline-none dark:border-slate-600 dark:bg-slate-900"
                    >
                      {ROLES.map((r) => (
                        <option key={r} value={r}>
                          {r}
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
                      {u.isActive ? 'Active' : 'Banned'}
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
                        {u.isActive ? 'Ban' : 'Unban'}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </main>
    </div>
  )
}
