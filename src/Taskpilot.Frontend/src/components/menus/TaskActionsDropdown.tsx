import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { useTranslation } from 'react-i18next'
import { menuContentClass, menuItemClass, menuSeparatorClass } from '../contextMenuStyles'

const PRIORITIES = ['High', 'Medium', 'Low']

/**
 * Hover "⋮" three-dot menu for a task card — the same actions as the right-click
 * context menu, exposed via a visible button for discoverability.
 */
export default function TaskActionsDropdown({
  onEdit,
  onDuplicate,
  onCopyLink,
  onChangePriority,
  assignTargets,
  onAssign,
  moveTargets,
  onMove,
  onViewHistory,
  onDelete,
}: {
  onEdit: () => void
  onDuplicate: () => void
  onCopyLink: () => void
  onChangePriority: (priority: string) => void
  assignTargets: { id: string; name: string }[]
  onAssign: (userId: string | null) => void
  moveTargets: { id: string; name: string }[]
  onMove: (projectId: string) => void
  onViewHistory: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button
          // Stop the click from reaching the card (which would open the detail modal).
          onClick={(e) => e.stopPropagation()}
          aria-label="Task actions"
          className="rounded p-1 leading-none text-muted hover:bg-canvas hover:text-foreground"
        >
          ⋮
        </button>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" className={menuContentClass}>
          <DropdownMenu.Item className={menuItemClass} onSelect={onEdit}>
            {t('board.edit')}
          </DropdownMenu.Item>

          <DropdownMenu.Item className={menuItemClass} onSelect={onDuplicate}>
            {t('board.duplicate')}
          </DropdownMenu.Item>

          <DropdownMenu.Item className={menuItemClass} onSelect={onCopyLink}>
            {t('board.copyLink')}
          </DropdownMenu.Item>

          <DropdownMenu.Sub>
            <DropdownMenu.SubTrigger className={`${menuItemClass} flex items-center justify-between gap-4`}>
              {t('taskModal.priority')}
              <span className="text-muted">▸</span>
            </DropdownMenu.SubTrigger>
            <DropdownMenu.Portal>
              <DropdownMenu.SubContent className={menuContentClass}>
                {PRIORITIES.map((p) => (
                  <DropdownMenu.Item key={p} className={menuItemClass} onSelect={() => onChangePriority(p)}>
                    {t(`board.priority.${p}`, p)}
                  </DropdownMenu.Item>
                ))}
              </DropdownMenu.SubContent>
            </DropdownMenu.Portal>
          </DropdownMenu.Sub>

          {assignTargets.length > 0 && (
            <DropdownMenu.Sub>
              <DropdownMenu.SubTrigger className={`${menuItemClass} flex items-center justify-between gap-4`}>
                {t('board.assignTo')}
                <span className="text-muted">▸</span>
              </DropdownMenu.SubTrigger>
              <DropdownMenu.Portal>
                <DropdownMenu.SubContent className={menuContentClass}>
                  {assignTargets.map((m) => (
                    <DropdownMenu.Item key={m.id} className={menuItemClass} onSelect={() => onAssign(m.id)}>
                      {m.name}
                    </DropdownMenu.Item>
                  ))}
                  <DropdownMenu.Separator className={menuSeparatorClass} />
                  <DropdownMenu.Item className={menuItemClass} onSelect={() => onAssign(null)}>
                    {t('board.unassign')}
                  </DropdownMenu.Item>
                </DropdownMenu.SubContent>
              </DropdownMenu.Portal>
            </DropdownMenu.Sub>
          )}

          {moveTargets.length > 0 && (
            <DropdownMenu.Sub>
              <DropdownMenu.SubTrigger className={`${menuItemClass} flex items-center justify-between gap-4`}>
                {t('board.moveToProject')}
                <span className="text-muted">▸</span>
              </DropdownMenu.SubTrigger>
              <DropdownMenu.Portal>
                <DropdownMenu.SubContent className={menuContentClass}>
                  {moveTargets.map((p) => (
                    <DropdownMenu.Item key={p.id} className={menuItemClass} onSelect={() => onMove(p.id)}>
                      {p.name}
                    </DropdownMenu.Item>
                  ))}
                </DropdownMenu.SubContent>
              </DropdownMenu.Portal>
            </DropdownMenu.Sub>
          )}

          <DropdownMenu.Item className={menuItemClass} onSelect={onViewHistory}>
            {t('board.viewHistory')}
          </DropdownMenu.Item>

          <DropdownMenu.Separator className={menuSeparatorClass} />

          <DropdownMenu.Item className={`${menuItemClass} text-red-600`} onSelect={onDelete}>
            {t('taskModal.deleteTask')}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}
