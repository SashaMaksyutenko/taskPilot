import { useState } from 'react'
import { useBranding } from '../hooks/useBranding'

/**
 * The organization's logo. Shows the admin-configured custom logo when one is set, and
 * falls back to a built-in image otherwise — or if the custom logo fails to load, so a
 * broken URL never leaves an empty space where the brand should be.
 */
export default function BrandLogo({
  className,
  /** Built-in image to show when there is no custom logo (or it fails to load). */
  fallback = '/logo-mark.svg',
}: {
  className?: string
  fallback?: string
}) {
  const { logoUrl } = useBranding()
  const [failed, setFailed] = useState(false)

  const src = logoUrl && !failed ? logoUrl : fallback
  return (
    <img
      src={src}
      alt=""
      className={className}
      // A bad custom-logo URL drops back to the built-in mark instead of a broken image.
      onError={() => setFailed(true)}
    />
  )
}
