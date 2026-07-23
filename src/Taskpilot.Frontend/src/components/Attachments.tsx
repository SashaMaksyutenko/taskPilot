import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { type AttachmentSource } from '../services/attachmentSources'
import { fileService } from '../services/fileService'
import { useAppSelector } from '../store/hooks'
import type { Attachment } from '../types/attachment'

/** Formats a byte count for display, e.g. "1.4 MB". */
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const kb = bytes / 1024
  if (kb < 1024) return `${Math.round(kb)} KB`
  return `${(kb / 1024).toFixed(1)} MB`
}

/**
 * Files attached to a task or a forum topic: the list, an upload button and a remove
 * action.
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

  // Which owner has been requested. A ref, not state: StrictMode double-invokes
  // effects in dev and the second pass would still read the stale state value.
  const loadedFor = useRef<string | null>(null)

  useEffect(() => {
    if (loadedFor.current === ownerId) return
    loadedFor.current = ownerId

    setItems(null)
    setFailed(false)
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
    try {
      await source.remove(attachment.id)
    } catch (e) {
      setError(apiErrorMessage(e))
      setItems((prev) => (prev ? [attachment, ...prev] : prev))
    }
  }

  const download = async (attachment: Attachment) => {
    const blob = await fileService.download(attachment.fileId).catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = attachment.fileName
    a.click()
    URL.revokeObjectURL(url)
  }

  // Nothing to show and nothing to add: stay out of the way entirely.
  if (!canAttach && (failed || items?.length === 0)) return null

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

      <div className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm">
        {failed && <p className="text-muted">{t('attachments.failed')}</p>}
        {!failed && items === null && <p className="text-muted">{t('attachments.loading')}</p>}
        {!failed && items?.length === 0 && <p className="text-muted">{t('attachments.empty')}</p>}

        {!!items?.length && (
          <ul className="space-y-1">
            {items.map((a) => (
              <li key={a.id} className="flex items-baseline gap-2 py-0.5">
                <button
                  type="button"
                  onClick={() => download(a)}
                  className="min-w-0 flex-1 truncate text-left text-primary hover:underline"
                >
                  📎 {a.fileName}
                </button>
                <span className="flex-none text-xs text-muted">
                  {formatSize(a.sizeBytes)} · {a.uploadedByName ?? t('attachments.unknownUser')} ·{' '}
                  {new Date(a.createdAt).toLocaleDateString()}
                </span>
                {/* Only the uploader may remove a file — the same rule the rest of
                    the file features follow. */}
                {a.uploadedById === currentUserId && (
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
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
