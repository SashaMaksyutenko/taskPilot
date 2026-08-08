import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { tokenStorage } from './tokenStorage'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5025'

/**
 * Builds a SignalR connection to the collaboration hub, used for real-time CRDT editing.
 * The server only relays Yjs updates/awareness and stores snapshots; all merge logic is local.
 */
export function createCollabConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseURL}/hubs/collab`, {
      accessTokenFactory: () => tokenStorage.getAccess() ?? '',
    })
    .withAutomaticReconnect()
    .build()
}
