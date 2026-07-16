import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Sparkles } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { chatbotService } from '../services/chatbotService'

/**
 * Pages that own the bottom-right corner with their own composer/send button — the
 * floating button would sit on top of it.
 */
const HIDDEN_ON = ['/assistant', '/chat']

/**
 * Floating shortcut to the AI assistant, shown on every authenticated page except those
 * that own the bottom-right corner. Hidden entirely when the assistant is not configured.
 */
export default function AssistantFab() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const [enabled, setEnabled] = useState(false)

  useEffect(() => {
    chatbotService.status().then((s) => setEnabled(s.enabled)).catch(() => setEnabled(false))
  }, [])

  if (!enabled || HIDDEN_ON.includes(location.pathname)) return null

  return (
    <button
      onClick={() => navigate('/assistant')}
      aria-label={t('assistant.fab')}
      title={t('assistant.fab')}
      className="group fixed bottom-6 right-6 z-40 flex items-center gap-2 rounded-full bg-primary py-3.5 pl-4 pr-5 font-semibold text-white shadow-elevated transition hover:-translate-y-0.5 hover:bg-primary-hover"
    >
      <Sparkles className="h-5 w-5" strokeWidth={2.2} />
      <span className="hidden text-sm sm:inline">{t('assistant.fab')}</span>
    </button>
  )
}
