import type { StandingDto } from './details'

export type StepView =
  | { kind: 'intro' }
  | { kind: 'player'; standing: StandingDto; fromLast: number }
  | { kind: 'podium'; top: StandingDto[] }
  | { kind: 'final' }

/**
 * Что показывать на шаге: 0 — вступление, 1..N — игроки с последнего места, затем пьедестал (топ-3) и финал.
 */
export function stepView(step: number, standings: StandingDto[]): StepView {
  const sorted = [...standings].sort((a, b) => a.place - b.place)
  const n = sorted.length
  if (step <= 0) {
    return { kind: 'intro' }
  }
  if (step <= n) {
    return { kind: 'player', standing: sorted[n - step], fromLast: step }
  }
  if (step === n + 1) {
    return { kind: 'podium', top: sorted.filter((s) => s.place <= 3) }
  }
  return { kind: 'final' }
}
