import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { MoreHorizontal } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { menuContentClass, menuItemClass, menuSeparatorClass } from '../contextMenuStyles'

/**
 * Consolidates the board's secondary actions (members, configuration, import/export) into a
 * single "More" menu, so the toolbar stays clean instead of overflowing with buttons.
 * Items are gated by the caller's permissions.
 */
export default function BoardActionsMenu({
  isOwner,
  canWrite,
  onMembers,
  onAutomations,
  onFields,
  onEpics,
  onShare,
  onImport,
  onExportCsv,
  onExportXlsx,
  onExportPdf,
}: {
  isOwner: boolean
  canWrite: boolean
  onMembers: () => void
  onAutomations: () => void
  onFields: () => void
  onEpics: () => void
  onShare: () => void
  onImport: () => void
  onExportCsv: () => void
  onExportXlsx: () => void
  onExportPdf: () => void
}) {
  const { t } = useTranslation()

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button
          className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-border bg-surface px-3 text-sm font-semibold text-foreground transition hover:bg-canvas focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
        >
          <MoreHorizontal className="h-4 w-4" />
          {t('board.more')}
        </button>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" sideOffset={4} className={menuContentClass}>
          <DropdownMenu.Item className={menuItemClass} onSelect={onMembers}>
            {t('members.button')}
          </DropdownMenu.Item>
          {isOwner && (
            <DropdownMenu.Item className={menuItemClass} onSelect={onAutomations}>
              {t('automation.button')}
            </DropdownMenu.Item>
          )}
          {canWrite && (
            <DropdownMenu.Item className={menuItemClass} onSelect={onFields}>
              {t('customFields.button')}
            </DropdownMenu.Item>
          )}
          {canWrite && (
            <DropdownMenu.Item className={menuItemClass} onSelect={onEpics}>
              {t('epics.button')}
            </DropdownMenu.Item>
          )}
          {isOwner && (
            <DropdownMenu.Item className={menuItemClass} onSelect={onShare}>
              {t('share.button')}
            </DropdownMenu.Item>
          )}

          <DropdownMenu.Separator className={menuSeparatorClass} />

          {canWrite && (
            <DropdownMenu.Item className={menuItemClass} onSelect={onImport}>
              {t('board.importCsv')}
            </DropdownMenu.Item>
          )}
          <DropdownMenu.Item className={menuItemClass} onSelect={onExportCsv}>
            {t('board.exportCsv')}
          </DropdownMenu.Item>
          <DropdownMenu.Item className={menuItemClass} onSelect={onExportXlsx}>
            {t('board.exportXlsx')}
          </DropdownMenu.Item>
          <DropdownMenu.Item className={menuItemClass} onSelect={onExportPdf}>
            {t('board.exportPdf')}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}
