import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Navbar from '../components/Navbar'
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

  const [tasks, setTasks] = useState<MarketTaskListItem[]>([])
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [budget, setBudget] = useState('')
  const [skills, setSkills] = useState('')

  const load = () => {
    marketplaceService.getTasks().then(setTasks).catch(() => {})
  }

  useEffect(load, [])

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
    load()
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-3xl px-6 py-8">
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
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder={t('market.describe')}
            rows={3}
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />
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
          <p className="text-slate-400">{t('market.empty')}</p>
        ) : (
          <ul className="space-y-2">
            {tasks.map((task) => (
              <li key={task.id}>
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
                  <span className={`flex-none rounded-full px-2 py-0.5 text-[11px] font-semibold ${statusColor[task.status] ?? 'bg-slate-200 text-slate-700'}`}>
                    {t(`market.status.${task.status}`, task.status)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  )
}
