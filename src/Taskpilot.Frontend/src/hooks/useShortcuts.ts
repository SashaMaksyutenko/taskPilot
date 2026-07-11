import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'

// "g" then one of these keys navigates to the matching route.
const GO_TO: Record<string, string> = {
  d: '/',
  p: '/projects',
  c: '/calendar',
  f: '/forum',
  m: '/marketplace',
  h: '/chat',
  n: '/notes',
  s: '/search',
}

/**
 * Global keyboard shortcuts for power users. Registers a window keydown listener
 * that ignores events originating from text fields. Supports:
 *  - "g" then a letter → navigate (d/p/c/f/m/h/n/s)
 *  - "/" → jump to search
 *  - "?" → toggle the shortcuts help overlay
 *  - Esc → close the help overlay
 * Returns the help overlay's open state so a host can render it.
 */
export function useShortcuts() {
  const navigate = useNavigate()
  const [helpOpen, setHelpOpen] = useState(false)
  // Whether the previous key was "g" (start of a two-key navigation sequence).
  const awaitingG = useRef(false)
  const gTimer = useRef<number | null>(null)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setHelpOpen(false)
        return
      }

      // Never hijack typing or modifier combos (copy/paste, browser shortcuts…).
      const el = e.target as HTMLElement | null
      const typing =
        !!el &&
        (el.tagName === 'INPUT' ||
          el.tagName === 'TEXTAREA' ||
          el.tagName === 'SELECT' ||
          el.isContentEditable)
      if (typing || e.metaKey || e.ctrlKey || e.altKey) return

      if (e.key === '?') {
        e.preventDefault()
        setHelpOpen((v) => !v)
        return
      }
      if (e.key === '/') {
        e.preventDefault()
        navigate('/search')
        return
      }

      // Second key of a "g <letter>" sequence.
      if (awaitingG.current) {
        awaitingG.current = false
        if (gTimer.current) clearTimeout(gTimer.current)
        const dest = GO_TO[e.key.toLowerCase()]
        if (dest) {
          e.preventDefault()
          navigate(dest)
        }
        return
      }

      // First key: arm the sequence for a short window.
      if (e.key.toLowerCase() === 'g') {
        awaitingG.current = true
        if (gTimer.current) clearTimeout(gTimer.current)
        gTimer.current = window.setTimeout(() => {
          awaitingG.current = false
        }, 1200)
      }
    }

    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('keydown', onKey)
      if (gTimer.current) clearTimeout(gTimer.current)
    }
  }, [navigate])

  return { helpOpen, setHelpOpen }
}
