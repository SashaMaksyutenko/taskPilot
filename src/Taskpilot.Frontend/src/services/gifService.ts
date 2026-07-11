import api from '../lib/api'

/** A single GIF result (mirrors GifDto). */
export interface Gif {
  id: string
  url: string
  previewUrl: string
  width: number
  height: number
}

/** Result of a GIF search (mirrors GifSearchResult). */
export interface GifSearchResult {
  enabled: boolean
  gifs: Gif[]
}

/** GIF search proxied through our API (provider key stays server-side). */
export const gifService = {
  /** Searches GIFs, or returns trending when the query is empty. */
  search(q?: string): Promise<GifSearchResult> {
    return api
      .get<GifSearchResult>('/api/gifs/search', { params: { q: q?.trim() || undefined, limit: 50 } })
      .then((r) => r.data)
  },
}

// A message whose whole content is a GIF URL (from our providers) is rendered as an image.
const GIF_URL = /^https?:\/\/\S+\.gif(\?\S*)?$/i
const GIF_HOST = /^https?:\/\/\S*(giphy\.com|tenor\.com|tenor\.googleapis\.com)\/\S+/i

/** True if a chat message's content is a single GIF URL that should render as an image. */
export function isGifMessage(content: string): boolean {
  const c = content.trim()
  if (c.includes(' ') || c.includes('\n')) return false
  return GIF_URL.test(c) || GIF_HOST.test(c)
}
