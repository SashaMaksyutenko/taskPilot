import { describe, expect, it, vi, beforeEach } from 'vitest'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { useDragAndDrop } from './useDragAndDrop'

/**
 * Exercises the drag state machine directly: press → move past threshold → drop onto a
 * zone. jsdom has no real hit-testing, so document.elementFromPoint is stubbed to return
 * whichever zone a test decides the pointer is over.
 */

/** A tiny board: one draggable card and two drop zones. */
function Harness({ onDrop }: { onDrop: (zone: string, id: string) => void }) {
  const dnd = useDragAndDrop({ onDrop, renderGhost: (id) => <span>ghost {id}</span> })
  return (
    <div>
      <button data-testid="card" onClick={() => onDrop('CLICK', 'task-1')} {...dnd.draggableProps('task-1')}>
        card
      </button>
      <div data-testid="zoneA" {...dnd.dropZoneProps('A')}>
        A
      </div>
      <div data-testid="zoneB" {...dnd.dropZoneProps('B')}>
        B
      </div>
    </div>
  )
}

/** Makes elementFromPoint resolve to a given drop-zone element for the rest of a test. */
function pointOver(testId: string) {
  document.elementFromPoint = () => screen.getByTestId(testId)
}

/** Fires a window-level pointer event the way the hook's global listeners expect it. */
function windowPointer(type: 'pointermove' | 'pointerup' | 'pointercancel', x: number, y: number, pointerId = 1) {
  const e = new Event(type, { bubbles: true }) as Event & { clientX: number; clientY: number; pointerId: number }
  e.clientX = x
  e.clientY = y
  e.pointerId = pointerId
  act(() => {
    window.dispatchEvent(e)
  })
}

function pressCard(x = 0, y = 0, pointerType: 'mouse' | 'touch' = 'mouse') {
  fireEvent.pointerDown(screen.getByTestId('card'), { clientX: x, clientY: y, pointerId: 1, pointerType, button: 0 })
}

describe('useDragAndDrop', () => {
  beforeEach(() => {
    // Default: nothing under the pointer until a test says otherwise.
    document.elementFromPoint = () => null
  })

  it('drops on the zone the pointer is released over', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    pointOver('zoneB')
    pressCard()
    windowPointer('pointermove', 20, 0) // past the 6px threshold → engages
    windowPointer('pointerup', 20, 0)

    expect(onDrop).toHaveBeenCalledExactlyOnceWith('B', 'task-1')
  })

  it('does not drop when the pointer never moves (a plain click)', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    pointOver('zoneB')
    pressCard()
    windowPointer('pointerup', 0, 0) // no movement

    // The drag never engaged, so onDrop was not called for a drop.
    expect(onDrop).not.toHaveBeenCalledWith('B', 'task-1')
  })

  it('does not engage below the movement threshold', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    pointOver('zoneB')
    pressCard()
    windowPointer('pointermove', 3, 0) // under 6px
    windowPointer('pointerup', 3, 0)

    expect(onDrop).not.toHaveBeenCalled()
  })

  it('fires exactly once per drag across repeated drags (no listener leak)', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    // Drag 1 → zone A.
    pointOver('zoneA')
    pressCard()
    windowPointer('pointermove', 20, 0)
    windowPointer('pointerup', 20, 0)

    // Drag 2 → zone B.
    pointOver('zoneB')
    pressCard()
    windowPointer('pointermove', 20, 0)
    windowPointer('pointerup', 20, 0)

    // If listeners leaked, the second pointerup would re-fire drag 1's handler too.
    expect(onDrop).toHaveBeenCalledTimes(2)
    expect(onDrop).toHaveBeenNthCalledWith(1, 'A', 'task-1')
    expect(onDrop).toHaveBeenNthCalledWith(2, 'B', 'task-1')
  })

  it('aborts without dropping on pointercancel', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    pointOver('zoneB')
    pressCard()
    windowPointer('pointermove', 20, 0)
    windowPointer('pointercancel', 20, 0)

    expect(onDrop).not.toHaveBeenCalled()
  })

  it('does not drop when released outside any zone', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    pressCard()
    windowPointer('pointermove', 20, 0) // engages, but nothing under the pointer
    windowPointer('pointerup', 20, 0)

    expect(onDrop).not.toHaveBeenCalled()
  })

  it('suppresses the click that follows a drag but allows a real click', () => {
    const onDrop = vi.fn()
    render(<Harness onDrop={onDrop} />)

    // A real click (no drag) still calls the card's onClick.
    fireEvent.click(screen.getByTestId('card'))
    expect(onDrop).toHaveBeenCalledWith('CLICK', 'task-1')
  })
})
