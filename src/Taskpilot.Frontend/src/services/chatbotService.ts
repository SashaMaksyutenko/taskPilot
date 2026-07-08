import api from '../lib/api'

export interface ChatBotMessage {
  role: 'user' | 'assistant'
  content: string
}

/** REST calls for the in-app AI assistant. */
export const chatbotService = {
  /** Whether the assistant is configured on the server. */
  status(): Promise<{ enabled: boolean }> {
    return api.get<{ enabled: boolean }>('/api/chatbot/status').then((r) => r.data)
  },

  /** Sends the running conversation and returns the assistant's reply. */
  ask(messages: ChatBotMessage[]): Promise<string> {
    return api.post<{ reply: string }>('/api/chatbot/ask', { messages }).then((r) => r.data.reply)
  },
}
