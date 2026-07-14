import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fileService } from '../services/fileService'

/**
 * Renders a chat message's file attachment: an inline thumbnail for images
 * (loaded as an authenticated blob), or a download button for other file types.
 * Clicking either downloads the file. The uploader also gets a "share link" action
 * that copies a public URL anyone can download from.
 */
export default function AttachmentPreview({
  fileId,
  fileName,
  contentType,
  mine,
  onDownload,
}: {
  fileId: string
  fileName: string | null
  contentType: string | null
  mine: boolean
  onDownload: (fileId: string, fileName: string) => void
}) {
  const { t } = useTranslation()
  const isImage = !!contentType?.startsWith('image/')
  const [url, setUrl] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  // For images, fetch the bytes (auth) once and expose them via an object URL.
  useEffect(() => {
    if (!isImage) return
    let active = true
    let objectUrl: string | null = null
    fileService
      .download(fileId)
      .then((blob) => {
        if (!active) return
        objectUrl = URL.createObjectURL(blob)
        setUrl(objectUrl)
      })
      .catch(() => {})
    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [fileId, isImage])

  // Creates the public link (idempotent server-side) and copies it to the clipboard.
  const copyShareLink = async () => {
    const link = await fileService.share(fileId).catch(() => null)
    if (!link) return
    await navigator.clipboard.writeText(link.url).catch(() => {})
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  const preview = isImage && url ? (
    <img
      src={url}
      alt={fileName ?? ''}
      onClick={() => onDownload(fileId, fileName ?? 'file')}
      className="max-h-48 max-w-full cursor-pointer rounded-lg"
    />
  ) : (
    <button
      onClick={() => onDownload(fileId, fileName ?? 'file')}
      className={`flex items-center gap-1 text-sm underline ${mine ? 'text-white/90' : 'text-primary'}`}
    >
      📎 {fileName}
    </button>
  )

  return (
    <div className="mt-1">
      {preview}
      {/* Only the uploader may create a public link. */}
      {mine && (
        <button
          onClick={copyShareLink}
          title={t('files.shareLink')}
          className="mt-1 text-xs text-white/80 underline hover:text-white"
        >
          🔗 {copied ? t('files.linkCopied') : t('files.shareLink')}
        </button>
      )}
    </div>
  )
}
