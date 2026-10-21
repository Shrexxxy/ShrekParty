import { describe, expect, it } from 'vitest'
import { ROUNDS, isCorrect, shuffleRound } from './annoyingCaptcha'

describe('annoyingCaptcha', () => {
  it('три раунда, в каждом 3 правильных и 6 неправильных', () => {
    expect(ROUNDS).toHaveLength(3)
    for (const round of ROUNDS) {
      expect(round.correct).toHaveLength(3)
      expect(round.wrong.length).toBeGreaterThanOrEqual(6)
    }
  })

  it('у каждой клетки своя картинка — одинаковых в сетке нет', () => {
    for (const round of ROUNDS) {
      const ids = [...round.correct, ...round.wrong].map((t) => t.id)
      expect(new Set(ids).size).toBe(ids.length)
    }
  })

  it('перемешанная сетка — 9 клеток, ровно 3 правильных', () => {
    for (const round of ROUNDS) {
      const tiles = shuffleRound(round, Math.random)
      expect(tiles).toHaveLength(9)
      expect(tiles.filter((t) => t.correct)).toHaveLength(3)
    }
  })

  it('выбраны ровно правильные — верно', () => {
    const tiles = shuffleRound(ROUNDS[0], Math.random)
    const selected = new Set(tiles.flatMap((t, i) => (t.correct ? [i] : [])))
    expect(isCorrect(tiles, selected)).toBe(true)
  })

  it('не хватает одной — неверно', () => {
    const tiles = shuffleRound(ROUNDS[0], Math.random)
    const correct = tiles.flatMap((t, i) => (t.correct ? [i] : []))
    expect(isCorrect(tiles, new Set(correct.slice(1)))).toBe(false)
  })

  it('лишняя клетка — неверно', () => {
    const tiles = shuffleRound(ROUNDS[0], Math.random)
    const selected = new Set(tiles.flatMap((t, i) => (t.correct ? [i] : [])))
    selected.add(tiles.findIndex((t) => !t.correct))
    expect(isCorrect(tiles, selected)).toBe(false)
  })
})
