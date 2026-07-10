import { Fragment } from 'react'

// Splits on @token mentions (same shape the backend parses).
const SPLIT = /(@[A-Za-z0-9_]+)/g
const IS_MENTION = /^@[A-Za-z0-9_]+$/

/**
 * Renders text with @mentions highlighted. Plain text otherwise; preserves the
 * surrounding whitespace handling of the parent (use with whitespace-pre-wrap).
 */
export default function MentionText({ text }: { text: string }) {
  const parts = text.split(SPLIT)
  return (
    <>
      {parts.map((part, i) =>
        IS_MENTION.test(part) ? (
          <span key={i} className="font-semibold text-primary dark:text-sky-300">
            {part}
          </span>
        ) : (
          <Fragment key={i}>{part}</Fragment>
        ),
      )}
    </>
  )
}
