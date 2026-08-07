/**
 * Human-friendly task references, e.g. "TP-142". The letter key is derived from the project name
 * (cosmetic — the backend resolves a reference by its number within the linked project), so it
 * needs no server round-trip.
 */

/** A short uppercase key from a project name (2 letters), e.g. "TaskPilot" → "TA", "tprpoject" → "TP". */
export function projectKey(name: string): string {
  const letters = (name || '').replace(/[^A-Za-z]/g, '').toUpperCase()
  return letters.slice(0, 2) || 'PR'
}

/** The reference shown for a task, e.g. "TA-142". */
export function taskRef(projectName: string, number: number): string {
  return `${projectKey(projectName)}-${number}`
}
