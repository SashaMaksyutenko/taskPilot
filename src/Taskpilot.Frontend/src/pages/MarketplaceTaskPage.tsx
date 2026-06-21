import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import Navbar from '../components/Navbar'
import { marketplaceService } from '../services/marketplaceService'
import { useAppSelector } from '../store/hooks'
import type { MarketTaskDetail } from '../types/marketplace'

const appStatusColor: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-700',
  Accepted: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-700',
}

/**
 * One marketplace task. The poster sees and decides on applications; everyone
 * else can apply (while the task is open and they haven't applied yet).
 */
export default function MarketplaceTaskPage() {
  const { taskId = '' } = useParams()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [task, setTask] = useState<MarketTaskDetail | null>(null)
  const [coverLetter, setCoverLetter] = useState('')
  const [rate, setRate] = useState('')

  const load = () => {
    if (taskId) marketplaceService.getTask(taskId).then(setTask).catch(() => {})
  }

  useEffect(load, [taskId])

  if (!task) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-900">
        <Navbar />
        <p className="p-8 text-slate-400">Loading…</p>
      </div>
    )
  }

  const isPoster = currentUserId === task.posterId
  const myApplication = task.applications.find((a) => a.applicantId === currentUserId)

  const apply = async () => {
    if (!coverLetter.trim() || !rate) return
    await marketplaceService
      .apply({ taskId: task.id, coverLetter: coverLetter.trim(), proposedRate: Number(rate) })
      .catch(() => {})
    setCoverLetter('')
    setRate('')
    load()
  }

  const decide = async (applicationId: string, accept: boolean) => {
    if (accept) await marketplaceService.accept(applicationId).catch(() => {})
    else await marketplaceService.reject(applicationId).catch(() => {})
    load()
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-3xl px-6 py-8">
        <Link to="/marketplace" className="text-sm text-slate-500 hover:underline dark:text-slate-400">
          ← Marketplace
        </Link>

        {/* Task header */}
        <div className="mt-3 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <div className="flex items-start gap-3">
            <h1 className="flex-1 text-xl font-bold">{task.title}</h1>
            <span className="text-lg font-bold">${task.budget}</span>
          </div>
          <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">
            by {task.posterName} · {task.status}
            {task.assigneeName ? ` · assigned to ${task.assigneeName}` : ''}
          </div>
          {task.requiredSkills && (
            <div className="mt-2 text-sm text-slate-600 dark:text-slate-300">Skills: {task.requiredSkills}</div>
          )}
          <p className="mt-4 whitespace-pre-wrap">{task.description}</p>
        </div>

        {/* Poster: applications */}
        {isPoster ? (
          <>
            <h2 className="mb-3 mt-6 font-bold">{task.applications.length} application(s)</h2>
            <div className="space-y-3">
              {task.applications.map((a) => (
                <div key={a.id} className="rounded-xl border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-800">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold">{a.applicantName}</span>
                    <span className="text-sm text-slate-500 dark:text-slate-400">· ${a.proposedRate}</span>
                    <span className={`ml-auto rounded-full px-2 py-0.5 text-[11px] font-semibold ${appStatusColor[a.status]}`}>
                      {a.status}
                    </span>
                  </div>
                  <p className="mt-2 text-sm">{a.coverLetter}</p>
                  {a.status === 'Pending' && task.status === 'Open' && (
                    <div className="mt-3 flex gap-2">
                      <button onClick={() => decide(a.id, true)} className="rounded-lg bg-green-600 px-4 py-1.5 text-sm font-semibold text-white hover:bg-green-700">
                        Accept
                      </button>
                      <button onClick={() => decide(a.id, false)} className="rounded-lg border border-slate-300 px-4 py-1.5 text-sm font-semibold hover:bg-slate-50 dark:border-slate-600 dark:hover:bg-slate-700">
                        Reject
                      </button>
                    </div>
                  )}
                </div>
              ))}
              {task.applications.length === 0 && <p className="text-slate-400">No applications yet.</p>}
            </div>
          </>
        ) : myApplication ? (
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
            You applied —{' '}
            <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${appStatusColor[myApplication.status]}`}>
              {myApplication.status}
            </span>
          </div>
        ) : task.status === 'Open' ? (
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
            <h2 className="mb-3 font-bold">Apply for this task</h2>
            <textarea
              value={coverLetter}
              onChange={(e) => setCoverLetter(e.target.value)}
              placeholder="Cover letter"
              rows={3}
              className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            <div className="flex gap-2">
              <input
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                type="number"
                placeholder="Your rate ($)"
                className="w-40 rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
              />
              <button onClick={apply} className="rounded-lg bg-[#1E2A44] px-5 font-semibold text-white hover:bg-[#27345a]">
                Apply
              </button>
            </div>
          </div>
        ) : (
          <p className="mt-6 text-sm text-slate-400">This task is no longer open for applications.</p>
        )}
      </main>
    </div>
  )
}
