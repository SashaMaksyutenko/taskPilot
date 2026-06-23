import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

const PRIORITIES = ['High', 'Medium', 'Low']

const itemClass =
  'cursor-pointer rounded px-3 py-1.5 text-sm outline-none data-[highlighted]:bg-slate-100 dark:data-[highlighted]:bg-slate-700'
const contentClass =
  'z-50 min-w-40 rounded-lg border border-slate-200 bg-white p-1 shadow-lg dark:border-slate-700 dark:bg-slate-800'

/**
 * Right-click (or long-press) context menu for a task card: edit, change priority
 * and delete. Wraps the card via Radix's ContextMenu.Trigger.
 */
export default function TaskContextMenu({
  children,
  onEdit,
  onChangePriority,
  onDelete,
}: {
  children: ReactNode
  onEdit: () => void
  onChangePriority: (priority: string) => void
  onDelete: () => void
}) {
  const { t } = useTranslation()

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={onEdit}>
            {t('board.edit')}
          </ContextMenu.Item>

          {/* Change priority submenu */}
          <ContextMenu.Sub>
            <ContextMenu.SubTrigger className={`${itemClass} flex items-center justify-between gap-4`}>
              {t('taskModal.priority')}
              <span className="text-slate-400">▸</span>
            </ContextMenu.SubTrigger>
            <ContextMenu.Portal>
              <ContextMenu.SubContent className={contentClass}>
                {PRIORITIES.map((p) => (
                  <ContextMenu.Item key={p} className={itemClass} onSelect={() => onChangePriority(p)}>
                    {t(`board.priority.${p}`, p)}
                  </ContextMenu.Item>
                ))}
              </ContextMenu.SubContent>
            </ContextMenu.Portal>
          </ContextMenu.Sub>

          <ContextMenu.Separator className="my-1 h-px bg-slate-100 dark:bg-slate-700" />

          <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
            {t('taskModal.deleteTask')}
          </ContextMenu.Item>
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
