import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '../../lib/cn'

/** Elevated surface container with consistent border and shadow. */
export default function Card({
  className,
  children,
  hover,
  ...props
}: HTMLAttributes<HTMLDivElement> & { hover?: boolean; children: ReactNode }) {
  return (
    <div
      className={cn(
        'rounded-[var(--radius-card)] border border-border bg-surface shadow-soft',
        hover && 'transition hover:border-primary/20 hover:shadow-card',
        className,
      )}
      {...props}
    >
      {children}
    </div>
  )
}
