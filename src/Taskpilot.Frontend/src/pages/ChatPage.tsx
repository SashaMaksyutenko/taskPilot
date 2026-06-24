import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { HubConnection } from '@microsoft/signalr'
import MessageContextMenu from '../components/MessageContextMenu'
import Navbar from '../components/Navbar'
import { createChatConnection } from '../lib/chatHub'
import { chatService } from '../services/chatService'
import { fileService } from '../services/fileService'
import { userService, type UserSearchResult } from '../services/userService'
import type { Conversation, Message } from '../types/chat'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

/**
 * Chat page: a list of conversations on the left and the selected conversation's
 * messages on the right. Sending uses REST; incoming messages arrive in real time
 * over the SignalR hub.
 */
export default function ChatPage() {
  const { t } = useTranslation()
  const dispatch = useAppDispatch()
  const currentUser = useAppSelector((s) => s.auth.user)

  const [conversations, setConversations] = useState<Conversation[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [messages, setMessages] = useState<Message[]>([])
  const [text, setText] = useState('')
  const [search, setSearch] = useState('')
  const [results, setResults] = useState<UserSearchResult[]>([])

  const connectionRef = useRef<HubConnection | null>(null)
  // Ref mirror of selectedId so the SignalR callback always sees the latest value.
  const selectedIdRef = useRef<string | null>(null)
  const bottomRef = useRef<HTMLDivElement | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  // Make sure we know who the current user is (to label direct chats).
  useEffect(() => {
    if (!currentUser) dispatch(fetchMe())
  }, [currentUser, dispatch])

  // Load conversations and open the realtime connection once.
  useEffect(() => {
    chatService.getConversations().then(setConversations).catch(() => {})

    const connection = createChatConnection()
    connectionRef.current = connection
    connection.on('ReceiveMessage', (msg: Message) => {
      if (msg.conversationId === selectedIdRef.current) {
        setMessages((prev) => [...prev, msg])
      }
    })
    connection.start().catch(() => {})

    return () => {
      connection.stop()
    }
  }, [])

  // Auto-scroll to the newest message.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const selectConversation = async (id: string) => {
    const connection = connectionRef.current
    if (selectedIdRef.current && connection) {
      await connection.invoke('LeaveConversation', selectedIdRef.current).catch(() => {})
    }
    selectedIdRef.current = id
    setSelectedId(id)
    setMessages(await chatService.getMessages(id))
    if (connection) await connection.invoke('JoinConversation', id).catch(() => {})
  }

  const send = async () => {
    const content = text.trim()
    if (!selectedId || !content) return
    setText('')
    // The message is echoed back to us via the hub, so we do not append it here.
    await chatService.sendMessage(selectedId, content).catch(() => {})
  }

  const deleteMessage = async (id: string) => {
    await chatService.deleteMessage(id).catch(() => {})
    setMessages((prev) => prev.filter((m) => m.id !== id))
  }

  // Upload the chosen file, then send it as a message attachment.
  const attachFile = async (file: File) => {
    if (!selectedId) return
    const uploaded = await fileService.upload(file).catch(() => null)
    if (uploaded) await chatService.sendMessage(selectedId, '', uploaded.id).catch(() => {})
  }

  // Download an attachment (authenticated) and trigger a save dialog.
  const downloadAttachment = async (fileId: string, fileName: string) => {
    const blob = await fileService.download(fileId).catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = fileName
    a.click()
    URL.revokeObjectURL(url)
  }

  // Debounced user search: query the backend a short moment after typing stops.
  useEffect(() => {
    const term = search.trim()
    if (term.length < 2) {
      setResults([])
      return
    }
    const handle = setTimeout(() => {
      userService.searchUsers(term).then(setResults).catch(() => setResults([]))
    }, 300)
    return () => clearTimeout(handle)
  }, [search])

  const startDirect = async (userId: string) => {
    setSearch('')
    setResults([])
    try {
      const conv = await chatService.startDirect(userId)
      setConversations((prev) => (prev.some((c) => c.id === conv.id) ? prev : [conv, ...prev]))
      selectConversation(conv.id)
    } catch {
      // ignore (e.g. invalid id)
    }
  }

  // Title to show for a conversation in the list.
  const conversationTitle = (conv: Conversation): string => {
    if (conv.type === 'Group') return conv.name ?? t('chat.group')
    const other = conv.participants.find((p) => p.userId !== currentUser?.id)
    return other?.name ?? t('chat.directChat')
  }

  return (
    <div className="flex h-screen flex-col bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <div className="flex min-h-0 flex-1">
        {/* Sidebar: conversations */}
        <aside className="flex w-72 flex-col border-r border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800">
          <div className="border-b border-slate-200 p-4 dark:border-slate-700">
            <span className="font-bold">{t('chat.title')}</span>
          </div>

          {/* Start a direct chat by searching for a user by name or email */}
          <div className="relative border-b border-slate-200 p-3 dark:border-slate-700">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('chat.searchPlaceholder')}
              className="w-full rounded border border-slate-300 px-2 py-1 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            {results.length > 0 && (
              <ul className="absolute left-3 right-3 top-full z-20 -mt-1 max-h-64 overflow-y-auto rounded-b border border-t-0 border-slate-300 bg-white shadow-lg dark:border-slate-600 dark:bg-slate-800">
                {results.map((u) => (
                  <li key={u.id}>
                    <button
                      onClick={() => startDirect(u.id)}
                      className="block w-full px-3 py-2 text-left text-sm hover:bg-slate-50 dark:hover:bg-slate-700"
                    >
                      <span className="font-medium">{u.name}</span>
                      {u.title && (
                        <span className="ml-2 text-xs text-slate-400">{u.title}</span>
                      )}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="flex-1 overflow-y-auto">
            {conversations.length === 0 && (
              <p className="p-4 text-sm text-slate-400">{t('chat.noConversations')}</p>
            )}
            {conversations.map((conv) => (
              <button
                key={conv.id}
                onClick={() => selectConversation(conv.id)}
                className={`block w-full border-b border-slate-100 px-4 py-3 text-left text-sm hover:bg-slate-50 dark:border-slate-700/60 dark:hover:bg-slate-700/50 ${
                  selectedId === conv.id ? 'bg-slate-100 font-semibold dark:bg-slate-700' : ''
                }`}
              >
                {conversationTitle(conv)}
                <span className="ml-2 text-xs text-slate-400">{t(`chat.type.${conv.type}`, conv.type)}</span>
              </button>
            ))}
          </div>
        </aside>

        {/* Main: messages */}
        <main className="flex min-h-0 flex-1 flex-col">
          {selectedId ? (
            <>
              <div className="flex-1 space-y-3 overflow-y-auto p-6">
                {messages.map((m) => {
                  const mine = m.senderId === currentUser?.id
                  return (
                    <div key={m.id} className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
                      <MessageContextMenu
                        content={m.content}
                        canDelete={mine}
                        onDelete={() => deleteMessage(m.id)}
                      >
                        <div
                          className={`max-w-md rounded-2xl px-4 py-2 ${
                            mine
                              ? 'bg-[#1E2A44] text-white'
                              : 'bg-white text-[#1E2A44] shadow dark:bg-slate-800 dark:text-slate-100'
                          }`}
                        >
                          {!mine && (
                            <div className="mb-0.5 text-xs font-semibold text-slate-500 dark:text-slate-400">
                              {m.senderName}
                            </div>
                          )}
                          {m.content && <div className="whitespace-pre-wrap break-words">{m.content}</div>}
                          {m.fileId && (
                            <button
                              onClick={() => downloadAttachment(m.fileId!, m.fileName ?? 'file')}
                              className={`mt-1 flex items-center gap-1 text-sm underline ${mine ? 'text-white/90' : 'text-[#1E2A44] dark:text-slate-100'}`}
                            >
                              📎 {m.fileName}
                            </button>
                          )}
                        </div>
                      </MessageContextMenu>
                    </div>
                  )
                })}
                <div ref={bottomRef} />
              </div>

              <div className="flex items-center gap-2 border-t border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-800">
                {/* Attach a file */}
                <input
                  ref={fileInputRef}
                  type="file"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0]
                    if (f) attachFile(f)
                    e.target.value = '' // allow re-selecting the same file
                  }}
                />
                <button
                  onClick={() => fileInputRef.current?.click()}
                  title="Attach a file"
                  className="rounded-lg px-2 py-2 text-lg text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700"
                >
                  📎
                </button>
                <input
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && send()}
                  placeholder={t('chat.messagePlaceholder')}
                  className="flex-1 rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
                />
                <button
                  onClick={send}
                  className="rounded-lg bg-[#F6BE2C] px-5 font-semibold text-[#1E2A44] transition hover:brightness-95"
                >
                  {t('chat.send')}
                </button>
              </div>
            </>
          ) : (
            <div className="flex flex-1 items-center justify-center text-slate-400">
              {t('chat.selectConversation')}
            </div>
          )}
        </main>
      </div>
    </div>
  )
}
