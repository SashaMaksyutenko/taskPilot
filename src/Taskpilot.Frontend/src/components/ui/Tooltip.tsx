import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

type Side = 'top' | 'bottom'

/**
 * A lightweight CSS tooltip: wraps a trigger and reveals a small label on hover
 * or keyboard focus. No dependency and no portal — good for icon buttons.
 */
export default function Tooltip({
  label,
  side = 'bottom',
  children,
}: {
  label: string
  side?: Side
  children: ReactNode
}) {
  return (
    <span className="group/tooltip relative inline-flex">
      {children}
      <span
        role="tooltip"
        className={cn(
          'pointer-events-none absolute left-1/2 z-50 -translate-x-1/2 whitespace-nowrap rounded-md bg-foreground px-2 py-1 text-xs font-medium text-surface opacity-0 shadow-md transition group-hover/tooltip:opacity-100 group-focus-within/tooltip:opacity-100',
          side === 'bottom' ? 'top-full mt-1.5' : 'bottom-full mb-1.5',
        )}
      >
        {label}
      </span>
    </span>
  )
}
