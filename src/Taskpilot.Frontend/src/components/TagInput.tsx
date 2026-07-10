import { useTranslation } from 'react-i18next'

/**
 * A small tag editor: chips with a remove button plus a text input.
 * Enter or comma adds a tag; Backspace on an empty input removes the last one.
 * Tags are de-duplicated case-insensitively and capped.
 */
export default function TagInput({
  tags,
  onChange,
  max = 5,
  maxLength = 30,
}: {
  tags: string[]
  onChange: (tags: string[]) => void
  max?: number
  maxLength?: number
}) {
  const { t } = useTranslation()

  const add = (raw: string) => {
    const tag = raw.trim().slice(0, maxLength)
    if (!tag) return
    if (tags.some((x) => x.toLowerCase() === tag.toLowerCase())) return
    if (tags.length >= max) return
    onChange([...tags, tag])
  }

  const remove = (tag: string) => onChange(tags.filter((x) => x !== tag))

  return (
    <div className="flex flex-wrap items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-2 py-1.5 dark:border-slate-600 dark:bg-slate-900">
      {tags.map((tag) => (
        <span
          key={tag}
          className="flex items-center gap-1 rounded-full bg-[#1E2A44]/10 px-2 py-0.5 text-xs font-medium text-[#1E2A44] dark:bg-slate-700 dark:text-slate-200"
        >
          {tag}
          <button type="button" onClick={() => remove(tag)} className="text-slate-400 hover:text-red-600">
            ✕
          </button>
        </span>
      ))}
      {tags.length < max && (
        <input
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ',') {
              e.preventDefault()
              add((e.target as HTMLInputElement).value)
              ;(e.target as HTMLInputElement).value = ''
            } else if (e.key === 'Backspace' && !(e.target as HTMLInputElement).value && tags.length > 0) {
              remove(tags[tags.length - 1])
            }
          }}
          onBlur={(e) => {
            add(e.target.value)
            e.target.value = ''
          }}
          placeholder={t('forum.tagsPlaceholder')}
          className="min-w-[8rem] flex-1 bg-transparent px-1 py-0.5 text-sm outline-none"
        />
      )}
    </div>
  )
}
