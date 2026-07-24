import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from '../contextMenuStyles'

/**
 * Right-click context menu for a project card: export its tasks as CSV and
 * archive (or restore, when already archived) the project. Wraps the card via
 * Radix's ContextMenu.Trigger.
 */
export default function ProjectContextMenu({
  children,
  archived,
  canMute = false,
  muted = false,
  onToggleMute,
  onEdit,
  onDuplicate,
  onSaveAsTemplate,
  onExport,
  onArchive,
  onRestore,
  onDelete,
}: {
  children: ReactNode
  archived: boolean
  /** True for projects the user is a member of (not the owner) — only these can be muted. */
  canMute?: boolean
  muted?: boolean
  onToggleMute?: () => void
  onEdit: () => void
  onDuplicate: () => void
  onSaveAsTemplate: () => void
  onExport: () => void
  onArchive: () => void
  onRestore: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={onEdit}>
            {t('projects.edit')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={onDuplicate}>
            {t('projects.duplicate')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={onSaveAsTemplate}>
            {t('templates.saveAs')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={onExport}>
            {t('board.exportCsv')}
          </ContextMenu.Item>
          {archived ? (
            <ContextMenu.Item className={itemClass} onSelect={onRestore}>
              {t('projects.restore')}
            </ContextMenu.Item>
          ) : (
            <ContextMenu.Item className={itemClass} onSelect={onArchive}>
              {t('projects.archive')}
            </ContextMenu.Item>
          )}
          {canMute && (
            <ContextMenu.Item className={itemClass} onSelect={onToggleMute}>
              {muted ? t('projects.unmute') : t('projects.mute')}
            </ContextMenu.Item>
          )}
          <ContextMenu.Separator className={separatorClass} />
          <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
            {t('projects.delete')}
          </ContextMenu.Item>
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
