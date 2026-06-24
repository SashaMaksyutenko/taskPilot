import api from '../lib/api'

/** Metadata of an uploaded file (mirrors FileAttachmentDto). */
export interface UploadedFile {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
}

/** Upload and download of file attachments. */
export const fileService = {
  /** Uploads a file (multipart) and returns its metadata. */
  upload(file: File): Promise<UploadedFile> {
    const form = new FormData()
    form.append('file', file)
    // Override the default JSON content-type so the browser sets the multipart
    // boundary itself.
    return api
      .post<UploadedFile>('/api/files', form, { headers: { 'Content-Type': undefined } })
      .then((r) => r.data)
  },

  /** Downloads a file's bytes (authenticated) as a Blob. */
  download(id: string): Promise<Blob> {
    return api.get(`/api/files/${id}`, { responseType: 'blob' }).then((r) => r.data as Blob)
  },
}
