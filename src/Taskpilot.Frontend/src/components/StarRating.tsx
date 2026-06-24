/**
 * Five-star rating. Display-only by default; pass `onChange` to make it an input.
 */
export default function StarRating({
  value,
  onChange,
}: {
  value: number
  onChange?: (n: number) => void
}) {
  return (
    <span className="text-lg leading-none">
      {[1, 2, 3, 4, 5].map((n) => {
        const cls = n <= value ? 'text-amber-400' : 'text-slate-300 dark:text-slate-600'
        return onChange ? (
          <button key={n} type="button" onClick={() => onChange(n)} className="px-0.5" aria-label={`${n} stars`}>
            <span className={cls}>★</span>
          </button>
        ) : (
          <span key={n} className={cls}>
            ★
          </span>
        )
      })}
    </span>
  )
}
