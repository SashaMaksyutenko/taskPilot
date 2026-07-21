import { StrictMode } from 'react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import TaskAttachments from './TaskAttachments'
import { taskService, type TaskAttachment } from '../services/taskService'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string, fallback?: string) => fallback ?? k }),
}))

vi.mock('../services/taskService', () => ({
  taskService: { getAttachments: vi.fn(), attachFile: vi.fn(), detachFile: vi.fn() },
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

const attachment = (over: Partial<TaskAttachment> = {}): TaskAttachment => ({
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

describe('TaskAttachments', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads and lists the attachments when the task opens', async () => {
    mockedGet.mockResolvedValue([attachment()])
    render(<TaskAttachments taskId="t1" />)

    expect(await screen.findByText(/spec.pdf/)).toBeTruthy()
    expect(mockedGet).toHaveBeenCalledWith('t1')
    // Size and uploader are shown next to the name.
    expect(screen.getByText(/2 KB/)).toBeTruthy()
    expect(screen.getByText(/Alice/)).toBeTruthy()
  })

  it('fetches exactly once under StrictMode AND still renders the result', async () => {
    // StrictMode double-invokes effects in dev; a state guard would not have updated
    // on the second pass, so the ref must key on the task id.
    mockedGet.mockResolvedValue([attachment()])
    render(
      <StrictMode>
        <TaskAttachments taskId="t1" />
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
    render(<TaskAttachments taskId="t1" />)

    expect(await screen.findByText('attachments.empty')).toBeTruthy()
  })

  it('shows a message when the list cannot be loaded', async () => {
    mockedGet.mockRejectedValue(new Error('boom'))
    render(<TaskAttachments taskId="t1" />)

    expect(await screen.findByText('attachments.failed')).toBeTruthy()
  })

  it('uploads a picked file and prepends it to the list', async () => {
    mockedGet.mockResolvedValue([])
    mockedAttach.mockResolvedValue(attachment({ id: 'a2', fileName: 'notes.txt' }))
    render(<TaskAttachments taskId="t1" />)
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
    render(<TaskAttachments taskId="t1" />)
    await screen.findByText('attachments.empty')

    pickFile()

    expect(await screen.findByText('Something went wrong.')).toBeTruthy()
  })

  it('removes an attachment and puts it back when the server refuses', async () => {
    mockedGet.mockResolvedValue([attachment()])
    mockedDetach.mockRejectedValue(new Error('nope'))
    render(<TaskAttachments taskId="t1" />)
    await screen.findByText(/spec.pdf/)

    fireEvent.click(screen.getByLabelText('attachments.remove'))

    // Optimistic removal must not lose the row when the call fails.
    await waitFor(() => expect(screen.getByText(/spec.pdf/)).toBeTruthy())
    expect(mockedDetach).toHaveBeenCalledWith('a1')
  })

  it('offers no remove button on someone else’s file', async () => {
    // Detaching is uploader-only server-side; the UI must not pretend otherwise.
    mockedGet.mockResolvedValue([attachment({ uploadedById: 'someone-else' })])
    render(<TaskAttachments taskId="t1" />)
    await screen.findByText(/spec.pdf/)

    expect(screen.queryByLabelText('attachments.remove')).toBeNull()
  })

  it('reloads when the modal is reused for another task', async () => {
    mockedGet.mockResolvedValue([attachment()])
    const { rerender } = render(<TaskAttachments taskId="t1" />)
    await waitFor(() => expect(mockedGet).toHaveBeenCalledWith('t1'))

    rerender(<TaskAttachments taskId="t2" />)
    await waitFor(() => expect(mockedGet).toHaveBeenCalledWith('t2'))
  })
})
