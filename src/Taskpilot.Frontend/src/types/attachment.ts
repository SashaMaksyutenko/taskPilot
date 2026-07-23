/**
 * A file attached to something (mirrors the backend TaskAttachmentDto and
 * ForumAttachmentDto — deliberately the same shape, so one component renders both).
 */
export interface Attachment {
  /** The link's id — what detaching takes, not the file's id. */
  id: string
  /** The file itself; download it through /api/files/{fileId}. */
  fileId: string
  fileName: string
  contentType: string
  sizeBytes: number
  /** Current version number (1 for a file that has never been replaced). */
  version: number
  uploadedById: string
  /** Null when the uploader's account no longer exists. */
  uploadedByName: string | null
  createdAt: string
}

/** One entry in a file's version history (mirrors the backend FileVersionDto). */
export interface FileVersion {
  /** This version's stored file; download it through /api/files/{fileId}. */
  fileId: string
  version: number
  fileName: string
  sizeBytes: number
  /** Null when the uploader's account no longer exists. */
  uploadedByName: string | null
  createdAt: string
  /** True for the version the attachment currently points at. */
  isCurrent: boolean
}
