import { describe, expect, it } from 'vitest'
import type { Task } from '../types/project'
import { FILTER_ME, FILTER_UNASSIGNED, hasActiveFilters, matchesBoardFilters } from './boardFilters'

const task = (over: Partial<Task>): Task =>
  ({
    id: 't', projectId: 'p', title: 'T', description: null, status: 'Backlog', priority: 'Medium',
    assigneeId: null, assigneeName: null, creatorId: 'c', creatorName: 'C', parentTaskId: null,
    deadline: null, createdAt: '', updatedAt: null, completedAt: null, tags: [], timeSpentSeconds: 0,
    timerStartedAt: null, sprintId: null, epicId: null, estimate: null, recurrence: 'None', recurrenceInterval: 1,
    ...over,
  }) as Task

const none = { tags: [], assignee: '', priority: '' }

describe('matchesBoardFilters', () => {
  it('passes everything when no filter is set', () => {
    expect(matchesBoardFilters(task({}), none)).toBe(true)
  })

  it('filters by priority', () => {
    expect(matchesBoardFilters(task({ priority: 'High' }), { ...none, priority: 'High' })).toBe(true)
    expect(matchesBoardFilters(task({ priority: 'Low' }), { ...none, priority: 'High' })).toBe(false)
  })

  it('filters by any matching tag', () => {
    expect(matchesBoardFilters(task({ tags: ['bug', 'ui'] }), { ...none, tags: ['ui'] })).toBe(true)
    expect(matchesBoardFilters(task({ tags: ['bug'] }), { ...none, tags: ['ui'] })).toBe(false)
  })

  it('handles the me / unassigned / specific-user assignee filters', () => {
    expect(matchesBoardFilters(task({ assigneeId: 'u1' }), { ...none, assignee: FILTER_ME }, 'u1')).toBe(true)
    expect(matchesBoardFilters(task({ assigneeId: 'u2' }), { ...none, assignee: FILTER_ME }, 'u1')).toBe(false)
    expect(matchesBoardFilters(task({ assigneeId: null }), { ...none, assignee: FILTER_UNASSIGNED })).toBe(true)
    expect(matchesBoardFilters(task({ assigneeId: 'u2' }), { ...none, assignee: FILTER_UNASSIGNED })).toBe(false)
    expect(matchesBoardFilters(task({ assigneeId: 'u2' }), { ...none, assignee: 'u2' })).toBe(true)
  })

  it('combines filters with AND', () => {
    const t = task({ priority: 'High', assigneeId: 'u1', tags: ['ui'] })
    expect(matchesBoardFilters(t, { tags: ['ui'], assignee: 'u1', priority: 'High' })).toBe(true)
    expect(matchesBoardFilters(t, { tags: ['ui'], assignee: 'u1', priority: 'Low' })).toBe(false)
  })

  it('reports active filters', () => {
    expect(hasActiveFilters(none)).toBe(false)
    expect(hasActiveFilters({ ...none, priority: 'High' })).toBe(true)
  })
})
