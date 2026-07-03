import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from './contextMenuStyles'

/**
 * Right-click context menu for a chat message: copy its text and (for your own
 * messages) edit or delete it.
 */
export default function MessageContextMenu({
  children,
  content,
  isPinned,
  canDelete,
  canEdit,
  onEdit,
  onTogglePin,
  onDelete,
}: {
  children: ReactNode
  content: string
  isPinned: boolean
  canDelete: boolean
  canEdit?: boolean
  onEdit?: () => void
  onTogglePin: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()

  const copy = () => {
    navigator.clipboard?.writeText(content).catch(() => {})
  }

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={copy}>
            {t('chat.copy')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={onTogglePin}>
            {isPinned ? t('chat.unpin') : t('chat.pin')}
          </ContextMenu.Item>
          {canEdit && onEdit && (
            <ContextMenu.Item className={itemClass} onSelect={onEdit}>
              {t('chat.edit')}
            </ContextMenu.Item>
          )}
          {canDelete && (
            <>
              <ContextMenu.Separator className={separatorClass} />
              <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
                {t('chat.delete')}
              </ContextMenu.Item>
            </>
          )}
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
