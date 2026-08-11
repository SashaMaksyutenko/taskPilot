import { useCallback, useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { createWhiteboardConnection } from '../lib/whiteboardHub'
import { whiteboardService, type Note } from '../services/whiteboardService'

export type { Note }

/** Another user present on the board, with their live cursor (canvas coordinates). */
export interface BoardPeer {
  connectionId: string
  name: string
  color: string
  cursor: { x: number; y: number } | null
}

export interface UseWhiteboardOptions {
  projectId: string | null
  user: { id: string; name: string; color: string }
  enabled: boolean
}

export interface UseWhiteboardResult {
  notes: Note[]
  ready: boolean
  peers: BoardPeer[]
  /** Create a note (POST). */
  addNote: (x: number, y: number, color: string) => void
  /** Optimistic text edit + debounced persist. */
  editText: (id: string, text: string) => void
  /** Live drag: update locally and stream the position to peers (no persist). */
  moveNote: (id: string, x: number, y: number) => void
  /** Persist a note's final position after a drag. */
  commitMove: (id: string, x: number, y: number) => void
  /** Delete a note; resolves false when the server forbids it (not yours). */
  removeNote: (id: string) => Promise<boolean>
  /** Broadcast the local cursor (throttled), or null to stop. */
  setCursor: (pos: { x: number; y: number } | null) => void
}

const CURSOR_THROTTLE_MS = 50
const TEXT_SAVE_DEBOUNCE_MS = 500

/**
 * A collaborative whiteboard backed by an authoritative server: notes are REST CRUD (so per-note
 * delete permission is enforced), and realtime create/update/delete + live cursors/drags ride on
 * {@link createWhiteboardConnection}. See [[collab-crdt]] for why the whiteboard is authoritative
 * rather than pure-CRDT.
 */
export function useWhiteboard({ projectId, user, enabled }: UseWhiteboardOptions): UseWhiteboardResult {
  const [notes, setNotes] = useState<Note[]>([])
  const [ready, setReady] = useState(false)
  const [peers, setPeers] = useState<BoardPeer[]>([])

  const connRef = useRef<HubConnection | null>(null)
  const userRef = useRef(user)
  useEffect(() => {
    userRef.current = user
  })

  const upsert = useCallback((note: Note) => {
    setNotes((prev) => {
      const i = prev.findIndex((n) => n.id === note.id)
      if (i === -1) return [...prev, note]
      const copy = prev.slice()
      copy[i] = note
      return copy
    })
  }, [])

  const patchLocal = useCallback((id: string, patch: Partial<Note>) => {
    setNotes((prev) => prev.map((n) => (n.id === id ? { ...n, ...patch } : n)))
  }, [])

  useEffect(() => {
    if (!enabled || !projectId) return

    let disposed = false
    whiteboardService
      .getNotes(projectId)
      .then((list) => {
        if (!disposed) {
          setNotes(list)
          setReady(true)
        }
      })
      .catch(() => {})

    const conn = createWhiteboardConnection()
    connRef.current = conn

    conn.on('NoteUpserted', (note: Note) => !disposed && upsert(note))
    conn.on('NoteDeleted', (id: string) => !disposed && setNotes((prev) => prev.filter((n) => n.id !== id)))
    conn.on('LiveMove', ({ noteId, x, y }: { noteId: string; x: number; y: number }) =>
      !disposed && patchLocal(noteId, { x, y }),
    )
    conn.on('Cursor', (c: { connectionId: string; name: string; color: string; x: number; y: number }) => {
      if (disposed) return
      setPeers((prev) => {
        const rest = prev.filter((p) => p.connectionId !== c.connectionId)
        return [...rest, { connectionId: c.connectionId, name: c.name, color: c.color, cursor: { x: c.x, y: c.y } }]
      })
    })
    conn.on('PeerLeft', (connectionId: string) =>
      !disposed && setPeers((prev) => prev.filter((p) => p.connectionId !== connectionId)),
    )
    conn.onreconnected(() => conn.invoke('JoinBoard', projectId).catch(() => {}))

    conn
      .start()
      .then(() => (disposed ? undefined : conn.invoke('JoinBoard', projectId)))
      .catch(() => {})

    return () => {
      disposed = true
      if (conn.state === 'Connected') conn.invoke('LeaveBoard', projectId).catch(() => {})
      conn.stop().catch(() => {})
      connRef.current = null
      setReady(false)
      setNotes([])
      setPeers([])
    }
  }, [projectId, enabled, upsert, patchLocal])

  const addNote = useCallback(
    (x: number, y: number, color: string) => {
      if (!projectId) return
      whiteboardService.createNote(projectId, { x, y, color, text: '' }).then(upsert).catch(() => {})
    },
    [projectId, upsert],
  )

  // Debounced text persistence, keyed per note so two notes don't share a timer.
  const textTimers = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map())
  const editText = useCallback(
    (id: string, text: string) => {
      patchLocal(id, { text }) // optimistic
      const timers = textTimers.current
      const existing = timers.get(id)
      if (existing) clearTimeout(existing)
      timers.set(
        id,
        setTimeout(() => {
          timers.delete(id)
          whiteboardService.updateNote(id, { text }).then(upsert).catch(() => {})
        }, TEXT_SAVE_DEBOUNCE_MS),
      )
    },
    [patchLocal, upsert],
  )

  const moveNote = useCallback(
    (id: string, x: number, y: number) => {
      patchLocal(id, { x, y })
      const conn = connRef.current
      if (projectId && conn?.state === 'Connected') conn.invoke('SendMove', projectId, id, x, y).catch(() => {})
    },
    [projectId, patchLocal],
  )

  const commitMove = useCallback(
    (id: string, x: number, y: number) => {
      whiteboardService.updateNote(id, { x, y }).then(upsert).catch(() => {})
    },
    [upsert],
  )

  const removeNote = useCallback(async (id: string) => {
    const ok = await whiteboardService.deleteNote(id).catch(() => false)
    if (ok) setNotes((prev) => prev.filter((n) => n.id !== id))
    return ok
  }, [])

  const lastCursor = useRef(0)
  const setCursor = useCallback(
    (pos: { x: number; y: number } | null) => {
      if (!pos || !projectId) return
      const now = Date.now()
      if (now - lastCursor.current < CURSOR_THROTTLE_MS) return
      lastCursor.current = now
      const conn = connRef.current
      if (conn?.state === 'Connected') {
        conn.invoke('SendCursor', projectId, userRef.current.name, userRef.current.color, pos.x, pos.y).catch(() => {})
      }
    },
    [projectId],
  )

  return { notes, ready, peers, addNote, editText, moveNote, commitMove, removeNote, setCursor }
}
