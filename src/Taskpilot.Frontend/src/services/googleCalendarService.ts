import api from '../lib/api'

/** Whether Google Calendar sync is available (server-configured) and connected for the user. */
export interface GoogleCalendarStatus {
  configured: boolean
  connected: boolean
  lastSyncedUtc: string | null
}

/** Outcome of a push sync (TaskPilot → Google). */
export interface GoogleCalendarSyncResult {
  created: number
  updated: number
  total: number
}

/** Outcome of a pull sync (Google → TaskPilot): tasks rescheduled from moved events. */
export interface GoogleCalendarPullResult {
  rescheduled: number
  checked: number
}

/** Google Calendar sync (push phase): connect via OAuth, disconnect, status and on-demand sync. */
export const googleCalendarService = {
  status(): Promise<GoogleCalendarStatus> {
    return api.get<GoogleCalendarStatus>('/api/calendar/google/status').then((r) => r.data)
  },

  /** The Google consent URL to redirect to; redirectUri must match the one used on connect. */
  connectUrl(redirectUri: string): Promise<string> {
    return api
      .get<{ url: string }>('/api/calendar/google/connect-url', { params: { redirectUri } })
      .then((r) => r.data.url)
  },

  /** Completes the consent flow with the code Google returned. */
  connect(code: string, redirectUri: string): Promise<GoogleCalendarStatus> {
    return api
      .post<GoogleCalendarStatus>('/api/calendar/google/connect', { code, redirectUri })
      .then((r) => r.data)
  },

  /** Pushes the user's deadline tasks to their Google Calendar. */
  sync(): Promise<GoogleCalendarSyncResult> {
    return api.post<GoogleCalendarSyncResult>('/api/calendar/google/sync').then((r) => r.data)
  },

  /** Pulls moved events back: reschedules tasks whose Google event start changed. */
  pull(): Promise<GoogleCalendarPullResult> {
    return api.post<GoogleCalendarPullResult>('/api/calendar/google/pull').then((r) => r.data)
  },

  /** Disconnects Google Calendar and forgets the task↔event links. */
  disconnect(): Promise<void> {
    return api.delete('/api/calendar/google').then(() => undefined)
  },
}
