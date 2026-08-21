import { Play } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import Button from '../ui/Button'
import { useDemo } from '../../hooks/useDemo'
import { useAppSelector } from '../../store/hooks'

/**
 * "Try the live demo" call-to-action for the auth pages. Renders nothing unless the server has the
 * no-signup demo turned on; when clicked it spins up a seeded throwaway account and signs the
 * visitor straight in. Shared by the login and register pages.
 */
export default function DemoButton() {
  const { t } = useTranslation()
  const { available, start } = useDemo()
  const status = useAppSelector((s) => s.auth.status)

  if (!available) return null

  return (
    <>
      <div className="my-4 flex items-center gap-3 text-xs text-muted">
        <span className="h-px flex-1 bg-border" />
        {t('auth.demoOr')}
        <span className="h-px flex-1 bg-border" />
      </div>
      <Button type="button" variant="secondary" onClick={start} disabled={status === 'loading'} className="w-full">
        <Play className="h-4 w-4" />
        {t('auth.tryDemo')}
      </Button>
      <p className="mt-1.5 text-center text-xs text-muted">{t('auth.demoHint')}</p>
    </>
  )
}
