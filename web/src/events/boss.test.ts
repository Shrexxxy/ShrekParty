import { describe, expect, it } from 'vitest'
import { activeFighter, BOSS_NAMES, feedStep, fxClass, hitsBoss, hpPercent, type Fx } from './boss'

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
