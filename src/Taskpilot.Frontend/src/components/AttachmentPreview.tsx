import { useEffect, useState } from 'react'
import { fileService } from '../services/fileService'

/**
 * Renders a chat message's file attachment: an inline thumbnail for images
 * (loaded as an authenticated blob), or a download button for other file types.
 * Clicking either downloads the file.
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
  const isImage = !!contentType?.startsWith('image/')
  const [url, setUrl] = useState<string | null>(null)

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

  if (isImage && url) {
    return (
      <img
        src={url}
        alt={fileName ?? ''}
        onClick={() => onDownload(fileId, fileName ?? 'file')}
        className="mt-1 max-h-48 max-w-full cursor-pointer rounded-lg"
      />
    )
  }

  return (
    <button
      onClick={() => onDownload(fileId, fileName ?? 'file')}
      className={`mt-1 flex items-center gap-1 text-sm underline ${
        mine ? 'text-white/90' : 'text-primary'
      }`}
    >
      📎 {fileName}
    </button>
  )
}
