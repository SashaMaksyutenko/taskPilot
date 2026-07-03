import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  menuContentClass as contentClass,
  menuItemClass as itemClass,
  menuSeparatorClass as separatorClass,
} from './contextMenuStyles'

const PRIORITIES = ['High', 'Medium', 'Low']

/**
 * Right-click (or long-press) context menu for a task card: edit, change priority
 * and delete. Wraps the card via Radix's ContextMenu.Trigger.
 */
export default function TaskContextMenu({
  children,
  onEdit,
  onDuplicate,
  onChangePriority,
  onDelete,
}: {
  children: ReactNode
  onEdit: () => void
  onDuplicate: () => void
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

          <ContextMenu.Item className={itemClass} onSelect={onDuplicate}>
            {t('board.duplicate')}
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

          <ContextMenu.Separator className={separatorClass} />

          <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
            {t('taskModal.deleteTask')}
          </ContextMenu.Item>
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
