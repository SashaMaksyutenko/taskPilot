import LottieImport from 'lottie-react'
import type { ReactNode } from 'react'
import emptyAnimation from '../assets/lottie/empty.json'

// CJS/ESM interop: under Vite the default import can arrive as the module
// namespace object, so unwrap `.default` to get the actual component.
const Lottie = ((LottieImport as unknown as { default?: typeof LottieImport }).default ??
  LottieImport) as typeof LottieImport

/**
 * A friendly empty-state: a looping Lottie animation above a message. Used where a
 * list has no items yet. Honors reduced-motion by pausing the animation.
 */
export default function EmptyState({ message, children }: { message: string; children?: ReactNode }) {
  const reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia?.('(prefers-reduced-motion: reduce)').matches

  return (
    <div className="flex flex-col items-center justify-center gap-2 py-10 text-center">
      <Lottie
        animationData={emptyAnimation}
        loop={!reduceMotion}
        autoplay={!reduceMotion}
        style={{ width: 120, height: 120 }}
      />
      <p className="text-sm text-muted">{message}</p>
      {children}
    </div>
  )
}
