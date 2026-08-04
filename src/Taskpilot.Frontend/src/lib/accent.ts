/**
 * User-selectable accent colour. The whole app is built on CSS custom properties, so an accent
 * is just a set of variable overrides applied to <html>. The `indigo` default is the pristine
 * value baked into index.css (no inline overrides), so picking it clears any override.
 *
 * Primary stays a readable colour for text/links; the two gradient stops carry the brightness
 * (white text sits on the gradient). primary-muted is a translucent tint that works on both
 * light and dark grounds, so a single override is theme-safe.
 */
export interface Accent {
  id: string
  /** CSS background for the picker swatch. */
  swatch: string
  vars: Record<string, string>
}

const OVERRIDDEN = [
  '--color-primary',
  '--color-primary-hover',
  '--color-primary-muted',
  '--color-gradient-from',
  '--color-gradient-to',
] as const

export const ACCENTS: Accent[] = [
  {
    id: 'indigo',
    swatch: 'linear-gradient(135deg,#6366f1,#8b5cf6)',
    vars: {}, // default — no overrides (uses index.css)
  },
  {
    id: 'blue',
    swatch: 'linear-gradient(135deg,#3b82f6,#06b6d4)',
    vars: {
      '--color-primary': '#2563eb', '--color-primary-hover': '#1d4ed8',
      '--color-primary-muted': 'rgb(37 99 235 / 0.14)',
      '--color-gradient-from': '#3b82f6', '--color-gradient-to': '#06b6d4',
    },
  },
  {
    id: 'emerald',
    swatch: 'linear-gradient(135deg,#10b981,#14b8a6)',
    vars: {
      '--color-primary': '#047857', '--color-primary-hover': '#065f46',
      '--color-primary-muted': 'rgb(16 185 129 / 0.14)',
      '--color-gradient-from': '#10b981', '--color-gradient-to': '#14b8a6',
    },
  },
  {
    id: 'rose',
    swatch: 'linear-gradient(135deg,#f43f5e,#ec4899)',
    vars: {
      '--color-primary': '#e11d48', '--color-primary-hover': '#be123c',
      '--color-primary-muted': 'rgb(244 63 94 / 0.14)',
      '--color-gradient-from': '#f43f5e', '--color-gradient-to': '#ec4899',
    },
  },
  {
    id: 'amber',
    swatch: 'linear-gradient(135deg,#f59e0b,#fb8c3c)',
    vars: {
      '--color-primary': '#b45309', '--color-primary-hover': '#92400e',
      '--color-primary-muted': 'rgb(245 158 11 / 0.16)',
      '--color-gradient-from': '#f59e0b', '--color-gradient-to': '#fb8c3c',
    },
  },
  {
    id: 'violet',
    swatch: 'linear-gradient(135deg,#8b5cf6,#d946ef)',
    vars: {
      '--color-primary': '#7c3aed', '--color-primary-hover': '#6d28d9',
      '--color-primary-muted': 'rgb(139 92 246 / 0.14)',
      '--color-gradient-from': '#8b5cf6', '--color-gradient-to': '#d946ef',
    },
  },
]

const KEY = 'accent'

export function getAccentId(): string {
  return localStorage.getItem(KEY) ?? 'indigo'
}

/** Applies an accent's variable overrides to <html> (or clears them for the default). */
function apply(id: string) {
  const root = document.documentElement
  const accent = ACCENTS.find((a) => a.id === id)
  if (!accent || accent.id === 'indigo') {
    OVERRIDDEN.forEach((v) => root.style.removeProperty(v))
    return
  }
  for (const [k, v] of Object.entries(accent.vars)) root.style.setProperty(k, v)
}

/** Persists and applies the chosen accent. */
export function setAccent(id: string) {
  localStorage.setItem(KEY, id)
  apply(id)
}

/** Applies the saved accent — call once at startup, before render, to avoid a flash. */
export function initAccent() {
  const id = localStorage.getItem(KEY)
  if (id && id !== 'indigo') apply(id)
}
