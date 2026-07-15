import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// jsdom doesn't implement pointer capture; the drag hook calls it, so make it a no-op.
if (!Element.prototype.setPointerCapture) {
  Element.prototype.setPointerCapture = () => {}
}
if (!Element.prototype.releasePointerCapture) {
  Element.prototype.releasePointerCapture = () => {}
}

// Unmount React trees between tests so window listeners don't bleed across them.
afterEach(() => cleanup())
