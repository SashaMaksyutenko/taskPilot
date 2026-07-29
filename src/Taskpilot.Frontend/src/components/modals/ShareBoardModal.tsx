import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { projectService } from '../../services/projectService'
import type { ShareLink } from '../../types/project'

/**
 * Manage a board's public read-only share link. The owner can enable a link (anyone with it
 * views the board without logging in), copy it, or revoke it.
 */
export default function ShareBoardModal({ projectId, onClose }: { projectId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [share, setShare] = useState<ShareLink | null>(null)
  const [busy, setBusy] = useState(false)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    projectService.getShareLink(projectId).then(setShare).catch(() => {})
  }, [projectId])

  const url = share?.token ? `${window.location.origin}/board/${share.token}` : ''

  const enable = async () => {
    setBusy(true)
    setShare(await projectService.createShareLink(projectId).catch(() => share))
    setBusy(false)
  }

  const disable = async () => {
    setBusy(true)
    await projectService.revokeShareLink(projectId).catch(() => {})
    setShare({ token: null, enabled: false })
    setBusy(false)
  }

  const copy = async () => {
    await navigator.clipboard.writeText(url).catch(() => {})
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="w-full max-w-md rounded-xl bg-surface p-6 shadow-elevated" onClick={(e) => e.stopPropagation()}>
        <div className="mb-1 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('share.title')}</h2>
          <button onClick={onClose} className="text-muted hover:text-foreground">✕</button>
        </div>
        <p className="mb-4 text-sm text-muted">{t('share.subtitle')}</p>

        {share?.enabled ? (
          <>
            <div className="mb-3 flex gap-2">
              <input
                readOnly
                value={url}
                onFocus={(e) => e.target.select()}
                className="min-w-0 flex-1 rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none"
              />
              <button
                onClick={copy}
                className="flex-none rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover"
              >
                {copied ? t('share.copied') : t('share.copy')}
              </button>
            </div>
            <p className="mb-4 text-xs text-muted">{t('share.readOnlyNote')}</p>
            <button
              onClick={disable}
              disabled={busy}
              className="text-sm font-semibold text-red-600 hover:underline disabled:opacity-50"
            >
              {t('share.disable')}
            </button>
          </>
        ) : (
          <button
            onClick={enable}
            disabled={busy}
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-50"
          >
            {t('share.enable')}
          </button>
        )}
      </div>
    </div>
  )
}
