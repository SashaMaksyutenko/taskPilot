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
}
