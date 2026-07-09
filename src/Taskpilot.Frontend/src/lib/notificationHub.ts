import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { tokenStorage } from './tokenStorage'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/**
 * Builds a SignalR connection to the notification hub.
 * The access token is read from localStorage on each (re)connect, and is sent
 * as the "access_token" query param for the WebSocket handshake.
 *
 * Receive event: "ReceiveNotification" with an AppNotification payload.
 */
export function createNotificationConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseURL}/hubs/notifications`, {
      accessTokenFactory: () => tokenStorage.getAccess() ?? '',
    })
    .withAutomaticReconnect()
    .build()
}
