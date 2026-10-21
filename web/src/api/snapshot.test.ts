import { describe, expect, it } from 'vitest'
import { applySnapshot } from './snapshot'
import type { AppSnapshot } from './types'

function snapshot(serverTime: string, waitingCount = 0): AppSnapshot {
  return {
    registration: { state: 'Closed', openedAt: null, waitingCount, closedAt: null },
    leaderboard: [],
    serverTime,
  }
}

describe('applySnapshot', () => {
  it('принимает первый снапшот', () => {
    const next = snapshot('2026-11-04T18:00:00Z')
    expect(applySnapshot(null, next)).toBe(next)
  })

  it('берёт более новый снапшот', () => {
    const prev = snapshot('2026-11-04T18:00:00Z', 1)
    const next = snapshot('2026-11-04T18:00:01Z', 2)
    expect(applySnapshot(prev, next)).toBe(next)
  })

  it('отбрасывает снапшот, пришедший не по порядку', () => {
    const prev = snapshot('2026-11-04T18:00:05Z', 5)
    const stale = snapshot('2026-11-04T18:00:01Z', 1)
    expect(applySnapshot(prev, stale)).toBe(prev)
  })

  it('сравнивает время, а не строки (разные форматы смещения)', () => {
    const prev = snapshot('2026-11-04T18:00:05+00:00')
    const next = snapshot('2026-11-04T21:00:06+03:00')
    expect(applySnapshot(prev, next)).toBe(next)
  })
})
