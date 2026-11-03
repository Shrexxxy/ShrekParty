import { describe, expect, it } from 'vitest'
import type { MatchDto, TournamentDetails } from './details'
import { bracketLines, currentMatchId, myBets } from './tournament'

const ann = { id: 'ann', login: 'Ann', avatarUrl: null, isAdmin: false }
const bob = { id: 'bob', login: 'Bob', avatarUrl: null, isAdmin: false }

function match(id: string, over: Partial<MatchDto> = {}): MatchDto {
  return {
    id,
    index: 0,
    round: 'Quarter',
    roundIndex: 0,
    playerA: ann,
    playerB: bob,
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
    ...over,
  }
}

function details(matches: MatchDto[], betMatchId: string | null): TournamentDetails {
  return { game: 'FIFA', matches, myNextMatchId: null, challenge: { state: 'None', offeredToId: null, offeredToMe: false }, betMatchId }
}

describe('currentMatchId', () => {
  it('идёт матч — подсвечиваем его', () => {
    expect(currentMatchId(details([match('m1', { status: 'Live' }), match('m2')], 'm2'))).toBe('m1')
  })

  it('матча нет — подсвечиваем следующий, на который открыты ставки', () => {
    expect(currentMatchId(details([match('m1', { status: 'Done' }), match('m2')], 'm2'))).toBe('m2')
    expect(currentMatchId(details([match('m1', { status: 'Done' })], null))).toBeNull()
  })
})

describe('myBets', () => {
  it('свои ставки по порядку сетки: на кого, сколько и чем кончилось', () => {
    const bets = myBets(
      details(
        [
          match('m1', { index: 1, status: 'Done', winnerId: 'ann', myBet: { onPlayerId: 'ann', amount: 10 } }),
          match('m0', { index: 0, status: 'Done', winnerId: 'bob', myBet: { onPlayerId: 'ann', amount: 5 } }),
          match('m2', { index: 2, status: 'Live', myBet: { onPlayerId: 'bob', amount: 7 } }),
          match('m3', { index: 3 }),
        ],
        null,
      ),
    )

    expect(bets.map((b) => [b.matchId, b.on, b.amount, b.result])).toEqual([
      ['m0', 'Ann', 5, 'lost'],
      ['m1', 'Ann', 10, 'won'],
      ['m2', 'Bob', 7, 'pending'],
    ])
  })
})

describe('bracketLines', () => {
  const box = (left: number, top: number) => ({ left, top, right: left + 100, bottom: top + 60 })

  it('линия от правого края матча к левому краю следующего: вправо, по вертикали посередине промежутка, вправо', () => {
    const lines = bracketLines(
      [match('a', { index: 0, nextIndex: 2 }), match('b', { index: 1, nextIndex: 2 }), match('f', { index: 2 })],
      new Map([
        [0, box(0, 0)],
        [1, box(0, 100)],
        [2, box(140, 50)],
      ]),
    )

    expect(lines).toEqual([
      { key: '0-2', d: 'M 100 30 H 120 V 80 H 140' },
      { key: '1-2', d: 'M 100 130 H 120 V 80 H 140' },
    ])
  })

  it('матча нет на экране (вызов, третье место) — линии нет', () => {
    expect(bracketLines([match('a', { index: 0, nextIndex: 9 })], new Map([[0, box(0, 0)]]))).toEqual([])
  })
})
