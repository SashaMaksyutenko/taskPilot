import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from './contextMenuStyles'

/**
 * Right-click context menu for a forum topic card: copy its link and (for the
 * author or an admin) delete it.
 */
export default function TopicContextMenu({
  children,
  topicId,
  canDelete,
  onDelete,
}: {
  children: ReactNode
  topicId: string
  canDelete: boolean
  onDelete: () => void
}) {
  const { t } = useTranslation()

  const copyLink = () => {
    navigator.clipboard?.writeText(`${window.location.origin}/forum/${topicId}`).catch(() => {})
  }

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={copyLink}>
            {t('forum.copyLink')}
          </ContextMenu.Item>
          {canDelete && (
            <>
              <ContextMenu.Separator className={separatorClass} />
              <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
                {t('forum.delete')}
              </ContextMenu.Item>
            </>
          )}
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
