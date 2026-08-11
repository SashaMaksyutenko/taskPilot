import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { tokenStorage } from './tokenStorage'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/**
 * SignalR connection to the whiteboard hub: relays live cursors and in-flight drag positions, and
 * receives the server's authoritative note create/update/delete broadcasts.
 */
export function createWhiteboardConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseURL}/hubs/whiteboard`, {
      accessTokenFactory: () => tokenStorage.getAccess() ?? '',
    })
    .withAutomaticReconnect()
    .build()
}
