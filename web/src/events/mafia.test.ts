import { describe, expect, it } from 'vitest'
import type { MafiaDetails } from './details'
import { myStatus, nightLocked, nightOptions, phaseTitle, ROLE_NAMES, voteTally } from './mafia'

const player = (userId: string, alive = true) => ({ userId, login: userId, avatarUrl: null, alive, role: null })

function details(over: Partial<MafiaDetails>): MafiaDetails {
  return {
    phase: 'Night',
    day: 1,
    step: 0,
    deadline: null,
    players: [player('me'), player('p'), player('h'), player('x'), player('dead', false)],
    myRole: 'Civilian',
    iAmAlive: true,
    partners: [],
    myNightTarget: null,
    nightActions: [],
    checks: [],
    lastHeal: null,
    speakerId: null,
    speechQueue: [],
    votedCount: 0,
    iVoted: false,
    votes: [],
    eliminated: null,
    nightVictim: null,
    winner: null,
    ...over,
  }
}

describe('myStatus', () => {
  it('без роли — не в партии, иначе жив или выбыл', () => {
    expect(myStatus(details({ myRole: null, iAmAlive: false }))).toBe('outside')
    expect(myStatus(details({ iAmAlive: false }))).toBe('dead')
    expect(myStatus(details({}))).toBe('alive')
  })
})

describe('nightOptions', () => {
  const ids = (d: MafiaDetails) => nightOptions(d, 'me').map((p) => p.userId)

  it('живые кроме себя; мафии — без напарника, доктору — без вчерашнего', () => {
    expect(ids(details({}))).toEqual(['p', 'h', 'x'])
    expect(ids(details({ myRole: 'Mafia', partners: ['p'] }))).toEqual(['h', 'x'])
    expect(ids(details({ myRole: 'Doctor', lastHeal: 'h' }))).toEqual(['p', 'x'])
  })

})

describe('nightLocked', () => {
  it('комиссар после проверки больше не выбирает — список остаётся, но тапы молча игнорируются', () => {
    expect(nightLocked(details({ myRole: 'Commissar', myNightTarget: 'x' }))).toBe(true)
    expect(nightLocked(details({ myRole: 'Commissar' }))).toBe(false)
    expect(nightLocked(details({ myRole: 'Doctor', myNightTarget: 'x' }))).toBe(false)
  })
})

describe('phaseTitle', () => {
  it('текст для ТВ', () => {
    expect(phaseTitle('Night')).toBe('Город засыпает… 🌙')
    expect(phaseTitle('Morning')).toBe('Город просыпается ☀️')
  })
})

describe('ROLE_NAMES', () => {
  it('все роли по-русски', () => {
    expect(ROLE_NAMES.Mafia).toBe('Мафия')
    expect(ROLE_NAMES.Commissar).toBe('Комиссар')
  })
})

describe('voteTally', () => {
  it('голоса по игрокам, больше — выше; воздержавшиеся отдельно', () => {
    const tally = voteTally([
      { voterId: 'a', targetId: 'x' },
      { voterId: 'b', targetId: 'y' },
      { voterId: 'c', targetId: 'x' },
      { voterId: 'd', targetId: null },
    ])
    expect(tally.counts).toEqual([
      { targetId: 'x', voters: ['a', 'c'] },
      { targetId: 'y', voters: ['b'] },
    ])
    expect(tally.abstained).toEqual(['d'])
  })
})
