import confetti from 'canvas-confetti'

/**
 * Fires a short, celebratory confetti burst for a positive milestone
 * (e.g. a completed payment). No-op when the user prefers reduced motion.
 */
export function celebrate() {
  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
  confetti({
    particleCount: 90,
    spread: 70,
    origin: { y: 0.6 },
    scalar: 0.9,
  })
}
