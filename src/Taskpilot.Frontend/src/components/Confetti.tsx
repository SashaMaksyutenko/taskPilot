import { useEffect, useMemo } from 'react'
import { motion } from 'framer-motion'

// Brand + celebratory palette for the confetti pieces.
const COLORS = ['#4f46e5', '#ff8c42', '#10b981', '#f59e0b', '#0ea5e9', '#ec4899']

/**
 * A short, self-contained confetti burst. Renders a spray of colored pieces that
 * fly outward, spin and fall, then calls onDone so the parent can unmount it.
 * Purely decorative and pointer-transparent, so it never blocks the UI.
 */
export default function Confetti({ onDone }: { onDone: () => void }) {
  // Compute each piece's trajectory once, on mount.
  const pieces = useMemo(
    () =>
      Array.from({ length: 70 }).map((_, i) => {
        const angle = Math.random() * Math.PI * 2 // full-circle spread
        const distance = 120 + Math.random() * 220
        return {
          id: i,
          color: COLORS[i % COLORS.length],
          dx: Math.cos(angle) * distance,
          // Add gravity so pieces drift downward as they fade.
          dy: Math.sin(angle) * distance + 220 + Math.random() * 160,
          rotate: (Math.random() - 0.5) * 720,
          size: 6 + Math.random() * 6,
          delay: Math.random() * 0.1,
        }
      }),
    [],
  )

  useEffect(() => {
    const timer = setTimeout(onDone, 1600)
    return () => clearTimeout(timer)
  }, [onDone])

  return (
    <div className="pointer-events-none fixed inset-0 z-[100] overflow-hidden">
      {pieces.map((p) => (
        <motion.span
          key={p.id}
          className="absolute left-1/2 top-1/3 rounded-[2px]"
          style={{ width: p.size, height: p.size, background: p.color }}
          initial={{ opacity: 1, x: 0, y: 0, rotate: 0 }}
          animate={{ opacity: 0, x: p.dx, y: p.dy, rotate: p.rotate }}
          transition={{ duration: 1.4, ease: 'easeOut', delay: p.delay }}
        />
      ))}
    </div>
  )
}
