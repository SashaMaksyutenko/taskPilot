import type { ReactNode } from 'react'
import Card from './Card'

/** Settings page section — consistent card wrapper with optional title. */
export default function SettingsSection({
  title,
  description,
  children,
  className = '',
  danger,
}: {
  title: string
  description?: string
  children: ReactNode
  className?: string
  danger?: boolean
}) {
  if (danger) {
    return (
      <section className={`mb-8 rounded-[var(--radius-card)] border border-red-300 bg-red-50 p-6 dark:border-red-800 dark:bg-red-950/30 ${className}`}>
        <h2 className="mb-1 font-bold text-red-700 dark:text-red-300">{title}</h2>
        {description && <p className="mb-4 text-sm text-red-700/80 dark:text-red-300/80">{description}</p>}
        {children}
      </section>
    )
  }

  return (
    <Card className={`mb-8 p-6 ${className}`}>
      <h2 className="mb-1 font-bold text-foreground">{title}</h2>
      {description && <p className="mb-4 text-sm text-muted">{description}</p>}
      {children}
    </Card>
  )
}
