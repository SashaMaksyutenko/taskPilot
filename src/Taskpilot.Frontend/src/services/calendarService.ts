import api from '../lib/api'
import type { CalendarTask } from '../types/calendar'

/** REST calls for the calendar. */
export const calendarService = {
  /** Tasks with deadlines in [from, to] (date strings, YYYY-MM-DD). */
  getTasks(from: string, to: string): Promise<CalendarTask[]> {
    return api
      .get<CalendarTask[]>('/api/calendar/tasks', { params: { from, to } })
      .then((r) => r.data)
  },

  /** The current user's overdue tasks (past deadline, not Done). */
  getOverdue(): Promise<CalendarTask[]> {
    return api.get<CalendarTask[]>('/api/tasks/overdue').then((r) => r.data)
  },

  /** Downloads the user's deadline tasks as an iCalendar (.ics) blob. */
  exportIcs(): Promise<Blob> {
    return api.get('/api/calendar/export.ics', { responseType: 'blob' }).then((r) => r.data as Blob)
  },
}
