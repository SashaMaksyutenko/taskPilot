import { forumService } from './forumService'
import { taskService } from './taskService'
import type { Attachment } from '../types/attachment'

/**
 * Where a list of attachments lives. Tasks and forum topics expose the same three
 * calls over the same DTO, so one component (<Attachments />) serves both.
 */
export interface AttachmentSource {
  list(ownerId: string): Promise<Attachment[]>
  upload(ownerId: string, file: File): Promise<Attachment>
  remove(attachmentId: string): Promise<void>
}

/**
 * Module-level constants on purpose: a source built inline in a parent's render would
 * get a new identity every time and defeat the load-once guard in <Attachments />.
 * They live here rather than beside the component because a file that exports both a
 * component and constants breaks React Fast Refresh.
 */
export const taskAttachments: AttachmentSource = {
  list: (taskId) => taskService.getAttachments(taskId),
  upload: (taskId, file) => taskService.attachFile(taskId, file),
  remove: (id) => taskService.detachFile(id),
}

export const forumAttachments: AttachmentSource = {
  list: (topicId) => forumService.getAttachments(topicId),
  upload: (topicId, file) => forumService.attachFile(topicId, file),
  remove: (id) => forumService.detachFile(id),
}
