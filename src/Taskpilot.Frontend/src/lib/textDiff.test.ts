import { describe, expect, it } from 'vitest'
import { diffText } from './textDiff'

describe('diffText', () => {
  it('detects an append at the end', () => {
    expect(diffText('hello', 'hello!')).toEqual({ index: 5, deleteCount: 0, insert: '!' })
  })

  it('detects an insert at the start', () => {
    expect(diffText('bc', 'abc')).toEqual({ index: 0, deleteCount: 0, insert: 'a' })
  })

  it('detects a single-character deletion in the middle', () => {
    expect(diffText('hello', 'helo')).toEqual({ index: 3, deleteCount: 1, insert: '' })
  })

  it('detects a replaced selection', () => {
    expect(diffText('the cat', 'the dog')).toEqual({ index: 4, deleteCount: 3, insert: 'dog' })
  })

  it('produces an empty change when nothing changed', () => {
    expect(diffText('same', 'same')).toEqual({ index: 4, deleteCount: 0, insert: '' })
  })

  it('round-trips: applying the change reproduces the new string', () => {
    const cases: [string, string][] = [
      ['', 'first draft'],
      ['first draft', 'first DRAFT done'],
      ['abcdef', 'abXYef'],
      ['keep the middle', 'keep middle'],
    ]
    for (const [oldStr, newStr] of cases) {
      const { index, deleteCount, insert } = diffText(oldStr, newStr)
      const applied = oldStr.slice(0, index) + insert + oldStr.slice(index + deleteCount)
      expect(applied).toBe(newStr)
    }
  })
})
