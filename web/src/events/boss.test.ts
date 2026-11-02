import { describe, expect, it } from 'vitest'
import { actionText, activeFighter, BOSS_NAMES, feedStep, fxClass, hitsBoss, hpPercent, openLots, parsePrize, PRIZE_OPTIONS, prizeKey, type Fx } from './boss'

const fx = (kind: Fx['kind'], actorId: string | null = null, targetId: string | null = null, amount = 0): Fx => ({ kind, actorId, targetId, amount })

describe('BOSS_NAMES', () => {
  it('три босса', () => {
    expect(BOSS_NAMES).toEqual({ Peasant: '🧑‍🌾 Крестьянин с вилами', Farquaad: '🤴 Лорд Фаркуад', Fairy: '🧚 Фея-крёстная' })
  })
})

describe('feedStep', () => {
  it('по 0,6 с на событие, после конца — длина', () => {
    expect(feedStep(4, 0)).toBe(0)
    expect(feedStep(4, 599)).toBe(0)
    expect(feedStep(4, 650)).toBe(1)
    expect(feedStep(4, 10_000)).toBe(4)
    expect(feedStep(0, 500)).toBe(0)
  })
})

describe('activeFighter', () => {
  it('удар питомца — актёр; удар босса — цель; ход босса без цели — никто', () => {
    const feed = [fx('Attack', 'a', null, 8), fx('BossHit', null, 'b', 20), fx('BossHeal', null, null, 5)]
    expect(activeFighter(feed, 0)).toBe('a')
    expect(activeFighter(feed, 1)).toBe('b')
    expect(activeFighter(feed, 2)).toBeNull()
    expect(activeFighter(feed, 3)).toBeNull()
  })
})

describe('fxClass', () => {
  it('имя анимации-заглушки по типу события', () => {
    expect(fxClass(fx('Attack'))).toBe('boss-fx-attack')
    expect(fxClass(fx('BossHit'))).toBe('boss-fx-boss-hit')
    expect(fxClass(fx('Knockout'))).toBe('boss-fx-knockout')
  })
})

describe('hpPercent', () => {
  it('доля здоровья, не меньше 0', () => {
    expect(hpPercent(25, 100)).toBe(25)
    expect(hpPercent(-5, 100)).toBe(0)
    expect(hpPercent(1, 0)).toBe(0)
  })
})

describe('hitsBoss', () => {
  it('по боссу — только удары игроков; заслон, лечение, тыква — нет', () => {
    expect(['Attack', 'Weak', 'Power', 'Fire'].every((k) => hitsBoss(fx(k as Fx['kind'], 'a', null, 5)))).toBe(true)
    expect(['Covered', 'Cover', 'Heal', 'Stunned', 'BossHit'].some((k) => hitsBoss(fx(k as Fx['kind'], 'a', 'b', 5)))).toBe(false)
  })
})

describe('призы', () => {
  it('ключ «Куда»: финальный или босс × медаль, туда и обратно', () => {
    expect(prizeKey(null, null)).toBe('')
    expect(prizeKey('Peasant', 'Gold')).toBe('Peasant:Gold')
    expect(parsePrize('Peasant:Gold')).toEqual({ boss: 'Peasant', medal: 'Gold' })
    expect(parsePrize('')).toEqual({ boss: null, medal: null })
    expect(PRIZE_OPTIONS).toHaveLength(1 + 3 * 3)
  })

  it('сколько лотов уйдёт на аукцион боя — открытые и непроданные', () => {
    const prizes = [
      { name: 'а', medal: 'Bronze' as const, open: true, sold: true },
      { name: 'б', medal: 'Silver' as const, open: true, sold: false },
      { name: 'в', medal: 'Gold' as const, open: false, sold: false },
    ]
    expect(openLots(prizes)).toBe(1)
  })
})

describe('actionText', () => {
  it('название и что делает, с числом для своего питомца', () => {
    expect(actionText('Attack', 7)).toEqual({ name: 'Лягнуть', hint: 'Бьёт копытом: урон 7' })
    expect(actionText('Power', 14)).toEqual({ name: 'С разбега', hint: 'Двойной урон 14, но увернуться в этот ход нельзя' })
    expect(actionText('Fire', 18)).toEqual({ name: 'Драконий чих', hint: 'Огнём по боссу: урон 18, 4 маны' })
    expect(actionText('Heal', 12)).toEqual({ name: 'Луковый угар', hint: 'Лечит союзника на 12, 3 маны' })
    expect(actionText('Dodge', null).name).toBe('Увернуться')
    expect(actionText('Cover', null).name).toBe('Встать стеной')
  })
})
