/** A single contiguous edit: delete `deleteCount` chars at `index`, then insert `insert`. */
export interface TextChange {
  index: number
  deleteCount: number
  insert: string
}

/**
 * Reduces an old→new string edit to one contiguous change by trimming the common prefix and
 * suffix. A normal textarea edit (type, delete, paste, replace-selection) touches one region,
 * so this maps cleanly onto a Yjs Y.Text delete+insert without rewriting the whole document.
 */
export function diffText(oldStr: string, newStr: string): TextChange {
  let start = 0
  const minLen = Math.min(oldStr.length, newStr.length)
  while (start < minLen && oldStr[start] === newStr[start]) start++

  // Walk the suffix back, but never past the prefix we already matched.
  let endOld = oldStr.length
  let endNew = newStr.length
  while (endOld > start && endNew > start && oldStr[endOld - 1] === newStr[endNew - 1]) {
    endOld--
    endNew--
  }

  return { index: start, deleteCount: endOld - start, insert: newStr.slice(start, endNew) }
}
