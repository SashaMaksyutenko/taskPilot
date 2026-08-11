import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import WhiteboardCanvas from '../components/WhiteboardCanvas'
import { colorFromString } from '../lib/userColor'
import { projectService } from '../services/projectService'
import { useAppSelector } from '../store/hooks'

/** Full-page collaborative whiteboard for a project. */
export default function WhiteboardPage() {
  const { t } = useTranslation()
  const { projectId = '' } = useParams()
  const user = useAppSelector((s) => s.auth.user)

  const [projectName, setProjectName] = useState('')
  const [canEdit, setCanEdit] = useState(false)
  const [isOwner, setIsOwner] = useState(false)

  useEffect(() => {
    if (!projectId) return
    projectService.getProject(projectId).then((p) => setProjectName(p.name)).catch(() => {})
    projectService
      .getMembers(projectId)
      .then((ms) => {
        const me = ms.find((m) => m.userId === user?.id)
        setCanEdit(!!me && (me.isOwner || me.role === 'Editor'))
        setIsOwner(!!me?.isOwner)
      })
      .catch(() => {})
  }, [projectId, user?.id])

  return (
    <div className="flex h-[calc(100vh-8rem)] flex-col">
      <div className="mb-3 flex items-center gap-3">
        <Link to={`/projects/${projectId}`} className="text-muted transition hover:text-foreground" aria-label={t('whiteboard.back')}>
          <ArrowLeft className="h-5 w-5" />
        </Link>
        <h1 className="text-lg font-bold">
          {t('whiteboard.title')}
          {projectName && <span className="ml-2 font-normal text-muted">· {projectName}</span>}
        </h1>
      </div>

      <div className="flex-1 overflow-hidden rounded-[var(--radius-card)] border border-border bg-surface">
        <WhiteboardCanvas
          projectId={projectId}
          user={{ id: user?.id ?? 'me', name: user?.name ?? 'You', color: colorFromString(user?.id ?? 'me') }}
          canEdit={canEdit}
          isOwner={isOwner}
        />
      </div>
    </div>
  )
}
