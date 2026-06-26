import * as ContextMenu from '@radix-ui/react-context-menu'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ROLES } from '../types/admin'
import { menuContentClass as contentClass, menuItemClass as itemClass, menuSeparatorClass as separatorClass } from './contextMenuStyles'

/**
 * Right-click context menu for a user row in the admin table: view profile,
 * change role and ban/unban. Wraps the row via Radix's ContextMenu.Trigger.
 */
export default function UserContextMenu({
  children,
  isActive,
  canModerate,
  onViewProfile,
  onChangeRole,
  onToggleBan,
}: {
  children: ReactNode
  isActive: boolean
  canModerate: boolean
  onViewProfile: () => void
  onChangeRole: (role: string) => void
  onToggleBan: () => void
}) {
  const { t } = useTranslation()

  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger asChild>{children}</ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Content className={contentClass}>
          <ContextMenu.Item className={itemClass} onSelect={onViewProfile}>
            {t('admin.viewProfile')}
          </ContextMenu.Item>

          <ContextMenu.Sub>
            <ContextMenu.SubTrigger className={`${itemClass} flex items-center justify-between gap-4`}>
              {t('admin.changeRole')}
              <span className="text-slate-400">▸</span>
            </ContextMenu.SubTrigger>
            <ContextMenu.Portal>
              <ContextMenu.SubContent className={contentClass}>
                {ROLES.map((r) => (
                  <ContextMenu.Item key={r} className={itemClass} onSelect={() => onChangeRole(r)}>
                    {t(`admin.roles.${r}`, r)}
                  </ContextMenu.Item>
                ))}
              </ContextMenu.SubContent>
            </ContextMenu.Portal>
          </ContextMenu.Sub>

          {/* Ban/unban — hidden for the current admin's own row */}
          {canModerate && (
            <>
              <ContextMenu.Separator className={separatorClass} />
              <ContextMenu.Item className={`${itemClass} text-red-600`} onSelect={onToggleBan}>
                {isActive ? t('admin.ban') : t('admin.unban')}
              </ContextMenu.Item>
            </>
          )}
        </ContextMenu.Content>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  )
}
