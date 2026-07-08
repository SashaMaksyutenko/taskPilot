import { motion } from 'framer-motion'
import type { ReactNode } from 'react'

/**
 * Wraps content in a subtle fade-and-rise entrance animation. Used on page
 * content to make navigation feel smooth. Respects the user's reduced-motion
 * preference automatically (framer-motion disables transforms when set).
 */
export default function FadeIn({
  children,
  className,
  delay = 0,
}: {
  children: ReactNode
  className?: string
  delay?: number
}) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25, ease: 'easeOut', delay }}
    >
      {children}
    </motion.div>
  )
}
