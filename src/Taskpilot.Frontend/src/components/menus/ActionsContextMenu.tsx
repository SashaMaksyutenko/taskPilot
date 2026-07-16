import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from '../contextMenuStyles'

/** One entry in a context menu. Use the string 'separator' for a divider. */
export type ContextAction = { label: string; onSelect: () => void; danger?: boolean } | 'separator'

/**
 * Generic right-click context menu: wrap any element and pass a list of actions.
 * Keeps every entity's menu visually consistent without a bespoke component each.
 */
export default function ActionsContextMenu({
  children,
  actions,
}: {
  children: ReactNode
  actions: ContextAction[]
}) {
  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          {actions.map((a, i) =>
            a === 'separator' ? (
              <ContextMenu.Separator key={i} className={separatorClass} />
            ) : (
              <ContextMenu.Item
                key={i}
                className={a.danger ? `${itemClass} text-red-600` : itemClass}
                onSelect={a.onSelect}
              >
                {a.label}
              </ContextMenu.Item>
            ),
          )}
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
