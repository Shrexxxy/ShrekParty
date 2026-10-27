import { describe, expect, it } from 'vitest'
import { byRounds, nextRound, type QuizThemeDto } from './editor'

const theme = (round: number, name: string): QuizThemeDto => ({ id: name, round, order: 0, name, questions: [] })

describe('byRounds', () => {
  it('раунды по порядку, финал отдельно', () => {
    const { rounds, final } = byRounds([theme(2, 'Б'), theme(0, 'Ф'), theme(1, 'А1'), theme(1, 'А2')])
    expect(rounds.map((r) => [r.round, r.themes.map((t) => t.name)])).toEqual([
      [1, ['А1', 'А2']],
      [2, ['Б']],
    ])
    expect(final?.name).toBe('Ф')
  })

  it('без финала — null', () => {
    expect(byRounds([theme(1, 'А')]).final).toBeNull()
  })
})

describe('nextRound', () => {
  it('следующий после последнего', () => {
    expect(nextRound([theme(1, 'А'), theme(3, 'В'), theme(0, 'Ф')])).toBe(4)
  })

  it('пусто — первый', () => {
    expect(nextRound([])).toBe(1)
  })
})
