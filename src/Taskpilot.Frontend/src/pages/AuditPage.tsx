import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { adminService } from '../services/adminService'
import type { AuditLog } from '../types/admin'

const PAGE_SIZE = 25

/**
 * Admin audit log: a paginated, filterable feed of recorded actions
 * (logins, registrations, moderation, etc.). Admin-only (AdminRoute guard +
 * backend RBAC).
 */
export default function AuditPage() {
  const { t } = useTranslation()
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

  // Exports the most recent audit entries (the backend caps the slice).
  const downloadAudit = async (format: 'pdf' | 'xlsx') => {
    const blob = await (format === 'pdf'
      ? adminService.auditReportPdf()
      : adminService.auditReportXlsx()
    ).catch(() => null)
    if (!blob) return

    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `audit-log.${format}`
    a.click()
    URL.revokeObjectURL(url)
  }

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
    <div className="mx-auto max-w-6xl px-6 py-8">
        <div className="mb-6 flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-bold">{t('audit.title')}</h1>
          <span className="text-sm text-muted">{t('audit.events', { count: total })}</span>
          <button
            onClick={() => downloadAudit('pdf')}
            className="ml-auto rounded-lg border border-border px-3 py-1.5 text-sm font-semibold hover:bg-canvas"
          >
            {t('audit.exportPdf')}
          </button>
          <button
            onClick={() => downloadAudit('xlsx')}
            className="rounded-lg border border-border px-3 py-1.5 text-sm font-semibold hover:bg-canvas"
          >
            {t('audit.exportXlsx')}
          </button>
          <Link to="/admin" className="text-sm text-muted hover:underline">
            {t('audit.backToUsers')}
          </Link>
        </div>

        {/* Filter by action code */}
        <div className="mb-4 flex gap-2">
          <input
            value={actionInput}
            onChange={(e) => setActionInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && applyFilter()}
            placeholder={t('audit.filterPlaceholder')}
            className="min-w-0 flex-1 rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary sm:max-w-sm"
          />
          <button
            onClick={applyFilter}
            className="rounded-lg bg-primary px-4 text-sm font-semibold text-white hover:bg-primary-hover"
          >
            {t('audit.filter')}
          </button>
          {action && (
            <button
              onClick={clearFilter}
              className="rounded-lg border border-border px-4 text-sm font-semibold hover:bg-canvas"
            >
              {t('audit.clear')}
            </button>
          )}
        </div>

        <div className="overflow-hidden rounded-xl border border-border bg-surface">
          <table className="w-full text-sm">
            <thead className="bg-canvas text-left text-xs uppercase text-muted">
              <tr>
                <th className="px-4 py-3">{t('audit.time')}</th>
                <th className="px-4 py-3">{t('audit.action')}</th>
                <th className="px-4 py-3">{t('audit.actor')}</th>
                <th className="px-4 py-3">{t('audit.ip')}</th>
                <th className="px-4 py-3">{t('audit.details')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {loading ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-muted">
                    {t('topic.loading')}
                  </td>
                </tr>
              ) : logs.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-muted">
                    {t('audit.noEvents')}
                  </td>
                </tr>
              ) : (
                logs.map((l) => (
                  <tr key={l.id}>
                    <td className="whitespace-nowrap px-4 py-3 text-muted">
                      {new Date(l.createdAt).toLocaleString()}
                    </td>
                    <td className="px-4 py-3">
                      <span className="rounded bg-canvas px-2 py-0.5 text-[11px] font-semibold">
                        {l.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-foreground">
                      {l.actorEmail ?? <span className="text-muted">{t('audit.system')}</span>}
                    </td>
                    <td className="px-4 py-3 text-muted">{l.ipAddress ?? '—'}</td>
                    <td className="px-4 py-3 text-muted">{l.details ?? '—'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="mt-4 flex items-center justify-between text-sm">
          <span className="text-muted">
            {t('audit.pageOf', { page, total: totalPages })}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="rounded-lg border border-border px-3 py-1.5 font-semibold disabled:opacity-40"
            >
              {t('audit.prev')}
            </button>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="rounded-lg border border-border px-3 py-1.5 font-semibold disabled:opacity-40"
            >
              {t('audit.next')}
            </button>
          </div>
        </div>
      </div>
  )
}
