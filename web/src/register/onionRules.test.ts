import { describe, expect, it } from 'vitest'
import { MIN_ARRIVAL_GAP_MS, TIMEOUT_MS, createGame, move, rollMs, spawnIntervalMs, tick, type GameState, type Lane } from './onionRules'

/** Лук всегда в жёлобе 2 (правый верхний). */
const alwaysLane2 = () => 0.5

function step(state: GameState, ms: number, stepMs = 50): GameState {
  let s = state
  for (let t = 0; t < ms; t += stepMs) {
    s = tick(s, stepMs)
  }
  return s
}

/** Бот: всегда стоит у жёлоба лука, который докатится первым. */
function perfectBot(state: GameState): GameState {
  const next = [...state.onions].sort((a, b) => a.durationMs - a.progressMs - (b.durationMs - b.progressMs))[0]
  return next ? move(state, next.lane) : state
}

describe('onionRules: очки и стрик', () => {
  it('первый пойманный — +1, второй подряд — +2', () => {
    let s = move(createGame(224, alwaysLane2), 2)
    s = step(s, 3000)
    expect(s.score).toBe(1)
    expect(s.streak).toBe(1)

    s = step(s, 3000)
    expect(s.score).toBe(3)
    expect(s.streak).toBe(2)
  })

  it('промах обнуляет стрик, очки остаются', () => {
    let s = move(createGame(224, alwaysLane2), 2)
    s = step(s, 3000)
    s = move(s, 0)
    s = step(s, 3000)
    expect(s.streak).toBe(0)
    expect(s.score).toBe(1)
    expect(s.missed).toBeGreaterThanOrEqual(1)
  })

  it('после промаха следующий снова +1', () => {
    let s = move(createGame(224, alwaysLane2), 2)
    s = step(s, 3000)
    s = move(s, 0)
    s = step(s, 2000)
    expect(s.streak).toBe(0)
    const lastBefore = s.lastCatch?.id
    s = move(s, 2)
    // Ждём ровно первый пойманный после промаха.
    for (let t = 0; t < 10_000 && s.lastCatch?.id === lastBefore; t += 50) {
      s = tick(s, 50)
    }
    expect(s.lastCatch?.points).toBe(1)
  })
})

describe('onionRules: темп от стрика', () => {
  it('скорость растёт со стриком, но не быстрее 700 мс', () => {
    expect(rollMs(0)).toBe(3000)
    expect(rollMs(5)).toBe(2400)
    expect(rollMs(100)).toBe(700)
  })

  it('лук появляется всё чаще, но не больше ~4 на поле', () => {
    expect(spawnIntervalMs(0)).toBe(1800)
    expect(spawnIntervalMs(4)).toBeCloseTo(rollMs(4) * 0.5)
    expect(spawnIntervalMs(50)).toBe(rollMs(50) * 0.25)
  })

  it('два лука никогда не докатываются ближе 350 мс', () => {
    let s = createGame(224, Math.random)
    const arrivals: number[] = []
    for (let t = 0; t < 120_000 && !s.done; t += 20) {
      s = perfectBot(s)
      const before = s.onions.map((o) => o.id)
      s = tick(s, 20)
      if (s.done) {
        // На финальном тике поле очищается — это не докатившиеся луки.
        break
      }
      const after = new Set(s.onions.map((o) => o.id))
      before.filter((id) => !after.has(id)).forEach(() => arrivals.push(s.elapsedMs))
    }
    const gaps = arrivals.slice(1).map((t, i) => t - arrivals[i])
    // Шаг симуляции 20 мс — допускаем погрешность в один шаг.
    expect(Math.min(...gaps)).toBeGreaterThanOrEqual(MIN_ARRIVAL_GAP_MS - 20)
  })
})

describe('onionRules: конец игры', () => {
  it('идеальный бот набирает 224 очка', () => {
    let s = createGame(224, Math.random)
    for (let t = 0; t < TIMEOUT_MS && !s.done; t += 20) {
      s = tick(perfectBot(s), 20)
    }
    expect(s.done).toBe(true)
    expect(s.tiredOut).toBe(false)
    expect(s.score).toBeGreaterThanOrEqual(224)
  })

  it('через 5 минут засчитывается, даже без очков', () => {
    let s = move(createGame(224, alwaysLane2), 0)
    s = tick(s, TIMEOUT_MS)
    expect(s.done).toBe(true)
    expect(s.tiredOut).toBe(true)
  })

  it('после окончания ничего не меняется', () => {
    const s = tick(createGame(224, alwaysLane2), TIMEOUT_MS)
    expect(tick(s, 1000)).toBe(s)
    expect(move(s, 3 as Lane)).toBe(s)
  })
})
