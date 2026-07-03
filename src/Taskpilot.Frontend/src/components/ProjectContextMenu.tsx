import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { menuContentClass as contentClass, menuItemClass as itemClass } from './contextMenuStyles'

/**
 * Right-click context menu for a project card: export its tasks as CSV and
 * archive (or restore, when already archived) the project. Wraps the card via
 * Radix's ContextMenu.Trigger.
 */
export default function ProjectContextMenu({
  children,
  archived,
  onEdit,
  onExport,
  onArchive,
  onRestore,
}: {
  children: ReactNode
  archived: boolean
  onEdit: () => void
  onExport: () => void
  onArchive: () => void
  onRestore: () => void
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
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
