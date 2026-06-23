import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import Navbar from '../components/Navbar'
import { adminService } from '../services/adminService'
import type { AuditLog } from '../types/admin'

const PAGE_SIZE = 25

/**
 * Admin audit log: a paginated, filterable feed of recorded actions
 * (logins, registrations, moderation, etc.). Admin-only (AdminRoute guard +
 * backend RBAC).
 */
export default function AuditPage() {
  const [logs, setLogs] = useState<AuditLog[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [action, setAction] = useState('') // applied filter
  const [actionInput, setActionInput] = useState('') // text being typed
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    adminService
      .getAudit(page, PAGE_SIZE, action || undefined)
      .then((res) => {
        setLogs(res.items)
        setTotal(res.total)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [page, action])

  const applyFilter = () => {
    setPage(1) // filtering resets to the first page
    setAction(actionInput.trim())
  }

  const clearFilter = () => {
    setActionInput('')
    setAction('')
    setPage(1)
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-6xl px-6 py-8">
        <div className="mb-6 flex items-center gap-3">
          <h1 className="text-2xl font-bold">Admin · Audit log</h1>
          <span className="text-sm text-slate-400">({total} events)</span>
          <Link to="/admin" className="ml-auto text-sm text-slate-500 hover:underline dark:text-slate-400">
            ← Users
          </Link>
        </div>

        {/* Filter by action code */}
        <div className="mb-4 flex gap-2">
          <input
            value={actionInput}
            onChange={(e) => setActionInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && applyFilter()}
            placeholder="Filter by action, e.g. auth.login.failed"
            className="min-w-0 flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-800 sm:max-w-sm"
          />
          <button
            onClick={applyFilter}
            className="rounded-lg bg-[#1E2A44] px-4 text-sm font-semibold text-white hover:bg-[#27345a]"
          >
            Filter
          </button>
          {action && (
            <button
              onClick={clearFilter}
              className="rounded-lg border border-slate-300 px-4 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
            >
              Clear
            </button>
          )}
        </div>

        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500 dark:bg-slate-700/50 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3">Time (UTC)</th>
                <th className="px-4 py-3">Action</th>
                <th className="px-4 py-3">Actor</th>
                <th className="px-4 py-3">IP</th>
                <th className="px-4 py-3">Details</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
              {loading ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                    Loading…
                  </td>
                </tr>
              ) : logs.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                    No events.
                  </td>
                </tr>
              ) : (
                logs.map((l) => (
                  <tr key={l.id}>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-500 dark:text-slate-400">
                      {new Date(l.createdAt).toLocaleString()}
                    </td>
                    <td className="px-4 py-3">
                      <span className="rounded bg-slate-100 px-2 py-0.5 text-[11px] font-semibold dark:bg-slate-700">
                        {l.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                      {l.actorEmail ?? <span className="text-slate-400">system</span>}
                    </td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{l.ipAddress ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{l.details ?? '—'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="mt-4 flex items-center justify-between text-sm">
          <span className="text-slate-500 dark:text-slate-400">
            Page {page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="rounded-lg border border-slate-300 px-3 py-1.5 font-semibold disabled:opacity-40 dark:border-slate-600"
            >
              Prev
            </button>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="rounded-lg border border-slate-300 px-3 py-1.5 font-semibold disabled:opacity-40 dark:border-slate-600"
            >
              Next
            </button>
          </div>
        </div>
      </main>
    </div>
  )
}
