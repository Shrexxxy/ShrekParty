import { describe, expect, it } from 'vitest'
import type { ActiveEventDto, EventDto } from '../api/types'
import { eventPageState } from './eventPage'

const active = { id: 'a', type: 'Quiz', title: 'Своя игра', startedAt: null, details: null } as ActiveEventDto
const events = [
  { id: 'a', type: 'Quiz', title: 'Своя игра', status: 'Active', maxDonkeys: 100 },
  { id: 'f', type: 'FightPoll', title: 'Опрос', status: 'Finished', maxDonkeys: 0 },
  { id: 'd', type: 'Mafia', title: 'Мафия', status: 'Draft', maxDonkeys: 25 },
] as EventDto[]

describe('eventPageState', () => {
  it('идёт — по снапшоту; закончился, ещё впереди или нет такого — по программе', () => {
    expect(eventPageState('a', active, events)).toBe('live')
    expect(eventPageState('f', active, events)).toBe('finished')
    expect(eventPageState('d', active, events)).toBe('upcoming')
    expect(eventPageState('x', active, events)).toBe('unknown')
  })

  it('ивент без осликов (опрос) не попадает в программу — если страница видела его идущим, он закончился', () => {
    expect(eventPageState('p', null, events, true)).toBe('finished')
    expect(eventPageState('p', null, events, false)).toBe('unknown')
  })

  it('программа ещё не загрузилась — не «нет такого», а ждём', () => {
    expect(eventPageState('f', null, null)).toBe('loading')
    expect(eventPageState('a', active, null)).toBe('live')
  })
})
