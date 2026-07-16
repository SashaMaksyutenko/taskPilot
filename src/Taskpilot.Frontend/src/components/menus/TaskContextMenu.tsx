import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  menuContentClass as contentClass,
  menuItemClass as itemClass,
  menuSeparatorClass as separatorClass,
} from '../contextMenuStyles'

const PRIORITIES = ['High', 'Medium', 'Low']

/**
 * Right-click (or long-press) context menu for a task card: edit, change priority
 * and delete. Wraps the card via Radix's ContextMenu.Trigger.
 */
export default function TaskContextMenu({
  children,
  onEdit,
  onDuplicate,
  onCopyLink,
  onChangePriority,
  assignTargets,
  onAssign,
  moveTargets,
  onMove,
  onDelete,
  bookmarked,
  onBookmark,
}: {
  children: ReactNode
  onEdit: () => void
  onDuplicate: () => void
  onCopyLink: () => void
  onChangePriority: (priority: string) => void
  assignTargets: { id: string; name: string }[]
  onAssign: (userId: string | null) => void
  moveTargets: { id: string; name: string }[]
  onMove: (projectId: string) => void
  onDelete: () => void
  bookmarked?: boolean
  onBookmark?: () => void
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

          <ContextMenu.Item className={itemClass} onSelect={onCopyLink}>
            {t('board.copyLink')}
          </ContextMenu.Item>

          {onBookmark && (
            <ContextMenu.Item className={itemClass} onSelect={onBookmark}>
              {bookmarked ? t('bookmarks.remove') : t('bookmarks.add')}
            </ContextMenu.Item>
          )}

          {/* Change priority submenu */}
          <ContextMenu.Sub>
            <ContextMenu.SubTrigger className={`${itemClass} flex items-center justify-between gap-4`}>
              {t('taskModal.priority')}
              <span className="text-muted">▸</span>
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

          {/* Assign to a member */}
          {assignTargets.length > 0 && (
            <ContextMenu.Sub>
              <ContextMenu.SubTrigger className={`${itemClass} flex items-center justify-between gap-4`}>
                {t('board.assignTo')}
                <span className="text-muted">▸</span>
              </ContextMenu.SubTrigger>
              <ContextMenu.Portal>
                <ContextMenu.SubContent className={contentClass}>
                  {assignTargets.map((m) => (
                    <ContextMenu.Item key={m.id} className={itemClass} onSelect={() => onAssign(m.id)}>
                      {m.name}
                    </ContextMenu.Item>
                  ))}
                  <ContextMenu.Separator className={separatorClass} />
                  <ContextMenu.Item className={itemClass} onSelect={() => onAssign(null)}>
                    {t('board.unassign')}
                  </ContextMenu.Item>
                </ContextMenu.SubContent>
              </ContextMenu.Portal>
            </ContextMenu.Sub>
          )}

          {/* Move to another project */}
          {moveTargets.length > 0 && (
            <ContextMenu.Sub>
              <ContextMenu.SubTrigger className={`${itemClass} flex items-center justify-between gap-4`}>
                {t('board.moveToProject')}
                <span className="text-muted">▸</span>
              </ContextMenu.SubTrigger>
              <ContextMenu.Portal>
                <ContextMenu.SubContent className={contentClass}>
                  {moveTargets.map((p) => (
                    <ContextMenu.Item key={p.id} className={itemClass} onSelect={() => onMove(p.id)}>
                      {p.name}
                    </ContextMenu.Item>
                  ))}
                </ContextMenu.SubContent>
              </ContextMenu.Portal>
            </ContextMenu.Sub>
          )}

          <ContextMenu.Separator className={separatorClass} />

          <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
            {t('taskModal.deleteTask')}
          </ContextMenu.Item>
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
