import { describe, expect, it } from 'vitest'
import type { PetDto } from '../api/types'
import { CLASS_ICONS, CLASS_NAMES, eggArt, eggStage, levelUpText, petArt, skillGuide, switchToGame, xpProgress } from './pets'

function pet(over: Partial<PetDto>): PetDto {
  return {
    userId: 'u',
    login: 'Кот',
    name: 'Дракоша Кот',
    named: false,
    variant: 1,
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

describe('petArt / eggArt', () => {
  it('картинка по классу и рисунку', () => {
    expect(petArt(pet({ class: 'Tank', variant: 3 }))).toBe('/assets/pets/tank-3.png')
    expect(eggArt(pet({ class: 'Mage', variant: 1 }))).toBe('/assets/pets/egg-mage-1.png')
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

describe('skillGuide', () => {
  it('маг видит лягнуть, уворот, огонь и лечение — с уроном от интеллекта и ценой в мане', () => {
    const guide = skillGuide(pet({ class: 'Mage', damage: 5, intellect: 6 }))

    expect(guide.map((s) => s.action)).toEqual(['Attack', 'Dodge', 'Fire', 'Heal'])
    expect(guide.find((s) => s.action === 'Attack')?.amount).toBe(5)
    expect(guide.find((s) => s.action === 'Fire')).toMatchObject({ amount: 18, cost: '4 маны' })
    expect(guide.find((s) => s.action === 'Heal')).toMatchObject({ amount: 12, cost: '3 маны' })
    expect(guide.find((s) => s.action === 'Dodge')).toMatchObject({ amount: null, cost: null })
  })

  it('боец бьёт с разбега вдвое сильнее', () => {
    const guide = skillGuide(pet({ class: 'Fighter', damage: 7, intellect: 1 }))

    expect(guide.map((s) => s.action)).toEqual(['Attack', 'Dodge', 'Power'])
    expect(guide.find((s) => s.action === 'Power')?.amount).toBe(14)
  })

  it('танк встаёт стеной, числа у заслона нет; у каждого скилла есть название и описание', () => {
    const guide = skillGuide(pet({ class: 'Tank', damage: 4 }))

    expect(guide.map((s) => s.action)).toEqual(['Attack', 'Dodge', 'Cover'])
    expect(guide.find((s) => s.action === 'Cover')?.amount).toBeNull()
    expect(guide.every((s) => s.name.length > 0 && s.text.length > 20)).toBe(true)
  })
})
