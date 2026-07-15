import {
  useRef,
  useState,
  type CSSProperties,
  type PointerEvent as ReactPointerEvent,
  type RefObject,
} from 'react'
import { createPortal } from 'react-dom'

/**
 * Pointer-events drag-and-drop that works with a mouse, a pen AND touch — unlike the
 * native HTML5 drag API, which silently does nothing on touch screens.
 *
 * How it engages:
 *  - mouse/pen: start dragging once the pointer moves past a small threshold.
 *  - touch: a press-and-hold OR a decisive move picks the item up, so a quick swipe
 *    from the gaps between items still scrolls the page.
 *
 * Implementation notes that matter for reliability:
 *  - The window pointer handlers are created ONCE (in a controller kept in a ref) and
 *    read the latest callbacks/state through refs. If they were re-created every render
 *    (opts is a fresh object each time), removeEventListener would fail to detach the
 *    right function, listeners would pile up and fire with stale closures — which makes
 *    dragging flaky.
 *  - The dragged pointer is captured, so we keep receiving move/up events even when the
 *    pointer travels over other elements or leaves the window.
 *  - Drop targets are found with document.elementFromPoint (the ghost has
 *    pointer-events:none so it doesn't shadow the hit-test); mark them with dropZoneProps.
 */
export function useDragAndDrop(opts: {
  onDrop: (zoneKey: string, itemId: string) => void
  renderGhost?: (id: string) => React.ReactNode
  /** Pixels of movement before a mouse/pen drag starts. */
  threshold?: number
  /** Milliseconds to hold a touch before it becomes a drag. */
  touchHoldMs?: number
}) {
  // Latest opts, read by the stable handlers.
  const optsRef = useRef(opts)
  optsRef.current = opts

  const [draggingId, setDraggingId] = useState<string | null>(null)
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null)
  const [activeZone, setActiveZone] = useState<string | null>(null)

  // All mutable drag state — never triggers a render, always current for the handlers.
  const st = useRef({
    id: '',
    pointerId: -1,
    captureEl: null as Element | null,
    startX: 0,
    startY: 0,
    dragging: false,
    zone: null as string | null,
    justDragged: false,
    holdTimer: 0 as ReturnType<typeof setTimeout> | 0,
  })

  // The controller holds the window handlers. Built once so their references are stable.
  const ctrl = useRef<ReturnType<typeof makeController> | undefined>(undefined)
  if (!ctrl.current) {
    ctrl.current = makeController(st, optsRef, { setDraggingId, setPos, setActiveZone })
  }

  const draggableProps = (id: string) => ({
    onPointerDown: (e: ReactPointerEvent) => {
      // Left button only for the mouse; touch/pen have no button concept here.
      if (e.pointerType === 'mouse' && e.button !== 0) return
      ctrl.current!.start(id, e)
    },
    // touch-action:none delivers touch as pointer events instead of scrolling; the
    // user-select rules stop the mouse from selecting the card's text on press-drag,
    // which would otherwise hijack the gesture.
    style: {
      touchAction: 'none',
      userSelect: 'none',
      WebkitUserSelect: 'none',
      cursor: 'grab',
    } as CSSProperties,
  })

  const dropZoneProps = (key: string) => ({ 'data-dropzone': key })

  const overlay =
    draggingId && pos
      ? createPortal(
          <div
            style={{
              position: 'fixed',
              left: pos.x,
              top: pos.y,
              transform: 'translate(-50%, -50%)',
              pointerEvents: 'none',
              zIndex: 9999,
              opacity: 0.9,
            }}
          >
            {opts.renderGhost?.(draggingId)}
          </div>,
          document.body,
        )
      : null

  return {
    /** Spread onto anything that can be picked up. */
    draggableProps,
    /** Spread onto anything that can receive a drop; compare its key with activeZone. */
    dropZoneProps,
    /** Key of the drop zone currently under the pointer (for highlighting). */
    activeZone,
    /** Id of the item being dragged, or null. */
    draggingId,
    /** True for the click that immediately follows a drag — guard navigation with it. */
    justDragged: () => st.current.justDragged,
    /** The floating "ghost"; render it once anywhere in the tree. */
    overlay,
  }
}

