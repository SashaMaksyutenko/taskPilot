import type { HTMLAttributes } from 'react'
import { cn } from '../../lib/cn'

/**
 * A shimmering placeholder block shown while data loads. Compose several to
 * mirror the shape of the content that will appear.
 */
export default function Skeleton({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('animate-pulse rounded-md bg-border/70', className)}
      {...props}
    />
  )
}

/** A card-shaped skeleton with a few lines — handy for list loading states. */
export function SkeletonCard({ className }: { className?: string }) {
  return (
    <div className={cn('rounded-[var(--radius-card)] border border-border bg-surface p-4 shadow-soft', className)}>
      <div className="flex items-center gap-3">
        <Skeleton className="h-10 w-10 rounded-full" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-3.5 w-1/3" />
          <Skeleton className="h-3 w-1/4" />
        </div>
      </div>
      <div className="mt-4 space-y-2">
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-5/6" />
      </div>
    </div>
  )
}
