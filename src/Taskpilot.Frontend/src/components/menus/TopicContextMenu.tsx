import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from '../contextMenuStyles'

/**
 * Right-click context menu for a forum topic card: copy its link, moderation
 * actions (pin/lock) for admins/authors, and delete for the author or an admin.
 */
export default function TopicContextMenu({
  children,
  topicId,
  canDelete,
  onDelete,
  isPinned,
  canPin,
  onTogglePin,
  isLocked,
  canLock,
  onToggleLock,
  bookmarked,
  onBookmark,
}: {
  children: ReactNode
  topicId: string
  canDelete: boolean
  onDelete: () => void
  isPinned?: boolean
  canPin?: boolean
  onTogglePin?: () => void
  isLocked?: boolean
  canLock?: boolean
  onToggleLock?: () => void
  bookmarked?: boolean
  onBookmark?: () => void
}) {
  const { t } = useTranslation()

  const copyLink = () => {
    navigator.clipboard?.writeText(`${window.location.origin}/forum/${topicId}`).catch(() => {})
  }

  const showModeration = (canPin && onTogglePin) || (canLock && onToggleLock)

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={copyLink}>
            {t('forum.copyLink')}
          </ContextMenu.Item>

          {onBookmark && (
            <ContextMenu.Item className={itemClass} onSelect={onBookmark}>
              {bookmarked ? t('bookmarks.remove') : t('bookmarks.add')}
            </ContextMenu.Item>
          )}

          {showModeration && (
            <>
              <ContextMenu.Separator className={separatorClass} />
              {canPin && onTogglePin && (
                <ContextMenu.Item className={itemClass} onSelect={onTogglePin}>
                  {isPinned ? t('forum.unpin') : t('forum.pin')}
                </ContextMenu.Item>
              )}
              {canLock && onToggleLock && (
                <ContextMenu.Item className={itemClass} onSelect={onToggleLock}>
                  {isLocked ? t('forum.unlock') : t('forum.lock')}
                </ContextMenu.Item>
              )}
            </>
          )}

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
