import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Avatar from '../components/Avatar'
import FadeIn from '../components/FadeIn'
import EmptyState from '../components/EmptyState'
import MarkdownEditor from '../components/MarkdownEditor'
import Navbar from '../components/Navbar'
import ActionsContextMenu from '../components/ActionsContextMenu'
import { marketplaceService } from '../services/marketplaceService'
import { useAppSelector } from '../store/hooks'
import type { MarketTaskListItem } from '../types/marketplace'

const statusColor: Record<string, string> = {
  Open: 'bg-green-100 text-green-700',
  InProgress: 'bg-blue-100 text-blue-700',
  Completed: 'bg-slate-200 text-slate-700',
  Cancelled: 'bg-red-100 text-red-700',
}

/**
 * Marketplace home: browse public tasks and post a new one.
 */
export default function MarketplacePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  // RBAC: only Managers and Admins may post tasks (backend enforces this too).
  const role = useAppSelector((s) => s.auth.user?.role)
  const canPost = role === 'Manager' || role === 'Admin'

  const PAGE_SIZE = 10
  const [tasks, setTasks] = useState<MarketTaskListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [budget, setBudget] = useState('')
  const [skills, setSkills] = useState('')

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const load = (p: number) => {
    marketplaceService
      .getTasks({ page: p, pageSize: PAGE_SIZE })
      .then((r) => {
        setTasks(r.items)
        setTotal(r.total)
      })
      .catch(() => {})
  }

  useEffect(() => load(page), [page])

  const create = async () => {
    if (!title.trim() || !description.trim() || !budget) return
    await marketplaceService
      .createTask({
        title: title.trim(),
        description: description.trim(),
        budget: Number(budget),
        requiredSkills: skills.trim() || undefined,
      })
      .catch(() => {})
    setTitle('')
    setDescription('')
    setBudget('')
    setSkills('')
    // Show the newest task on page 1 (reload if already there).
    if (page === 1) load(1)
    else setPage(1)
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-3xl px-6 py-8">
        <FadeIn>
        <h1 className="mb-6 text-2xl font-bold">{t('market.title')}</h1>

        {/* Post a task — Managers/Admins only */}
        {canPost && (
        <div className="mb-8 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-3 font-bold">{t('market.postSection')}</h2>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t('market.taskTitle')}
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />
          <div className="mb-2">
            <MarkdownEditor
              value={description}
              onChange={setDescription}
              placeholder={t('market.describe')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
          </div>
          <div className="mb-3 flex gap-2">
            <input
              value={budget}
              onChange={(e) => setBudget(e.target.value)}
              type="number"
              placeholder={t('market.budget')}
              className="w-32 rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            <input
              value={skills}
              onChange={(e) => setSkills(e.target.value)}
              placeholder={t('market.skills')}
              className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
          </div>
          <button
            onClick={create}
            className="rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a]"
          >
            {t('market.postBtn')}
          </button>
        </div>
        )}

        {/* Task list */}
        {tasks.length === 0 ? (
          <EmptyState message={t('market.empty')} />
        ) : (
          <ul className="space-y-2">
            {tasks.map((task) => (
              <li key={task.id}>
                <ActionsContextMenu
                  actions={[
                    { label: t('menu.open'), onSelect: () => navigate(`/marketplace/${task.id}`) },
                    {
                      label: t('menu.copyLink'),
                      onSelect: () =>
                        navigator.clipboard?.writeText(`${window.location.origin}/marketplace/${task.id}`).catch(() => {}),
                    },
                  ]}
                >
                <Link
                  to={`/marketplace/${task.id}`}
                  className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 transition hover:shadow-sm dark:border-slate-700 dark:bg-slate-800"
                >
                  <Avatar name={task.posterName} src={task.posterAvatarUrl} size={38} />
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-semibold">{task.title}</div>
                    <div className="text-xs text-slate-500 dark:text-slate-400">
                      {t('forum.by')}{' '}
                      <span
                        onClick={(e) => {
                          e.preventDefault()
                          e.stopPropagation()
                          navigate(`/users/${task.posterId}`)
                        }}
                        className="cursor-pointer font-medium hover:underline"
                      >
                        {task.posterName}
                      </span>{' '}
                      · {t('market.applications', { count: task.applicationCount })}
                      {task.requiredSkills ? ` · ${task.requiredSkills}` : ''}
                    </div>
                  </div>
                  <span className="flex-none font-bold">${task.budget}</span>
                  {task.paymentStatus === 'Paid' && (
                    <span className="flex-none rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-semibold text-green-700 dark:bg-green-900/40 dark:text-green-300">
                      {t('market.paid')}
                    </span>
                  )}
                  <span className={`flex-none rounded-full px-2 py-0.5 text-[11px] font-semibold ${statusColor[task.status] ?? 'bg-slate-200 text-slate-700'}`}>
                    {t(`market.status.${task.status}`, task.status)}
                  </span>
                </Link>
                </ActionsContextMenu>
              </li>
            ))}
          </ul>
        )}

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
        </FadeIn>
      </main>
    </div>
  )
}
