import { AnimatePresence, motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'

// The shortcut list: [keys, i18n label key].
const SHORTCUTS: { keys: string[]; label: string }[] = [
  { keys: ['g', 'd'], label: 'shortcuts.dashboard' },
  { keys: ['g', 'p'], label: 'shortcuts.projects' },
  { keys: ['g', 'c'], label: 'shortcuts.calendar' },
  { keys: ['g', 'f'], label: 'shortcuts.forum' },
  { keys: ['g', 'm'], label: 'shortcuts.marketplace' },
  { keys: ['g', 'h'], label: 'shortcuts.chat' },
  { keys: ['g', 'n'], label: 'shortcuts.notes' },
  { keys: ['/'], label: 'shortcuts.search' },
  { keys: ['?'], label: 'shortcuts.help' },
]

/** A modal listing the global keyboard shortcuts. */
export default function ShortcutsHelp({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation()

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4"
          onClick={onClose}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.15 }}
        >
          <motion.div
            className="w-full max-w-sm rounded-xl border border-border bg-surface p-6 shadow-elevated"
            onClick={(e) => e.stopPropagation()}
            initial={{ scale: 0.96, y: 8 }}
            animate={{ scale: 1, y: 0 }}
            exit={{ scale: 0.96, y: 8 }}
            transition={{ duration: 0.15 }}
          >
            <h2 className="mb-4 text-lg font-bold">{t('shortcuts.title')}</h2>
            <ul className="space-y-2">
              {SHORTCUTS.map((s) => (
                <li key={s.label} className="flex items-center justify-between text-sm">
                  <span className="text-muted">{t(s.label)}</span>
                  <span className="flex items-center gap-1">
                    {s.keys.map((k, i) => (
                      <kbd
                        key={i}
                        className="rounded-md border border-border bg-canvas px-2 py-0.5 font-mono text-xs font-semibold text-foreground shadow-soft"
                      >
                        {k}
                      </kbd>
                    ))}
                  </span>
                </li>
              ))}
            </ul>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
