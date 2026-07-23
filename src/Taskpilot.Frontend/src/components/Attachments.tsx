import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { type AttachmentSource } from '../services/attachmentSources'
import { fileService } from '../services/fileService'
import { useAppSelector } from '../store/hooks'
import type { Attachment, FileVersion } from '../types/attachment'

/** Formats a byte count for display, e.g. "1.4 MB". */
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const kb = bytes / 1024
  if (kb < 1024) return `${Math.round(kb)} KB`
  return `${(kb / 1024).toFixed(1)} MB`
}

/** Downloads a stored file (authenticated) and triggers a save dialog. */
async function downloadFile(fileId: string, fileName: string) {
  const blob = await fileService.download(fileId).catch(() => null)
  if (!blob) return
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

/**
 * Files attached to a task or a forum topic: the list, an upload button and a remove
 * action. Task attachments additionally support version history (the source carries a
 * `versions` capability); forum attachments do not, so that UI simply never appears.
 *
 * The list loads as soon as the task or topic is opened — attachments are part of the
 * content, so hiding them behind an expander (the way TaskHistory does) would mean
 * nobody knows they are there. The bytes are served by the shared /api/files/{id}
 * endpoint, which any signed-in user may read.
 */
export default function Attachments({
  source,
  ownerId,
  canAttach = true,
}: {
  source: AttachmentSource
  /** Id of the task or topic the files hang off. */
  ownerId: string
  /**
   * Whether the current user may add files. The server decides for real; this only
   * avoids offering a button that is certain to fail (e.g. a forum topic you did not
   * write, or one that is locked).
   */
  canAttach?: boolean
}) {
  const { t } = useTranslation()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [items, setItems] = useState<Attachment[] | null>(null)
  const [failed, setFailed] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  // The attachment currently having a new version uploaded (drives the version file
  // picker and the per-row "uploading" state), and the one whose history is expanded.
  const [versioningId, setVersioningId] = useState<string | null>(null)
  const [openVersionsFor, setOpenVersionsFor] = useState<string | null>(null)
  const [versions, setVersions] = useState<FileVersion[] | null>(null)
  const [versionsFailed, setVersionsFailed] = useState(false)
  const versionInputRef = useRef<HTMLInputElement>(null)

  // Which owner has been requested. A ref, not state: StrictMode double-invokes
  // effects in dev and the second pass would still read the stale state value.
  const loadedFor = useRef<string | null>(null)

  useEffect(() => {
    if (loadedFor.current === ownerId) return
    loadedFor.current = ownerId

    setItems(null)
    setFailed(false)
    setOpenVersionsFor(null)
    source
      .list(ownerId)
      .then((list) => {
        // Comparing the ref (rather than a cleanup flag) discards a stale response
        // without throwing away the only in-flight one under StrictMode.
        if (loadedFor.current === ownerId) setItems(list)
      })
      .catch(() => {
        if (loadedFor.current === ownerId) setFailed(true)
      })
  }, [ownerId, source])

  const upload = async (file: File) => {
    setUploading(true)
    setError('')
    try {
      const added = await source.upload(ownerId, file)
      setItems((prev) => [added, ...(prev ?? [])])
    } catch (e) {
      // Size limits and the organization storage quota are refused here.
      setError(apiErrorMessage(e))
    } finally {
      setUploading(false)
      // Clear the input so re-picking the same file fires change again.
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  const remove = async (attachment: Attachment) => {
    setError('')
    // Optimistic: the row disappears at once and comes back if the server refuses.
    setItems((prev) => prev?.filter((a) => a.id !== attachment.id) ?? null)
    if (openVersionsFor === attachment.id) setOpenVersionsFor(null)
    try {
      await source.remove(attachment.id)
    } catch (e) {
      setError(apiErrorMessage(e))
      setItems((prev) => (prev ? [attachment, ...prev] : prev))
    }
  }

  // Opens the version file picker for a specific attachment.
  const pickVersionFor = (attachmentId: string) => {
    setVersioningId(attachmentId)
    versionInputRef.current?.click()
  }

  const uploadVersion = async (file: File) => {
    if (!source.versions || !versioningId) return
    const attachmentId = versioningId
    setError('')
    try {
      const updated = await source.versions.upload(attachmentId, file)
      // The attachment keeps its id but now points at the new file/version.
      setItems((prev) => prev?.map((a) => (a.id === attachmentId ? updated : a)) ?? null)
      // Refresh the history if it happens to be open on this row.
      if (openVersionsFor === attachmentId) await loadVersions(attachmentId)
    } catch (e) {
      setError(apiErrorMessage(e))
    } finally {
      setVersioningId(null)
      if (versionInputRef.current) versionInputRef.current.value = ''
    }
  }

  const loadVersions = async (attachmentId: string) => {
    if (!source.versions) return
    setVersions(null)
    setVersionsFailed(false)
    try {
      setVersions(await source.versions.list(attachmentId))
    } catch {
      setVersionsFailed(true)
    }
  }

  const toggleVersions = (attachmentId: string) => {
    if (openVersionsFor === attachmentId) {
      setOpenVersionsFor(null)
      return
    }
    setOpenVersionsFor(attachmentId)
    loadVersions(attachmentId)
  }

  // Nothing to show and nothing to add: stay out of the way entirely.
  if (!canAttach && (failed || items?.length === 0)) return null

  const supportsVersions = !!source.versions

  return (
    <div className="mb-4">
      <div className="mb-1 flex items-center justify-between">
        <label className="text-sm font-medium text-foreground">{t('attachments.title')}</label>
        {canAttach && (
          <>
            <button
              type="button"
              onClick={() => inputRef.current?.click()}
              disabled={uploading}
              className="text-sm font-medium text-primary hover:underline disabled:opacity-50"
            >
              {uploading ? t('attachments.uploading') : `+ ${t('attachments.attach')}`}
            </button>
            <input
              ref={inputRef}
              type="file"
              className="hidden"
              aria-label={t('attachments.attach')}
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) upload(file)
              }}
            />
          </>
        )}
      </div>

      {error && <p className="mb-1 text-xs text-red-600">{error}</p>}

      {/* One shared picker for new versions; the target row is held in versioningId. */}
      {supportsVersions && (
        <input
          ref={versionInputRef}
          type="file"
          className="hidden"
          data-testid="version-input"
          onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) uploadVersion(file)
          }}
        />
      )}

      <div className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm">
        {failed && <p className="text-muted">{t('attachments.failed')}</p>}
        {!failed && items === null && <p className="text-muted">{t('attachments.loading')}</p>}
        {!failed && items?.length === 0 && <p className="text-muted">{t('attachments.empty')}</p>}

        {!!items?.length && (
          <ul className="space-y-1">
            {items.map((a) => {
              const mine = a.uploadedById === currentUserId
              return (
                <li key={a.id} className="py-0.5">
                  <div className="flex items-baseline gap-2">
                    <button
                      type="button"
                      onClick={() => downloadFile(a.fileId, a.fileName)}
                      className="min-w-0 flex-1 truncate text-left text-primary hover:underline"
                    >
                      📎 {a.fileName}
                    </button>
                    {/* Version marker. Past versions exist only above v1, so only then is
                        it a toggle; at v1 it is just a label. */}
                    {supportsVersions &&
                      (a.version > 1 ? (
                        <button
                          type="button"
                          onClick={() => toggleVersions(a.id)}
                          aria-expanded={openVersionsFor === a.id}
                          className="flex-none text-xs font-medium text-primary hover:underline"
                        >
                          v{a.version} {openVersionsFor === a.id ? '▾' : '▸'}
                        </button>
                      ) : (
                        <span className="flex-none text-xs text-muted">v{a.version}</span>
                      ))}
                    <span className="flex-none text-xs text-muted">
                      {formatSize(a.sizeBytes)} · {a.uploadedByName ?? t('attachments.unknownUser')} ·{' '}
                      {new Date(a.createdAt).toLocaleDateString()}
                    </span>
                    {/* Replacing and removing are both uploader-only, matching the server. */}
                    {mine && supportsVersions && (
                      <button
                        type="button"
                        onClick={() => pickVersionFor(a.id)}
                        disabled={versioningId === a.id}
                        title={t('attachments.newVersion')}
                        aria-label={t('attachments.newVersion')}
                        className="flex-none text-muted hover:text-primary disabled:opacity-50"
                      >
                        ⭯
                      </button>
                    )}
                    {mine && (
                      <button
                        type="button"
                        onClick={() => remove(a)}
                        title={t('attachments.remove')}
                        aria-label={t('attachments.remove')}
                        className="flex-none text-muted hover:text-red-600"
                      >
                        ×
                      </button>
                    )}
                  </div>

                  {/* Expanded version history for this attachment. */}
                  {openVersionsFor === a.id && (
                    <div className="ml-6 mt-1 border-l border-border pl-3">
                      {versionsFailed && <p className="text-xs text-muted">{t('attachments.failed')}</p>}
                      {!versionsFailed && versions === null && (
                        <p className="text-xs text-muted">{t('attachments.loading')}</p>
                      )}
                      {versions?.map((v) => (
                        <div key={v.fileId} className="flex items-baseline gap-2 py-0.5 text-xs">
                          <button
                            type="button"
                            onClick={() => downloadFile(v.fileId, v.fileName)}
                            className="flex-none text-primary hover:underline"
                          >
                            v{v.version}
                          </button>
                          {v.isCurrent && <span className="flex-none text-muted">{t('attachments.current')}</span>}
                          <span className="min-w-0 flex-1 truncate text-muted">
                            {formatSize(v.sizeBytes)} · {v.uploadedByName ?? t('attachments.unknownUser')} ·{' '}
                            {new Date(v.createdAt).toLocaleDateString()}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </div>
  )
}