type DragState = RefObject<{
  id: string
  pointerId: number
  captureEl: Element | null
  startX: number
  startY: number
  dragging: boolean
  zone: string | null
  justDragged: boolean
  holdTimer: ReturnType<typeof setTimeout> | 0
}>

/**
 * Builds the stable window pointer handlers. Kept out of React's render/deps entirely —
 * everything is read from the mutable state ref and the latest-opts ref, so the same
 * function references are used for both addEventListener and removeEventListener.
 */
function makeController(
  st: DragState,
  optsRef: RefObject<{
    onDrop: (zoneKey: string, itemId: string) => void
    threshold?: number
    touchHoldMs?: number
  }>,
  setters: {
    setDraggingId: (v: string | null) => void
    setPos: (v: { x: number; y: number } | null) => void
    setActiveZone: (v: string | null) => void
  },
) {
  const { setDraggingId, setPos, setActiveZone } = setters

  const updateZone = (x: number, y: number) => {
    const el = document.elementFromPoint(x, y) as HTMLElement | null
    const zone = (el?.closest('[data-dropzone]') as HTMLElement | null)?.dataset.dropzone ?? null
    st.current.zone = zone
    setActiveZone(zone)
  }

  const engage = (x: number, y: number) => {
    const s = st.current
    if (s.dragging) return
    s.dragging = true
    // Lock scrolling and text selection for the duration of the drag.
    document.body.style.touchAction = 'none'
    document.body.style.userSelect = 'none'
    setDraggingId(s.id)
    setPos({ x, y })
    updateZone(x, y)
  }

  const onMove = (e: PointerEvent) => {
    const s = st.current
    if (!s.id || e.pointerId !== s.pointerId) return
    if (!s.dragging) {
      const moved = Math.hypot(e.clientX - s.startX, e.clientY - s.startY)
      if (moved < (optsRef.current.threshold ?? 6)) return
      engage(e.clientX, e.clientY)
      return
    }
    setPos({ x: e.clientX, y: e.clientY })
    updateZone(e.clientX, e.clientY)
  }

  const finish = (commit: boolean) => {
    const s = st.current
    if (s.holdTimer) {
      clearTimeout(s.holdTimer)
      s.holdTimer = 0
    }
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    window.removeEventListener('pointercancel', onCancel)
    document.body.style.touchAction = ''
    document.body.style.userSelect = ''
    if (s.captureEl && s.pointerId !== -1) {
      try {
        s.captureEl.releasePointerCapture(s.pointerId)
      } catch {
        /* pointer already released */
      }
    }

    if (commit && s.dragging && s.zone) optsRef.current.onDrop(s.zone, s.id)

    // Swallow the click that a mouse fires right after a drag (so a dragged card doesn't
    // also navigate). Cleared on the next tick.
    s.justDragged = s.dragging
    if (s.justDragged) setTimeout(() => (st.current.justDragged = false), 0)

    s.id = ''
    s.pointerId = -1
    s.captureEl = null
    s.dragging = false
    s.zone = null
    setDraggingId(null)
    setPos(null)
    setActiveZone(null)
  }

  const onUp = () => finish(true)
  const onCancel = () => finish(false)

  const start = (id: string, e: ReactPointerEvent) => {
    const s = st.current
    // A stray previous drag (shouldn't happen) is cleaned up first.
    if (s.id) finish(false)

    s.id = id
    s.pointerId = e.pointerId
    s.startX = e.clientX
    s.startY = e.clientY
    s.dragging = false
    s.zone = null

    // Capture so move/up keep coming even over other elements or off-window.
    try {
      e.currentTarget.setPointerCapture(e.pointerId)
      s.captureEl = e.currentTarget
    } catch {
      s.captureEl = null
    }

    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
    window.addEventListener('pointercancel', onCancel)

    // On touch, a press-and-hold starts the drag even without any movement.
    if (e.pointerType === 'touch') {
      const holdMs = optsRef.current.touchHoldMs ?? 220
      s.holdTimer = setTimeout(() => {
        if (!st.current.dragging && st.current.id === id) engage(st.current.startX, st.current.startY)
      }, holdMs)
    }
  }

  return { start }
}
