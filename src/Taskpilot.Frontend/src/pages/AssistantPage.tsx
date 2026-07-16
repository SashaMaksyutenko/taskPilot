import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import EmptyState from '../components/feedback/EmptyState'
import Markdown from '../components/Markdown'
import { chatbotService, type ChatBotMessage } from '../services/chatbotService'

/**
 * In-app AI assistant. Sends the running conversation to the backend (OpenAI) and
 * shows replies. When the assistant is not configured server-side, shows a note.
 */
export default function AssistantPage() {
  const { t } = useTranslation()
  const location = useLocation()
  const [enabled, setEnabled] = useState<boolean | null>(null)
  const [messages, setMessages] = useState<ChatBotMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)
  const endRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    chatbotService.status().then((s) => setEnabled(s.enabled)).catch(() => setEnabled(false))
  }, [])

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, sending])

  const sendText = async (raw: string) => {
    const text = raw.trim()
    if (!text || sending) return
    const next: ChatBotMessage[] = [...messages, { role: 'user', content: text }]
    setMessages(next)
    setInput('')
    setSending(true)
    try {
      const reply = await chatbotService.ask(next)
      setMessages((m) => [...m, { role: 'assistant', content: reply }])
    } catch {
      setMessages((m) => [...m, { role: 'assistant', content: t('assistant.error') }])
    } finally {
      setSending(false)
    }
  }

  const send = () => sendText(input)

  // A prompt handed in via navigation state (e.g. from a dashboard example) is sent once.
  useEffect(() => {
    const prompt = (location.state as { prompt?: string } | null)?.prompt
    if (prompt) {
      sendText(prompt)
      // Clear the state so a refresh or back-navigation doesn't resend it.
      window.history.replaceState({}, '')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const onKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  return (
    <div className="mx-auto flex h-[calc(100vh-64px)] max-w-3xl flex-col px-6 py-6">
        <h1 className="mb-1 text-2xl font-bold">{t('assistant.title')}</h1>
        <p className="mb-4 text-sm text-muted">{t('assistant.subtitle')}</p>

        {enabled === false ? (
          <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-700 dark:bg-amber-950/30 dark:text-amber-300">
            {t('assistant.disabled')}
          </div>
        ) : (
          <>
            <div className="flex-1 space-y-3 overflow-y-auto rounded-xl border border-border bg-surface p-4">
              {messages.length === 0 && <EmptyState message={t('assistant.empty')} />}
              {messages.map((m, i) => (
                <div key={i} className={m.role === 'user' ? 'flex justify-end' : 'flex justify-start'}>
                  <div
                    className={`max-w-[85%] rounded-2xl px-4 py-2 text-sm ${
                      m.role === 'user'
                        ? 'bg-primary text-white'
                        : 'bg-canvas text-primary'
                    }`}
                  >
                    {m.role === 'assistant' ? <Markdown>{m.content}</Markdown> : m.content}
                  </div>
                </div>
              ))}
              {sending && <p className="text-sm text-muted">{t('assistant.thinking')}</p>}
              <div ref={endRef} />
            </div>

            <div className="mt-3 flex gap-2">
              <textarea
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={onKeyDown}
                placeholder={t('assistant.placeholder')}
                rows={2}
                className="flex-1 resize-none rounded-lg border border-border bg-canvas px-3 py-2 outline-none focus:border-primary"
              />
              <button
                onClick={send}
                disabled={sending || !input.trim()}
                className="rounded-lg bg-primary px-5 font-semibold text-white transition hover:bg-primary-hover disabled:opacity-50"
              >
                {t('assistant.send')}
              </button>
            </div>
          </>
        )}
      </div>
  )
}
