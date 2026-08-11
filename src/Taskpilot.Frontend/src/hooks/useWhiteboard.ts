import { useCallback, useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import * as Y from 'yjs'
import { Awareness, applyAwarenessUpdate, encodeAwarenessUpdate } from 'y-protocols/awareness'
import { createCollabConnection } from '../lib/collabHub'
import { base64ToBytes, bytesToBase64 } from '../lib/base64'

/** A sticky note on the shared whiteboard. Stored as a plain object in a Yjs map. */
export interface Note {
  id: string
  x: number
  y: number
  text: string
  color: string
}

/** Another user present on the board, with their live cursor (canvas coordinates). */
export interface BoardPeer {
  clientId: number
  name: string
  color: string
  cursor: { x: number; y: number } | null
}

export interface UseWhiteboardOptions {
  /** Document id, e.g. `"board:{projectId}"`. Null disables the board. */
  docId: string | null
  /** The local user's presence identity. */
  user: { name: string; color: string }
  /** Turn syncing on/off. */
  enabled: boolean
}

export interface UseWhiteboardResult {
  notes: Note[]
  ready: boolean
  peers: BoardPeer[]
  addNote: (note: Note) => void
  updateNote: (id: string, patch: Partial<Note>) => void
  removeNote: (id: string) => void
  /** Broadcast the local cursor position (canvas coordinates), or null to clear it. */
  setCursor: (pos: { x: number; y: number } | null) => void
}

const PERSIST_DEBOUNCE_MS = 1500

/**
 * A collaborative whiteboard of sticky notes over a Yjs document, synced through the SignalR
 * collab hub — the same relay + snapshot store that powers task-description editing (see
 * [[collab-crdt]]). Notes live in a Y.Map keyed by id; presence/cursors ride on Awareness.
 */
export function useWhiteboard({ docId, user, enabled }: UseWhiteboardOptions): UseWhiteboardResult {
  const [notes, setNotes] = useState<Note[]>([])
  const [ready, setReady] = useState(false)
  const [peers, setPeers] = useState<BoardPeer[]>([])

  const docRef = useRef<Y.Doc | null>(null)
  const mapRef = useRef<Y.Map<Note> | null>(null)
  const awarenessRef = useRef<Awareness | null>(null)
  const connRef = useRef<HubConnection | null>(null)

  const userRef = useRef(user)
  useEffect(() => {
    userRef.current = user
  })

  useEffect(() => {
    if (!enabled || !docId) return

    let disposed = false
    const doc = new Y.Doc()
    const map = doc.getMap<Note>('notes')
    const awareness = new Awareness(doc)
    const conn = createCollabConnection()
    docRef.current = doc
    mapRef.current = map
    awarenessRef.current = awareness
    connRef.current = conn

    awareness.setLocalStateField('user', { name: userRef.current.name, color: userRef.current.color })

    // Doc → React: mirror the notes map into state.
    const readAll = () => setNotes(Array.from(map.values()))
    map.observe(readAll)

    // Local edits → relay + debounced snapshot.
    let persistTimer: ReturnType<typeof setTimeout> | null = null
    const schedulePersist = () => {
      if (persistTimer) clearTimeout(persistTimer)
      persistTimer = setTimeout(() => {
        if (disposed || conn.state !== 'Connected') return
        conn.invoke('PersistState', docId, bytesToBase64(Y.encodeStateAsUpdate(doc))).catch(() => {})
      }, PERSIST_DEBOUNCE_MS)
    }
    const onDocUpdate = (update: Uint8Array, origin: unknown) => {
      if (origin === 'remote') return
      if (conn.state === 'Connected') conn.invoke('SendUpdate', docId, bytesToBase64(update)).catch(() => {})
      schedulePersist()
    }
    doc.on('update', onDocUpdate)

    // Awareness → React (peers + cursors) and relay local awareness changes.
    const refreshPeers = () => {
      const list: BoardPeer[] = []
      awareness.getStates().forEach((state, clientId) => {
        if (clientId === awareness.clientID) return
        const s = state as { user?: { name?: string; color?: string }; cursor?: { x: number; y: number } | null }
        if (s.user) list.push({ clientId, name: s.user.name ?? 'Someone', color: s.user.color ?? '#888', cursor: s.cursor ?? null })
      })
      setPeers(list)
    }
    awareness.on('change', refreshPeers)
    const onAwarenessUpdate = (
      { added, updated, removed }: { added: number[]; updated: number[]; removed: number[] },
      origin: unknown,
    ) => {
      if (origin === 'remote') return
      const changed = [...added, ...updated, ...removed]
      if (conn.state === 'Connected') {
        conn.invoke('SendAwareness', docId, bytesToBase64(encodeAwarenessUpdate(awareness, changed))).catch(() => {})
      }
    }
    awareness.on('update', onAwarenessUpdate)

    // Server → client relays.
    conn.on('ReceiveState', (incomingDocId: string, base64: string | null) => {
      if (incomingDocId !== docId || disposed) return
      if (base64) Y.applyUpdate(doc, base64ToBytes(base64), 'remote')
      setReady(true)
    })
    conn.on('ReceiveUpdate', (incomingDocId: string, base64: string) => {
      if (incomingDocId === docId && !disposed) Y.applyUpdate(doc, base64ToBytes(base64), 'remote')
    })
    conn.on('ReceiveAwareness', (incomingDocId: string, base64: string) => {
      if (incomingDocId === docId && !disposed) applyAwarenessUpdate(awareness, base64ToBytes(base64), 'remote')
    })
    conn.on('PeerJoined', (incomingDocId: string) => {
      if (incomingDocId !== docId || conn.state !== 'Connected') return
      conn.invoke('SendAwareness', docId, bytesToBase64(encodeAwarenessUpdate(awareness, [awareness.clientID]))).catch(() => {})
    })
    conn.onreconnected(() => {
      conn.invoke('JoinDocument', docId).catch(() => {})
    })

    conn
      .start()
      .then(() => (disposed ? undefined : conn.invoke('JoinDocument', docId)))
      .catch(() => {})

    return () => {
      disposed = true
      if (persistTimer) clearTimeout(persistTimer)
      map.unobserve(readAll)
      doc.off('update', onDocUpdate)
      awareness.off('change', refreshPeers)
      awareness.off('update', onAwarenessUpdate)
      awareness.setLocalState(null)
      if (conn.state === 'Connected') {
        conn.invoke('SendAwareness', docId, bytesToBase64(encodeAwarenessUpdate(awareness, [awareness.clientID]))).catch(() => {})
        conn.invoke('LeaveDocument', docId).catch(() => {})
      }
      conn.stop().catch(() => {})
      awareness.destroy()
      doc.destroy()
      docRef.current = null
      mapRef.current = null
      awarenessRef.current = null
      connRef.current = null
      setReady(false)
      setPeers([])
      setNotes([])
    }
  }, [docId, enabled])

  const addNote = useCallback((note: Note) => {
    mapRef.current?.set(note.id, note)
  }, [])

  const updateNote = useCallback((id: string, patch: Partial<Note>) => {
    const map = mapRef.current
    const current = map?.get(id)
    if (map && current) map.set(id, { ...current, ...patch })
  }, [])

  const removeNote = useCallback((id: string) => {
    mapRef.current?.delete(id)
  }, [])

  const setCursor = useCallback((pos: { x: number; y: number } | null) => {
    awarenessRef.current?.setLocalStateField('cursor', pos)
  }, [])

  return { notes, ready, peers, addNote, updateNote, removeNote, setCursor }
}
