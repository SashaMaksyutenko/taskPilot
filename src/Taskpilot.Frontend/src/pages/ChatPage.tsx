import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { HubConnection } from '@microsoft/signalr'
import AttachmentPreview from '../components/AttachmentPreview'
import Avatar from '../components/Avatar'
import MentionField from '../components/MentionField'
import MentionText from '../components/MentionText'
import MessageContextMenu from '../components/MessageContextMenu'
import Navbar from '../components/Navbar'
import { apiErrorMessage } from '../lib/apiError'
import { createChatConnection } from '../lib/chatHub'
import { chatService } from '../services/chatService'
import { fileService } from '../services/fileService'
import { userService, type UserSearchResult } from '../services/userService'
import type { Conversation, Message, ReactionUpdate } from '../types/chat'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

// Curated set of emojis offered by the reaction picker (shown in a scrollable grid).
const REACTION_EMOJIS = [
  '👍', '👎', '❤️', '🧡', '💛', '💚', '💙', '💜',
  '🔥', '🎉', '😂', '🤣', '😊', '😍', '🥰', '😎',
  '😮', '😢', '😭', '😡', '🤔', '🙄', '😴', '🤯',
  '🙏', '👏', '🙌', '🤝', '💪', '🫶', '👀', '🚀',
  '💯', '✅', '❌', '⭐', '🎯', '💡', '☕', '🍕', '🍺',
]

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
  const [sendError, setSendError] = useState('')
  const [search, setSearch] = useState('')
  const [results, setResults] = useState<UserSearchResult[]>([])
  // Message id whose emoji picker is currently open (only one at a time).
  const [pickerFor, setPickerFor] = useState<string | null>(null)
  // Message currently being edited inline, and its draft text.
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editText, setEditText] = useState('')
  // Ids of other participants currently typing in the open conversation.
  const [typingUserIds, setTypingUserIds] = useState<string[]>([])

  const connectionRef = useRef<HubConnection | null>(null)
  // Ref mirror of selectedId so the SignalR callback always sees the latest value.
  const selectedIdRef = useRef<string | null>(null)
  // Ref mirror of the current user so the once-registered SignalR handler stays current.
  const currentUserRef = useRef(currentUser)
  currentUserRef.current = currentUser
  const bottomRef = useRef<HTMLDivElement | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  // Typing indicator bookkeeping: per-user auto-expiry timers, last StartTyping
  // send time (throttle), and the debounced StopTyping timer.
  const typingTimersRef = useRef<Record<string, ReturnType<typeof setTimeout>>>({})
  const lastTypingSentRef = useRef(0)
  const stopTypingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

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
        // We are looking at this conversation, so keep it marked as read.
        chatService.markRead(msg.conversationId).catch(() => {})
      } else if (msg.senderId !== currentUserRef.current?.id) {
        // A message arrived in a conversation we are not viewing: bump its badge.
        setConversations((prev) =>
          prev.map((c) => (c.id === msg.conversationId ? { ...c, unreadCount: c.unreadCount + 1 } : c)),
        )
      }
    })
    connection.on('ReceiveReaction', (upd: ReactionUpdate) => {
      setMessages((prev) => prev.map((m) => (m.id === upd.messageId ? { ...m, reactions: upd.reactions } : m)))
    })
    connection.on('MessageEdited', (msg: Message) => {
      setMessages((prev) => prev.map((m) => (m.id === msg.id ? msg : m)))
    })
    connection.on('UserTyping', (p: { conversationId: string; userId: string }) => {
      if (p.conversationId !== selectedIdRef.current || p.userId === currentUserRef.current?.id) return
      setTypingUserIds((prev) => (prev.includes(p.userId) ? prev : [...prev, p.userId]))
      const timers = typingTimersRef.current
      if (timers[p.userId]) clearTimeout(timers[p.userId])
      // Drop the indicator if no fresh "typing" arrives shortly.
      timers[p.userId] = setTimeout(() => {
        setTypingUserIds((prev) => prev.filter((id) => id !== p.userId))
        delete timers[p.userId]
      }, 4000)
    })
    connection.on('UserStoppedTyping', (p: { conversationId: string; userId: string }) => {
      if (p.conversationId !== selectedIdRef.current) return
      setTypingUserIds((prev) => prev.filter((id) => id !== p.userId))
      const timers = typingTimersRef.current
      if (timers[p.userId]) {
        clearTimeout(timers[p.userId])
        delete timers[p.userId]
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
    setTypingUserIds([]) // typing state is per-conversation
    setMessages(await chatService.getMessages(id))
    if (connection) await connection.invoke('JoinConversation', id).catch(() => {})
    // Opening a conversation clears its unread badge.
    chatService.markRead(id).catch(() => {})
    setConversations((prev) => prev.map((c) => (c.id === id ? { ...c, unreadCount: 0 } : c)))
  }

  // Tell the other members we are typing, throttled, with a debounced "stopped".
  const notifyTyping = () => {
    const connection = connectionRef.current
    const id = selectedIdRef.current
    if (!connection || !id) return
    const now = Date.now()
    if (now - lastTypingSentRef.current > 2000) {
      lastTypingSentRef.current = now
      connection.invoke('StartTyping', id).catch(() => {})
    }
    if (stopTypingTimerRef.current) clearTimeout(stopTypingTimerRef.current)
    stopTypingTimerRef.current = setTimeout(() => {
      connection.invoke('StopTyping', id).catch(() => {})
      lastTypingSentRef.current = 0
    }, 2500)
  }

  const stopTyping = () => {
    const connection = connectionRef.current
    const id = selectedIdRef.current
    if (stopTypingTimerRef.current) {
      clearTimeout(stopTypingTimerRef.current)
      stopTypingTimerRef.current = null
    }
    lastTypingSentRef.current = 0
    if (connection && id) connection.invoke('StopTyping', id).catch(() => {})
  }

  const send = async () => {
    const content = text.trim()
    if (!selectedId || !content) return
    setText('')
    setSendError('')
    stopTyping()
    // The message is echoed back to us via the hub, so we do not append it here.
    try {
      await chatService.sendMessage(selectedId, content)
    } catch (e) {
      setText(content) // keep the unsent text so it isn't lost
      setSendError(apiErrorMessage(e))
    }
  }

  const deleteMessage = async (id: string) => {
    await chatService.deleteMessage(id).catch(() => {})
    setMessages((prev) => prev.filter((m) => m.id !== id))
  }

  const toggleReaction = async (messageId: string, emoji: string) => {
    const upd = await chatService.react(messageId, emoji).catch(() => null)
    if (upd) setMessages((prev) => prev.map((m) => (m.id === messageId ? { ...m, reactions: upd.reactions } : m)))
  }

  const togglePin = async (messageId: string) => {
    const updated = await chatService.togglePin(messageId).catch(() => null)
    if (updated) setMessages((prev) => prev.map((m) => (m.id === messageId ? updated : m)))
  }

  const startEdit = (m: Message) => {
    setEditingId(m.id)
    setEditText(m.content)
  }

  const cancelEdit = () => {
    setEditingId(null)
    setEditText('')
  }

  const saveEdit = async (messageId: string) => {
    const content = editText.trim()
    if (!content) return
    const updated = await chatService.editMessage(messageId, content).catch(() => null)
    if (updated) setMessages((prev) => prev.map((m) => (m.id === messageId ? updated : m)))
    cancelEdit()
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

  // Avatar of the other participant for a direct conversation (null for groups).
  const conversationAvatar = (conv: Conversation): string | null => {
    if (conv.type === 'Group') return null
    return conv.participants.find((p) => p.userId !== currentUser?.id)?.avatarUrl ?? null
  }

  // @mention candidates: the current conversation's other participants.
  const mentionCandidates = (conversations.find((c) => c.id === selectedId)?.participants ?? [])
    .filter((p) => p.userId !== currentUser?.id)
    .map((p) => ({ id: p.userId, name: p.name, avatarUrl: p.avatarUrl }))

  // "X is typing…" label for the open conversation.
  const typingLabel = (): string | null => {
    if (typingUserIds.length === 0) return null
    if (typingUserIds.length > 1) return t('chat.typingMany')
    const participants = conversations.find((c) => c.id === selectedId)?.participants ?? []
    const name = participants.find((p) => p.userId === typingUserIds[0])?.name ?? '…'
    return t('chat.typing', { name })
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
                      className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-slate-50 dark:hover:bg-slate-700"
                    >
                      <Avatar name={u.name} src={u.avatarUrl} size={28} />
                      <span className="min-w-0 flex-1 truncate">
                        <span className="font-medium">{u.name}</span>
                        {u.title && <span className="ml-2 text-xs text-slate-400">{u.title}</span>}
                      </span>
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
                className={`flex w-full items-center gap-2 border-b border-slate-100 px-4 py-3 text-left text-sm hover:bg-slate-50 dark:border-slate-700/60 dark:hover:bg-slate-700/50 ${
                  selectedId === conv.id ? 'bg-slate-100 font-semibold dark:bg-slate-700' : ''
                }`}
              >
                <Avatar name={conversationTitle(conv)} src={conversationAvatar(conv)} size={32} />
                <span className={`min-w-0 flex-1 truncate ${conv.unreadCount > 0 && selectedId !== conv.id ? 'font-semibold' : ''}`}>
                  {conversationTitle(conv)}
                </span>
                {conv.unreadCount > 0 && selectedId !== conv.id ? (
                  <span className="flex-none rounded-full bg-[#F97316] px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
                    {conv.unreadCount > 99 ? '99+' : conv.unreadCount}
                  </span>
                ) : (
                  <span className="flex-none text-xs text-slate-400">{t(`chat.type.${conv.type}`, conv.type)}</span>
                )}
              </button>
            ))}
          </div>
        </aside>

        {/* Main: messages */}
        <main className="flex min-h-0 flex-1 flex-col">
          {selectedId ? (
            <>
              {/* Pinned messages strip */}
              {messages.some((m) => m.isPinned && !m.isDeleted) && (
                <div className="border-b border-slate-200 bg-amber-50 px-4 py-2 dark:border-slate-700 dark:bg-amber-950/20">
                  {messages
                    .filter((m) => m.isPinned && !m.isDeleted)
                    .map((m) => (
                      <div key={m.id} className="flex items-center gap-2 text-xs text-slate-600 dark:text-slate-300">
                        <span className="flex-none">📌</span>
                        <span className="min-w-0 flex-1 truncate">
                          <span className="font-semibold">{m.senderName}:</span> {m.content}
                        </span>
                        <button
                          onClick={() => togglePin(m.id)}
                          className="flex-none text-slate-400 hover:text-red-600"
                          title={t('chat.unpin')}
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                </div>
              )}

              <div className="flex-1 space-y-3 overflow-y-auto p-6">
                {messages.map((m) => {
                  const mine = m.senderId === currentUser?.id
                  return (
                    <div key={m.id} className={`flex items-end gap-2 ${mine ? 'justify-end' : 'justify-start'}`}>
                      {!mine && <Avatar name={m.senderName} src={m.senderAvatarUrl} size={28} />}
                      <MessageContextMenu
                        content={m.content}
                        isPinned={m.isPinned}
                        canDelete={mine}
                        canEdit={mine && !m.isDeleted}
                        onEdit={() => startEdit(m)}
                        onTogglePin={() => togglePin(m.id)}
                        onDelete={() => deleteMessage(m.id)}
                      >
                        <div
                          className={`max-w-md rounded-2xl px-4 py-2 ${
                            mine
                              ? 'bg-[#1E2A44] text-white'
                              : 'bg-white text-[#1E2A44] shadow dark:bg-slate-800 dark:text-slate-100'
                          }`}
                        >
                          {m.isPinned && (
                            <div className={`mb-0.5 flex items-center gap-1 text-[11px] font-semibold ${mine ? 'text-white/70' : 'text-[#F97316]'}`}>
                              📌 {t('chat.pinned')}
                            </div>
                          )}
                          {!mine && (
                            <div className="mb-0.5 text-xs font-semibold text-slate-500 dark:text-slate-400">
                              {m.senderName}
                            </div>
                          )}
                          {editingId === m.id ? (
                            <div>
                              <textarea
                                autoFocus
                                value={editText}
                                onChange={(e) => setEditText(e.target.value)}
                                onKeyDown={(e) => {
                                  if (e.key === 'Enter' && !e.shiftKey) {
                                    e.preventDefault()
                                    saveEdit(m.id)
                                  } else if (e.key === 'Escape') {
                                    e.preventDefault()
                                    cancelEdit()
                                  }
                                }}
                                rows={2}
                                className="w-64 resize-none rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-[#F97316] dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
                              />
                              <div className={`mt-1 text-[11px] ${mine ? 'text-white/60' : 'text-slate-400'}`}>
                                {t('chat.editHint')}
                              </div>
                            </div>
                          ) : (
                            m.content && (
                              <div className="whitespace-pre-wrap break-words">
                                <MentionText text={m.content} />
                                {m.editedAt && (
                                  <span className={`ml-1 text-[11px] italic ${mine ? 'text-white/50' : 'text-slate-400'}`}>
                                    ({t('chat.edited')})
                                  </span>
                                )}
                              </div>
                            )
                          )}
                          {m.fileId && (
                            <AttachmentPreview
                              fileId={m.fileId}
                              fileName={m.fileName}
                              contentType={m.fileContentType}
                              mine={mine}
                              onDownload={downloadAttachment}
                            />
                          )}
                          {/* Reaction chips + picker */}
                          <div className="mt-1 flex flex-wrap items-center gap-1">
                            {m.reactions.map((r) => (
                              <button
                                key={r.emoji}
                                onClick={() => toggleReaction(m.id, r.emoji)}
                                className={`flex items-center gap-0.5 rounded-full border px-1.5 py-0.5 text-xs transition ${
                                  r.mine
                                    ? 'border-[#F97316] bg-[#F97316]/15 text-[#F97316] dark:text-orange-300'
                                    : mine
                                      ? 'border-white/30 bg-white/10 text-white/90'
                                      : 'border-slate-300 bg-slate-100 text-slate-600 dark:border-slate-600 dark:bg-slate-700 dark:text-slate-200'
                                }`}
                              >
                                <span>{r.emoji}</span>
                                <span>{r.count}</span>
                              </button>
                            ))}
                            <div className="relative">
                              <button
                                onClick={() => setPickerFor((p) => (p === m.id ? null : m.id))}
                                title="Add reaction"
                                className={`flex h-5 w-5 items-center justify-center rounded-full text-xs opacity-60 transition hover:opacity-100 ${
                                  mine ? 'text-white/80 hover:bg-white/10' : 'text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-700'
                                }`}
                              >
                                +
                              </button>
                              {pickerFor === m.id && (
                                <div className="absolute bottom-6 left-0 z-10 grid max-h-40 w-52 grid-cols-8 gap-0.5 overflow-y-auto rounded-xl border border-slate-200 bg-white p-1.5 shadow-lg dark:border-slate-600 dark:bg-slate-800">
                                  {REACTION_EMOJIS.map((e) => (
                                    <button
                                      key={e}
                                      onClick={() => {
                                        toggleReaction(m.id, e)
                                        setPickerFor(null)
                                      }}
                                      className="flex h-6 w-6 items-center justify-center rounded text-base leading-none transition hover:scale-125 hover:bg-slate-100 dark:hover:bg-slate-700"
                                    >
                                      {e}
                                    </button>
                                  ))}
                                </div>
                              )}
                            </div>
                          </div>
                        </div>
                      </MessageContextMenu>
                    </div>
                  )
                })}
                <div ref={bottomRef} />
              </div>

              {typingLabel() && (
                <div className="px-6 pb-1 text-xs italic text-slate-400 dark:text-slate-500">
                  {typingLabel()}
                </div>
              )}

              {sendError && (
                <div className="border-t border-red-200 bg-red-50 px-4 py-2 text-sm font-medium text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
                  {sendError}
                </div>
              )}

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
                <MentionField
                  value={text}
                  onChange={(v) => {
                    setText(v)
                    notifyTyping()
                  }}
                  candidates={mentionCandidates}
                  multiline={false}
                  onKeyDown={(e) => e.key === 'Enter' && send()}
                  placeholder={t('chat.messagePlaceholder')}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
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
