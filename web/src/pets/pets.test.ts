import { describe, expect, it } from 'vitest'
import type { PetDto } from '../api/types'
import { CLASS_ICONS, CLASS_NAMES, eggStage, levelUpText, switchToGame, xpProgress } from './pets'

function pet(over: Partial<PetDto>): PetDto {
  return {
    userId: 'u',
    login: 'Кот',
    name: 'Дракоша Кот',
    class: 'Mage',
    hatched: true,
    level: 1,
    earned: 0,
    nextLevelAt: 50,
    strength: 2,
    agility: 4,
    intellect: 6,
    health: 30,
    damage: 6,
    mana: 12,
    ...over,
  }
}

describe('CLASS_NAMES', () => {
  it('классы по-русски', () => {
    expect(CLASS_NAMES).toEqual({ Tank: 'Танк', Fighter: 'Боец', Mage: 'Маг' })
    expect(CLASS_ICONS).toEqual({ Tank: 'shield', Fighter: 'sword', Mage: 'spark' })
  })
})

describe('eggStage', () => {
  it('трещины на 5 и 10 тапе, на 15-м вылупился', () => {
    expect([0, 4, 5, 9, 10, 14, 15, 20].map(eggStage)).toEqual([0, 0, 1, 1, 2, 2, 3, 3])
  })
})

describe('xpProgress', () => {
  it('сколько осталось до следующего уровня и доля пройденного', () => {
    expect(xpProgress(pet({ level: 2, earned: 73, nextLevelAt: 100 }))).toEqual({ left: 27, percent: 46 })
    expect(xpProgress(pet({ level: 1, earned: 0, nextLevelAt: 50 }))).toEqual({ left: 50, percent: 0 })
  })
})

describe('levelUpText', () => {
  it('вырос на уровень — приросты, больший первым', () => {
    const prev = pet({ level: 3, strength: 3, agility: 5, intellect: 10 })
    const next = pet({ level: 4, strength: 4, agility: 5, intellect: 12 })
    expect(levelUpText(prev, next)).toBe('Дракоша Кот вырос до 4 уровня! +2 интеллект, +1 сила')
  })

  it('не вырос — null', () => {
    expect(levelUpText(pet({}), pet({}))).toBeNull()
  })

  it('сразу на два уровня — суммарно', () => {
    const prev = pet({ level: 3, strength: 3, agility: 5, intellect: 10 })
    const next = pet({ level: 5, strength: 4, agility: 6, intellect: 14 })
    expect(levelUpText(prev, next)).toBe('Дракоша Кот вырос до 5 уровня! +4 интеллект, +1 сила, +1 ловкость')
  })
})

describe('switchToGame', () => {
  it('начался новый ивент — на «Игру»; тот же или кончился — остаёмся', () => {
    expect(switchToGame(null, 'e1')).toBe(true)
    expect(switchToGame('e1', 'e2')).toBe(true)
    expect(switchToGame('e1', 'e1')).toBe(false)
    expect(switchToGame('e1', null)).toBe(false)
  })
})
