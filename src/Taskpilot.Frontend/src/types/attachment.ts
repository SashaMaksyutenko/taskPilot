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
  uploadedById: string
  /** Null when the uploader's account no longer exists. */
  uploadedByName: string | null
  createdAt: string
}
