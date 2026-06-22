import { useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import Navbar from '../components/Navbar'
import { createChatConnection } from '../lib/chatHub'
import { chatService } from '../services/chatService'
import type { Conversation, Message } from '../types/chat'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

/**
 * Chat page: a list of conversations on the left and the selected conversation's
 * messages on the right. Sending uses REST; incoming messages arrive in real time
 * over the SignalR hub.
 */
export default function ChatPage() {
  const dispatch = useAppDispatch()
  const currentUser = useAppSelector((s) => s.auth.user)

  const [conversations, setConversations] = useState<Conversation[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [messages, setMessages] = useState<Message[]>([])
  const [text, setText] = useState('')
  const [otherUserId, setOtherUserId] = useState('')

  const connectionRef = useRef<HubConnection | null>(null)
  // Ref mirror of selectedId so the SignalR callback always sees the latest value.
  const selectedIdRef = useRef<string | null>(null)
  const bottomRef = useRef<HTMLDivElement | null>(null)

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

  const startDirect = async () => {
    const id = otherUserId.trim()
    if (!id) return
    try {
      const conv = await chatService.startDirect(id)
      setOtherUserId('')
      setConversations((prev) => (prev.some((c) => c.id === conv.id) ? prev : [conv, ...prev]))
      selectConversation(conv.id)
    } catch {
      // ignore (e.g. invalid id)
    }
  }

  // Title to show for a conversation in the list.
  const conversationTitle = (conv: Conversation): string => {
    if (conv.type === 'Group') return conv.name ?? 'Group'
    const other = conv.participants.find((p) => p.userId !== currentUser?.id)
    return other?.name ?? 'Direct chat'
  }

  return (
    <div className="flex h-screen flex-col bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <div className="flex min-h-0 flex-1">
        {/* Sidebar: conversations */}
        <aside className="flex w-72 flex-col border-r border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800">
          <div className="border-b border-slate-200 p-4 dark:border-slate-700">
            <span className="font-bold">Chats</span>
          </div>

          {/* Start a direct chat by user id (user search comes later) */}
          <div className="flex gap-2 border-b border-slate-200 p-3 dark:border-slate-700">
            <input
              value={otherUserId}
              onChange={(e) => setOtherUserId(e.target.value)}
              placeholder="User id to chat with"
              className="min-w-0 flex-1 rounded border border-slate-300 px-2 py-1 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            <button
              onClick={startDirect}
              className="rounded bg-[#1E2A44] px-3 text-sm font-medium text-white"
            >
              +
            </button>
          </div>

          <div className="flex-1 overflow-y-auto">
            {conversations.length === 0 && (
              <p className="p-4 text-sm text-slate-400">No conversations yet.</p>
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
                <span className="ml-2 text-xs text-slate-400">{conv.type}</span>
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
                        <div className="whitespace-pre-wrap break-words">{m.content}</div>
                      </div>
                    </div>
                  )
                })}
                <div ref={bottomRef} />
              </div>

              <div className="flex gap-2 border-t border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-800">
                <input
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && send()}
                  placeholder="Type a message…"
                  className="flex-1 rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
                />
                <button
                  onClick={send}
                  className="rounded-lg bg-[#F6BE2C] px-5 font-semibold text-[#1E2A44] transition hover:brightness-95"
                >
                  Send
                </button>
              </div>
            </>
          ) : (
            <div className="flex flex-1 items-center justify-center text-slate-400">
              Select a conversation to start chatting
            </div>
          )}
        </main>
      </div>
    </div>
  )
}
