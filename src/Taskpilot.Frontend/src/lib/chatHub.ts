import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/**
 * Builds a SignalR connection to the chat hub.
 * The access token is read from localStorage on each (re)connect, and is sent
 * as the "access_token" query param for the WebSocket handshake.
 */
export function createChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseURL}/hubs/chat`, {
      accessTokenFactory: () => localStorage.getItem('accessToken') ?? '',
    })
    .withAutomaticReconnect()
    .build()
}
