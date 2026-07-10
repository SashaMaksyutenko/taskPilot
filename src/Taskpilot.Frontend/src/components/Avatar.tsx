import { apiBaseUrl } from '../lib/api'

/**
 * Round user avatar. Shows the uploaded image when available, otherwise falls back
 * to the user's initials on a solid background. Relative avatar URLs (from the API)
 * are resolved against the backend base URL so they work in plain <img> tags.
 */
export default function Avatar({
  name,
  src,
  size = 36,
}: {
  name: string
  src?: string | null
  size?: number
}) {
  const url = src ? (src.startsWith('http') ? src : apiBaseUrl + src) : null

  const initials =
    name
      .trim()
      .split(/\s+/)
      .map((w) => w[0])
      .slice(0, 2)
      .join('')
      .toUpperCase() || '?'

  if (url) {
    return (
      <img
        src={url}
        alt={name}
        className="shrink-0 rounded-full object-cover"
        style={{ width: size, height: size }}
      />
    )
  }

  return (
    <div
      className="flex shrink-0 items-center justify-center rounded-full bg-primary font-semibold text-white"
      style={{ width: size, height: size, fontSize: Math.round(size * 0.4) }}
      aria-label={name}
    >
      {initials}
    </div>
  )
}
