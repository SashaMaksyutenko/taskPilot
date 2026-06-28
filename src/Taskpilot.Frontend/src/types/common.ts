/** A page of results plus total count (mirrors backend PagedResult<T>). Shared across features. */
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
