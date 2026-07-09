import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { tokenStorage } from './tokenStorage'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/**
 * Builds a SignalR connection to the task hub, used for real-time task comments.
 * The access token is read from localStorage for the WebSocket handshake.
 */
export function createTaskConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseURL}/hubs/tasks`, {
      accessTokenFactory: () => tokenStorage.getAccess() ?? '',
    })
    .withAutomaticReconnect()
    .build()
}
