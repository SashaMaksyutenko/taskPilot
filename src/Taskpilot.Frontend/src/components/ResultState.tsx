import { motion } from 'framer-motion'
import { AlertTriangle, CheckCircle2, Inbox } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '../lib/cn'

type Variant = 'success' | 'error' | 'empty'

const ICONS: Record<Variant, typeof CheckCircle2> = {
  success: CheckCircle2,
  error: AlertTriangle,
  empty: Inbox,
}

const TONES: Record<Variant, string> = {
  success: 'text-emerald-500',
  error: 'text-red-500',
  empty: 'text-muted',
}

/**
 * A centered illustrated state (success / error / empty) with a springy icon
 * entrance. A dependency-free alternative to a Lottie animation for result and
 * error screens. Honors reduced-motion by skipping the transform.
 */
export default function ResultState({
  variant = 'empty',
  message,
  children,
}: {
  variant?: Variant
  message: string
  children?: ReactNode
}) {
  const Icon = ICONS[variant]
  const reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia?.('(prefers-reduced-motion: reduce)').matches

  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
      <motion.div
        initial={reduceMotion ? false : { scale: 0, rotate: -12 }}
        animate={{ scale: 1, rotate: 0 }}
        transition={{ type: 'spring', stiffness: 300, damping: 15 }}
      >
        <Icon className={cn('h-14 w-14', TONES[variant])} strokeWidth={1.5} />
      </motion.div>
      <p className="text-sm text-muted">{message}</p>
      {children}
    </div>
  )
}
