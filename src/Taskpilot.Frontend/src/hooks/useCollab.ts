import { useCallback, useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import * as Y from 'yjs'
import { Awareness, applyAwarenessUpdate, encodeAwarenessUpdate } from 'y-protocols/awareness'
import { createCollabConnection } from '../lib/collabHub'
import { base64ToBytes, bytesToBase64 } from '../lib/base64'
import { diffText } from '../lib/textDiff'

/** An editor other than us currently present in the document. */
export interface CollabPeer {
  clientId: number
  name: string
  color: string
}

export interface UseCollabOptions {
  /** Document id, e.g. `"task:{guid}"`. Null disables collaboration. */
  docId: string | null
  /** Seed text used only when the server has no snapshot yet (first ever open). */
  initialText: string
  /** The local user's presence identity. */
  user: { name: string; color: string }
  /** Turn the whole thing on/off (e.g. only when the editor is open and writable). */
  enabled: boolean
}

export interface UseCollabResult {
  /** Live document text (reflects local + remote edits). */
  text: string
  /** Apply a local edit (the full new textarea value); diffed onto the CRDT. */
  applyLocal: (next: string) => void
  /** Broadcast the local caret/selection so peers can show presence. */
  setCursor: (anchor: number, head: number) => void
  /** True once the initial state has been received/seeded. */
  ready: boolean
  /** True while the realtime connection is live. */
  connected: boolean
  /** Other editors currently in the document. */
  peers: CollabPeer[]
}

const PERSIST_DEBOUNCE_MS = 1500

/**
 * Collaborative editing over a Yjs document synced through the SignalR collab hub. The CRDT lives
 * here on the client; the server only relays updates/awareness and stores snapshots. See
 * [[collab-crdt]].
 */
export function useCollab({ docId, initialText, user, enabled }: UseCollabOptions): UseCollabResult {
  const [text, setText] = useState(initialText)
  const [ready, setReady] = useState(false)
  const [connected, setConnected] = useState(false)
  const [peers, setPeers] = useState<CollabPeer[]>([])

  const docRef = useRef<Y.Doc | null>(null)
  const textRef = useRef<Y.Text | null>(null)
  const awarenessRef = useRef<Awareness | null>(null)
  const connRef = useRef<HubConnection | null>(null)

  // Keep the latest identity/seed without re-running the connection effect on every keystroke.
  // (useRef seeds `current` on first render; this effect keeps it fresh on later renders.)
  const userRef = useRef(user)
  const initialRef = useRef(initialText)
  useEffect(() => {
    userRef.current = user
    initialRef.current = initialText
  })

  useEffect(() => {
    if (!enabled || !docId) return

    let disposed = false
    const doc = new Y.Doc()
    const ytext = doc.getText('content')
    const awareness = new Awareness(doc)
    const conn = createCollabConnection()
    docRef.current = doc
    textRef.current = ytext
    awarenessRef.current = awareness
    connRef.current = conn

    awareness.setLocalStateField('user', { name: userRef.current.name, color: userRef.current.color })

    // Doc → React: mirror the CRDT text into state.
    const onTextChange = () => setText(ytext.toString())
    ytext.observe(onTextChange)

    // Local doc edits → relay to peers; schedule a snapshot persist.
    let persistTimer: ReturnType<typeof setTimeout> | null = null
    const schedulePersist = () => {
      if (persistTimer) clearTimeout(persistTimer)
      persistTimer = setTimeout(() => {
        if (disposed || conn.state !== 'Connected') return
        conn.invoke('PersistState', docId, bytesToBase64(Y.encodeStateAsUpdate(doc))).catch(() => {})
      }, PERSIST_DEBOUNCE_MS)
    }
    const onDocUpdate = (update: Uint8Array, origin: unknown) => {
      if (origin === 'remote') return // came from a peer; don't echo it back
      if (conn.state === 'Connected') conn.invoke('SendUpdate', docId, bytesToBase64(update)).catch(() => {})
      schedulePersist()
    }
    doc.on('update', onDocUpdate)

    // Awareness → React (who's here) + relay local awareness changes to peers.
    const refreshPeers = () => {
      const list: CollabPeer[] = []
      awareness.getStates().forEach((state, clientId) => {
        if (clientId === awareness.clientID) return
        const u = (state as { user?: { name?: string; color?: string } }).user
        if (u) list.push({ clientId, name: u.name ?? 'Someone', color: u.color ?? '#888' })
      })
      setPeers(list)
    }
    const onAwarenessChange = () => refreshPeers()
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
    awareness.on('change', onAwarenessChange)
    awareness.on('update', onAwarenessUpdate)

    // Server → client relays.
    conn.on('ReceiveState', (incomingDocId: string, base64: string | null) => {
      if (incomingDocId !== docId || disposed) return
      if (base64) {
        Y.applyUpdate(doc, base64ToBytes(base64), 'remote')
      } else if (ytext.length === 0 && initialRef.current) {
        // First ever open: seed from the task's current description, then persist a baseline so
        // later joiners get state (not null) and don't seed again.
        ytext.insert(0, initialRef.current)
        conn.invoke('PersistState', docId, bytesToBase64(Y.encodeStateAsUpdate(doc))).catch(() => {})
      }
      setReady(true)
    })
    conn.on('ReceiveUpdate', (incomingDocId: string, base64: string) => {
      if (incomingDocId === docId && !disposed) Y.applyUpdate(doc, base64ToBytes(base64), 'remote')
    })
    conn.on('ReceiveAwareness', (incomingDocId: string, base64: string) => {
      if (incomingDocId === docId && !disposed) applyAwarenessUpdate(awareness, base64ToBytes(base64), 'remote')
    })
    conn.on('PeerJoined', (incomingDocId: string) => {
      // A newcomer needs our cursor — rebroadcast our full awareness state.
      if (incomingDocId !== docId || conn.state !== 'Connected') return
      conn.invoke('SendAwareness', docId, bytesToBase64(encodeAwarenessUpdate(awareness, [awareness.clientID]))).catch(() => {})
    })
    conn.onreconnected(() => {
      setConnected(true)
      conn.invoke('JoinDocument', docId).catch(() => {})
    })
    conn.onclose(() => setConnected(false))

    conn
      .start()
      .then(() => {
        if (disposed) return
        setConnected(true)
        return conn.invoke('JoinDocument', docId)
      })
      .catch(() => {})

    return () => {
      disposed = true
      if (persistTimer) clearTimeout(persistTimer)
      ytext.unobserve(onTextChange)
      doc.off('update', onDocUpdate)
      awareness.off('change', onAwarenessChange)
      awareness.off('update', onAwarenessUpdate)
      // Tell peers we're gone (clears our cursor), then tear down.
      awareness.setLocalState(null)
      if (conn.state === 'Connected') {
        conn.invoke('SendAwareness', docId, bytesToBase64(encodeAwarenessUpdate(awareness, [awareness.clientID]))).catch(() => {})
        conn.invoke('LeaveDocument', docId).catch(() => {})
      }
      conn.stop().catch(() => {})
      awareness.destroy()
      doc.destroy()
      docRef.current = null
      textRef.current = null
      awarenessRef.current = null
      connRef.current = null
      setReady(false)
      setPeers([])
    }
  }, [docId, enabled])

  // A local textarea edit: diff against the CRDT text and apply the one changed region.
  const applyLocal = useCallback((next: string) => {
    const ytext = textRef.current
    const doc = docRef.current
    if (!ytext || !doc) {
      setText(next) // collaboration off — behave like a plain controlled field
      return
    }
    const current = ytext.toString()
    if (next === current) return
    const { index, deleteCount, insert } = diffText(current, next)
    doc.transact(() => {
      if (deleteCount > 0) ytext.delete(index, deleteCount)
      if (insert) ytext.insert(index, insert)
    })
  }, [])

  const setCursor = useCallback((anchor: number, head: number) => {
    awarenessRef.current?.setLocalStateField('cursor', { anchor, head })
  }, [])

  return { text, applyLocal, setCursor, ready, connected, peers }
}
