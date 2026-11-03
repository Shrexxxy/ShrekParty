import { describe, expect, it } from 'vitest'
import { bracketColumns, oddsText } from './tournament'
import { formatClock, remainingMs } from './countdown'
import { stepView } from './ceremony'
import { isDarkPhase } from './hide'
import type { MatchDto, StandingDto } from './details'

function match(round: MatchDto['round'], roundIndex: number): MatchDto {
  return {
    id: `${round}-${roundIndex}`,
    index: 0,
    round,
    roundIndex,
    playerA: null,
    playerB: null,
    isBye: false,
    status: 'Pending',
    winnerId: null,
    score: null,
    photos: 0,
    pool: 0,
    oddsA: null,
    oddsB: null,
    myBet: null,
    nextIndex: null,
  }
}

describe('bracketColumns', () => {
  it('колонки по раундам по порядку, пустые раунды пропускаются', () => {
    const columns = bracketColumns([match('Final', 0), match('Quarter', 1), match('Quarter', 0), match('Semi', 0), match('ThirdPlace', 0)])
    expect(columns.map((c) => c.title)).toEqual(['1/4', '1/2', 'Финал'])
    expect(columns[0].matches.map((m) => m.roundIndex)).toEqual([0, 1])
  })

  it('матч за 3-е — в колонке финала, после финала', () => {
    const columns = bracketColumns([match('ThirdPlace', 0), match('Final', 0)])
    expect(columns.at(-1)!.matches.map((m) => m.round)).toEqual(['Final', 'ThirdPlace'])
  })

  it('отборочные и вызов — свои колонки по краям', () => {
    const columns = bracketColumns([match('Qualifier', 0), match('Final', 0), match('Challenge', 0)])
    expect(columns.map((c) => c.title)).toEqual(['Отбор', 'Финал', 'Вызов имениннику'])
  })
})

describe('oddsText', () => {
  it('коэффициент одним знаком', () => {
    expect(oddsText(2.4)).toBe('×2.4')
    expect(oddsText(1.5)).toBe('×1.5')
  })

  it('нет ставок — прочерк', () => {
    expect(oddsText(null)).toBe('—')
  })
})

describe('countdown', () => {
  it('остаток по часам сервера, а не телефона', () => {
    // Телефон отстаёт на 5 минут: снапшот пришёл в локальное 1000, сервер тогда был на 18:00:00.
    const ms = remainingMs('2026-11-04T18:02:00Z', '2026-11-04T18:00:00Z', 1000, 31_000)
    expect(ms).toBe(90_000)
  })

  it('не уходит в минус', () => {
    expect(remainingMs('2026-11-04T18:00:00Z', '2026-11-04T18:00:00Z', 0, 10_000)).toBe(0)
  })

  it('формат м:сс', () => {
    expect(formatClock(90_000)).toBe('1:30')
    expect(formatClock(5_400)).toBe('0:06')
    expect(formatClock(0)).toBe('0:00')
  })
})

describe('stepView', () => {
  const standings: StandingDto[] = [
    { userId: 'a', login: 'Ann', avatarUrl: null, donkeys: 300, place: 1, achievement: 'x' },
    { userId: 'b', login: 'Bob', avatarUrl: null, donkeys: 200, place: 2, achievement: 'y' },
    { userId: 'c', login: 'Cid', avatarUrl: null, donkeys: 100, place: 3, achievement: 'z' },
    { userId: 'd', login: 'Dan', avatarUrl: null, donkeys: 50, place: 4, achievement: 'w' },
  ]

  it('0 — вступление', () => {
    expect(stepView(0, standings).kind).toBe('intro')
  })

  it('игроки с последнего места', () => {
    const first = stepView(1, standings)
    expect(first.kind === 'player' && first.standing.login).toBe('Dan')
    const last = stepView(4, standings)
    expect(last.kind === 'player' && last.standing.login).toBe('Ann')
  })

  it('после игроков — пьедестал из топ-3, потом финал', () => {
    const podium = stepView(5, standings)
    expect(podium.kind === 'podium' && podium.top.map((s) => s.login)).toEqual(['Ann', 'Bob', 'Cid'])
    expect(stepView(6, standings).kind).toBe('final')
  })
})

describe('isDarkPhase', () => {
  const hide = (phase: string) => ({ id: 'e', type: 'HideAndSeek' as const, title: 'Прятки', startedAt: null, details: { phase } })

  it('темно, пока прячутся и снимают', () => {
    expect(isDarkPhase(hide('Hiding'))).toBe(true)
    expect(isDarkPhase(hide('Shooting'))).toBe(true)
  })

  it('на голосовании и в других ивентах — светло', () => {
    expect(isDarkPhase(hide('Voting'))).toBe(false)
    expect(isDarkPhase({ ...hide('Hiding'), type: 'Laugh' as const })).toBe(false)
    expect(isDarkPhase(null)).toBe(false)
  })
})
