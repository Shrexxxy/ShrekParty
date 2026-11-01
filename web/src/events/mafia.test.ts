import { describe, expect, it } from 'vitest'
import type { MafiaDetails } from './details'
import { bonusSheet, myStatus, nightOptions, phaseTitle, ROLE_NAMES, voteTally } from './mafia'

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
    myTasksDone: 0,
    tasksTotal: 0,
    tasks: [],
    bonuses: [],
    winDonkeys: 25,
    ...over,
  }
}

describe('bonusSheet', () => {
  const players = [
    { userId: 'm', login: 'Осёл', avatarUrl: null, alive: true, role: 'Mafia' as const },
    { userId: 'c', login: 'Кот', avatarUrl: null, alive: false, role: 'Civilian' as const },
    { userId: 'd', login: 'Фиона', avatarUrl: null, alive: true, role: 'Doctor' as const },
  ]

  it('по игроку: победа, бонусы строками «кол-во × цена = сумма», итог; больше — выше', () => {
    const sheet = bonusSheet(
      details({
        players,
        winner: 'Civilians',
        winDonkeys: 25,
        bonuses: [
          { userId: 'm', kind: 'Kill', count: 2, amount: 20 },
          { userId: 'c', kind: 'Tasks', count: 7, amount: 7 },
          { userId: 'c', kind: 'Oust', count: 1, amount: 10 },
        ],
      }),
    )

    expect(sheet.map((r) => r.login)).toEqual(['Кот', 'Фиона', 'Осёл'])
    expect(sheet[0]).toMatchObject({ role: 'Civilian', win: 25, total: 42 })
    expect(sheet[0].lines).toEqual(['Задания: 7 × 1 = 7', 'Посадки: 1 × 10 = 10'])
    expect(sheet[1]).toMatchObject({ win: 25, total: 25, lines: [] })
    expect(sheet[2]).toMatchObject({ win: 0, total: 20, lines: ['Убийства: 2 × 10 = 20'] })
  })

  it('пока нет победителя — за победу 0', () => {
    expect(bonusSheet(details({ players, winDonkeys: 25 })).every((r) => r.win === 0)).toBe(true)
  })
})

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
