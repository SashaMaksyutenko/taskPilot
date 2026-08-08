/** A stable, pleasant presence colour derived from a string (user id or name). */
export function colorFromString(seed: string): string {
  let hash = 0
  for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) | 0
  const hue = Math.abs(hash) % 360
  return `hsl(${hue} 70% 45%)`
}
