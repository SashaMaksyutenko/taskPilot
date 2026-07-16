import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from '../contextMenuStyles'

/**
 * Right-click context menu for a note card: pin/unpin, edit, export, copy the text
 * and delete. Mirrors the other entity context menus.
 */
export default function NoteContextMenu({
  children,
  isPinned,
  content,
  onTogglePin,
  onEdit,
  onExportPdf,
  onDelete,
}: {
  children: ReactNode
  isPinned: boolean
  content: string
  onTogglePin: () => void
  onEdit: () => void
  onExportPdf: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()

  const copyContent = () => {
    navigator.clipboard?.writeText(content).catch(() => {})
  }

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={onTogglePin}>
            {isPinned ? t('notes.unpin') : t('notes.pin')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={onEdit}>
            {t('notes.edit')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={copyContent}>
            {t('notes.copy')}
          </ContextMenu.Item>
          <ContextMenu.Item className={itemClass} onSelect={onExportPdf}>
            {t('notes.exportPdf')}
          </ContextMenu.Item>
          <ContextMenu.Separator className={separatorClass} />
          <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onDelete}>
            {t('notes.delete')}
          </ContextMenu.Item>
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
