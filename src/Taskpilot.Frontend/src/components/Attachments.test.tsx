import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import Attachments from './Attachments'
import { forumAttachments, taskAttachments } from '../services/attachmentSources'
import { forumService } from '../services/forumService'
import { taskService } from '../services/taskService'
import type { Attachment } from '../types/attachment'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string, fallback?: string) => fallback ?? k }),
}))

vi.mock('../services/taskService', () => ({
  taskService: { getAttachments: vi.fn(), attachFile: vi.fn(), detachFile: vi.fn() },
}))

vi.mock('../services/forumService', () => ({
  forumService: { getAttachments: vi.fn(), attachFile: vi.fn(), detachFile: vi.fn() },
}))

vi.mock('../services/fileService', () => ({
  fileService: { download: vi.fn() },
}))

// The signed-in user decides which rows get a remove button.
const CURRENT_USER = 'me'
vi.mock('../store/hooks', () => ({
  useAppSelector: (select: (s: unknown) => unknown) =>
    select({ auth: { user: { id: CURRENT_USER } } }),
}))

const attachment = (over: Partial<Attachment> = {}): Attachment => ({
  id: 'a1',
  fileId: 'f1',
  fileName: 'spec.pdf',
  contentType: 'application/pdf',
  sizeBytes: 2048,
  uploadedById: CURRENT_USER,
  uploadedByName: 'Alice',
  createdAt: '2026-07-21T10:00:00Z',
  ...over,
})

const mockedGet = vi.mocked(taskService.getAttachments)
const mockedAttach = vi.mocked(taskService.attachFile)
const mockedDetach = vi.mocked(taskService.detachFile)

/** Picks a file in the hidden input, the way the browser would. */
function pickFile(name = 'notes.txt') {
  const input = screen.getByLabelText('attachments.attach') as HTMLInputElement
  fireEvent.change(input, { target: { files: [new File(['x'], name)] } })
}

describe('Attachments', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads and lists the attachments when the owner opens', async () => {
    mockedGet.mockResolvedValue([attachment()])
    render(<Attachments source={taskAttachments} ownerId="t1" />)

    expect(await screen.findByText(/spec.pdf/)).toBeTruthy()
    expect(mockedGet).toHaveBeenCalledWith('t1')
    // Size and uploader are shown next to the name.
    expect(screen.getByText(/2 KB/)).toBeTruthy()
    expect(screen.getByText(/Alice/)).toBeTruthy()
  })

  it('fetches exactly once under StrictMode AND still renders the result', async () => {
    // StrictMode double-invokes effects in dev; a state guard would not have updated
    // on the second pass, so the ref must key on the owner id.
    mockedGet.mockResolvedValue([attachment()])
    render(
      <StrictMode>
        <Attachments source={taskAttachments} ownerId="t1" />
      </StrictMode>,
    )

    // Asserting the call count alone is not enough — the same bug that once left
    // TaskHistory spinning forever fired exactly once and dropped the response.
    expect(await screen.findByText(/spec.pdf/)).toBeTruthy()
    expect(mockedGet).toHaveBeenCalledTimes(1)
    expect(screen.queryByText('attachments.loading')).toBeNull()
  })

  it('shows an empty state when nothing is attached', async () => {
    mockedGet.mockResolvedValue([])
    render(<Attachments source={taskAttachments} ownerId="t1" />)

    expect(await screen.findByText('attachments.empty')).toBeTruthy()
  })

  it('shows a message when the list cannot be loaded', async () => {
    mockedGet.mockRejectedValue(new Error('boom'))
    render(<Attachments source={taskAttachments} ownerId="t1" />)

    expect(await screen.findByText('attachments.failed')).toBeTruthy()
  })

  it('uploads a picked file and prepends it to the list', async () => {
    mockedGet.mockResolvedValue([])
    mockedAttach.mockResolvedValue(attachment({ id: 'a2', fileName: 'notes.txt' }))
    render(<Attachments source={taskAttachments} ownerId="t1" />)
    await screen.findByText('attachments.empty')

    pickFile()

    expect(await screen.findByText(/notes.txt/)).toBeTruthy()
    expect(mockedAttach).toHaveBeenCalledWith('t1', expect.any(File))
  })

  it('surfaces the server error when the upload is refused', async () => {
    // The storage quota and per-file limit are enforced server-side; the reason
    // must reach the user rather than the file silently vanishing.
    mockedGet.mockResolvedValue([])
    mockedAttach.mockRejectedValue(new Error('boom'))
    render(<Attachments source={taskAttachments} ownerId="t1" />)
    await screen.findByText('attachments.empty')

    pickFile()

    expect(await screen.findByText('Something went wrong.')).toBeTruthy()
  })

  it('removes an attachment and puts it back when the server refuses', async () => {
    mockedGet.mockResolvedValue([attachment()])
    mockedDetach.mockRejectedValue(new Error('nope'))
    render(<Attachments source={taskAttachments} ownerId="t1" />)
    await screen.findByText(/spec.pdf/)

    fireEvent.click(screen.getByLabelText('attachments.remove'))

    // Optimistic removal must not lose the row when the call fails.
    await waitFor(() => expect(screen.getByText(/spec.pdf/)).toBeTruthy())
    expect(mockedDetach).toHaveBeenCalledWith('a1')
  })

  it('offers no remove button on someone else’s file', async () => {
    // Detaching is uploader-only server-side; the UI must not pretend otherwise.
    mockedGet.mockResolvedValue([attachment({ uploadedById: 'someone-else' })])
    render(<Attachments source={taskAttachments} ownerId="t1" />)
    await screen.findByText(/spec.pdf/)

    expect(screen.queryByLabelText('attachments.remove')).toBeNull()
  })

  it('reloads when the component is reused for another owner', async () => {
    mockedGet.mockResolvedValue([attachment()])
    const { rerender } = render(<Attachments source={taskAttachments} ownerId="t1" />)
    await waitFor(() => expect(mockedGet).toHaveBeenCalledWith('t1'))

    rerender(<Attachments source={taskAttachments} ownerId="t2" />)
    await waitFor(() => expect(mockedGet).toHaveBeenCalledWith('t2'))
  })

  describe('forum source', () => {
    it('reads through the forum service, not the task one', async () => {
      // Mixing the two up would silently hit the wrong endpoint, so assert the wiring.
      vi.mocked(forumService.getAttachments).mockResolvedValue([attachment({ fileName: 'error.log' })])

      render(<Attachments source={forumAttachments} ownerId="topic-1" />)

      expect(await screen.findByText(/error.log/)).toBeTruthy()
      expect(forumService.getAttachments).toHaveBeenCalledWith('topic-1')
      expect(mockedGet).not.toHaveBeenCalled()
    })

    it('hides the attach button when the user may not add files', async () => {
      // A non-author, or a locked topic: offering a button that always fails is worse
      // than no button.
      vi.mocked(forumService.getAttachments).mockResolvedValue([attachment()])

      render(<Attachments source={forumAttachments} ownerId="topic-1" canAttach={false} />)
      await screen.findByText(/spec.pdf/)

      expect(screen.queryByText(/attachments.attach/)).toBeNull()
    })

    it('renders nothing at all when there are no files and none may be added', async () => {
      // Most forum topics have no attachments; a permanent empty box under every
      // post would be noise.
      vi.mocked(forumService.getAttachments).mockResolvedValue([])

      const { container } = render(
        <Attachments source={forumAttachments} ownerId="topic-1" canAttach={false} />,
      )

      await waitFor(() => expect(container.textContent).toBe(''))
    })
  })
})
