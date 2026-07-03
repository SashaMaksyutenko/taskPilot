import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { useTranslation } from 'react-i18next'
import { menuContentClass, menuItemClass, menuSeparatorClass } from './contextMenuStyles'

const PRIORITIES = ['High', 'Medium', 'Low']

/**
 * Hover "⋮" three-dot menu for a task card — the same actions as the right-click
 * context menu, exposed via a visible button for discoverability.
 */
export default function TaskActionsDropdown({
  onEdit,
  onDuplicate,
  onChangePriority,
  onDelete,
}: {
  onEdit: () => void
  onDuplicate: () => void
  onChangePriority: (priority: string) => void
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
          className="rounded p-1 leading-none text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-700"
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

          <DropdownMenu.Sub>
            <DropdownMenu.SubTrigger className={`${menuItemClass} flex items-center justify-between gap-4`}>
              {t('taskModal.priority')}
              <span className="text-slate-400">▸</span>
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

          <DropdownMenu.Separator className={menuSeparatorClass} />

          <DropdownMenu.Item className={`${menuItemClass} text-red-600`} onSelect={onDelete}>
            {t('taskModal.deleteTask')}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}
