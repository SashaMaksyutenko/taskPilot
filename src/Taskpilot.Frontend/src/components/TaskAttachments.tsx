import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { fileService } from '../services/fileService'
import { taskService, type TaskAttachment } from '../services/taskService'
import { useAppSelector } from '../store/hooks'

/** Formats a byte count for display, e.g. "1.4 MB". */
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const kb = bytes / 1024
  if (kb < 1024) return `${Math.round(kb)} KB`
  return `${(kb / 1024).toFixed(1)} MB`
}

/**
 * Files attached to a task: the list, an upload button and a remove action.
 *
 * Unlike TaskHistory this loads as soon as the task is opened — attachments are part
 * of the task's content, so hiding them behind an expander would mean nobody knows
 * they are there. The bytes are served by the shared /api/files/{id} endpoint.
 */
export default function TaskAttachments({ taskId }: { taskId: string }) {
  const { t } = useTranslation()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [items, setItems] = useState<TaskAttachment[] | null>(null)
  const [failed, setFailed] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  // Which task has been requested. A ref, not state: StrictMode double-invokes
  // effects in dev and the second pass would still read the stale state value.
  const loadedFor = useRef<string | null>(null)

  useEffect(() => {
    if (loadedFor.current === taskId) return
    loadedFor.current = taskId

    setItems(null)
    setFailed(false)
    taskService
      .getAttachments(taskId)
      .then((list) => {
        // Comparing the ref (rather than a cleanup flag) discards a stale response
        // without throwing away the only in-flight one under StrictMode.
        if (loadedFor.current === taskId) setItems(list)
      })
      .catch(() => {
        if (loadedFor.current === taskId) setFailed(true)
      })
  }, [taskId])

  const upload = async (file: File) => {
    setUploading(true)
    setError('')
    try {
      const added = await taskService.attachFile(taskId, file)
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

  const remove = async (attachment: TaskAttachment) => {
    setError('')
    // Optimistic: the row disappears at once and comes back if the server refuses.
    setItems((prev) => prev?.filter((a) => a.id !== attachment.id) ?? null)
    try {
      await taskService.detachFile(attachment.id)
    } catch (e) {
      setError(apiErrorMessage(e))
      setItems((prev) => (prev ? [attachment, ...prev] : prev))
    }
  }

  const download = async (attachment: TaskAttachment) => {
    const blob = await fileService.download(attachment.fileId).catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = attachment.fileName
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="mb-4">
      <div className="mb-1 flex items-center justify-between">
        <label className="text-sm font-medium text-foreground">{t('attachments.title')}</label>
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
