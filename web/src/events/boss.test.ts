import { describe, expect, it } from 'vitest'
import { actionText, activeFighter, volleyProjectile, projectileFlight, hangingProjectiles, coveringAt, bossArt, bossBackdrop, bossPose, duelTarget, formation, hpAtStep, moveText, fxSprite, skillArt, BOSS_NAMES, feedStep, fxClass, hitsBoss, hpPercent, openLots, parsePrize, PRIZE_OPTIONS, prizeKey, type Fx } from './boss'

const fx = (kind: Fx['kind'], actorId: string | null = null, targetId: string | null = null, amount = 0): Fx => ({ kind, actorId, targetId, amount })

describe('BOSS_NAMES', () => {
  it('три босса', () => {
    expect(BOSS_NAMES).toEqual({ Peasant: 'Крестьянин с вилами', Farquaad: 'Лорд Фаркуад', Fairy: 'Фея-крёстная' })
  })
})

describe('feedStep', () => {
  it('по 1,5 с на событие, после конца — длина', () => {
    expect(feedStep(4, 0)).toBe(0)
    expect(feedStep(4, 1499)).toBe(0)
    expect(feedStep(4, 1550)).toBe(1)
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

describe('bossArt / skillArt', () => {
  it('картинки босса и скилла по имени', () => {
    expect(bossArt('Farquaad')).toBe('/assets/boss/farquaad.png')
    expect(skillArt('Heal')).toBe('/assets/skills/heal.png')
  })
})

describe('formation', () => {
  it('строй слева направо: маги сзади, бойцы посередине, танки спереди — ближе к боссу', () => {
    const f = (userId: string, cls: 'Tank' | 'Fighter' | 'Mage') => ({ userId, class: cls })
    const ranks = formation([f('t1', 'Tank'), f('m1', 'Mage'), f('f1', 'Fighter'), f('t2', 'Tank')])
    expect(ranks.map((r) => r.map((x) => x.userId))).toEqual([['m1'], ['f1'], ['t1', 't2']])
  })
})

describe('fxSprite', () => {
  it('какой спрайт эффекта и на ком: у того, кто бьёт, у цели или у босса', () => {
    expect(fxSprite('Attack', 'boss')).toBe('hit')
    expect(fxSprite('Power', 'boss')).toBe('slash')
    expect(fxSprite('Fire', 'actor')).toBe('fireball')
    expect(fxSprite('Fire', 'boss')).toBe('hit')
    expect(fxSprite('Heal', 'target')).toBe('onion-cloud')
    expect(fxSprite('Cover', 'actor')).toBe('shield')
    expect(fxSprite('Covered', 'actor')).toBe('shield')
    expect(fxSprite('BossHit', 'target')).toBe('hit')
    expect(fxSprite('Dodged', 'target')).toBe('dust')
    expect(fxSprite('Stunned', 'actor')).toBe('pumpkin')
    expect(fxSprite('BossShield', 'boss')).toBe('shield')
    expect(fxSprite('BossHeal', 'boss')).toBe('heal')
    expect(fxSprite('Attack', 'actor')).toBeNull()
  })
})

describe('duelTarget', () => {
  it('босс целится в одного — тот выходит к нему на подготовку и розыгрыш хода', () => {
    const one = { move: 'Sword' as const, targets: ['a'], damage: 10 }
    const all = { move: 'Volley' as const, targets: ['a', 'b'], damage: 5 }
    expect(duelTarget('Choosing', one, null)).toBe('a')
    expect(duelTarget('Choosing', all, 'a')).toBeNull()
    expect(duelTarget('Showing', null, 'a')).toBe('a')
    expect(duelTarget('Intro', null, 'a')).toBeNull()
    expect(duelTarget('Won', null, 'a')).toBeNull()
  })
})

describe('hpAtStep', () => {
  // Итог хода с сервера: Ann ударила босса на 10, босс ударил Bob на 30 и тот выбыл, маг вылечил Ann на 5.
  const fighters = [
    { userId: 'ann', hp: 25, maxHp: 40, alive: true },
    { userId: 'bob', hp: 0, maxHp: 30, alive: false },
  ]
  const feed = [
    fx('Heal', 'mage', 'ann', 5),
    fx('Attack', 'ann', null, 10),
    fx('BossHit', null, 'bob', 30),
    fx('Knockout', null, 'bob'),
  ]

  it('до удара полоски как в начале хода, после — по факту', () => {
    const start = hpAtStep(60, fighters, feed, 0)
    expect(start.bossHp).toBe(70)
    expect(start.fighters.map((f) => [f.hp, f.alive])).toEqual([[20, true], [30, true]])

    const afterHit = hpAtStep(60, fighters, feed, 3)
    expect(afterHit.bossHp).toBe(60)
    expect(afterHit.fighters.map((f) => [f.hp, f.alive])).toEqual([[25, true], [0, true]])

    const end = hpAtStep(60, fighters, feed, 4)
    expect(end.fighters.map((f) => [f.hp, f.alive])).toEqual([[25, true], [0, false]])
  })

  it('кто выбыл раньше этого хода — лежит с самого начала', () => {
    expect(hpAtStep(60, [{ userId: 'x', hp: 0, maxHp: 10, alive: false }], [], 0).fighters[0].alive).toBe(false)
  })
})

describe('formation с выбывшими', () => {
  it('выбывшие — отдельным задним рядом слева', () => {
    const f = (userId: string, cls: 'Tank' | 'Fighter' | 'Mage', alive = true) => ({ userId, class: cls, alive })
    const ranks = formation([f('t1', 'Tank'), f('m1', 'Mage', false), f('f1', 'Fighter')])
    expect(ranks.map((r) => r.map((x) => x.userId))).toEqual([['m1'], ['f1'], ['t1']])
    expect(formation([f('t1', 'Tank'), f('t2', 'Tank', false)]).map((r) => r.map((x) => x.userId))).toEqual([['t2'], ['t1']])
  })
})

describe('moveText', () => {
  it('название хода босса и что будет, с примерным уроном', () => {
    expect(moveText('Sword', 33)).toEqual({ name: 'Удар мечом', hint: 'Бьёт одного: ~33 урона' })
    expect(moveText('Volley', 12)).toEqual({ name: 'Залп лучников', hint: 'Стрелы по всем: ~12 урона каждому' })
    expect(moveText('Shield', null)).toEqual({ name: 'Щит', hint: 'Прикрывается: урон по нему вдвое меньше' })
    expect(moveText('Pumpkin', 9).hint).toBe('Превращает одного в тыкву: ~9 урона и пропуск хода')
  })
})

describe('formation: выбывшие по три в колонке', () => {
  it('выбывших больше трёх — несколько задних колонок', () => {
    const f = (userId: string, alive = false) => ({ userId, class: 'Tank' as const, alive })
    const ranks = formation([f('d1'), f('d2'), f('d3'), f('d4'), f('t1', true)])
    expect(ranks.map((r) => r.map((x) => x.userId))).toEqual([['d4'], ['d1', 'd2', 'd3'], ['t1']])
  })
})

describe('fxSprite: снаряды залпа и звёздного дождя', () => {
  it('стрела/звезда летит в цель; у танка в заслоне — разбивается о щит; меч — взмах', () => {
    expect(fxSprite('BossHit', 'target', 'Volley')).toBe('arrow')
    expect(fxSprite('BossHit', 'target', 'Stars')).toBe('star')
    expect(fxSprite('BossHit', 'target', 'Sword')).toBe('slash')
    expect(fxSprite('BossHit', 'target', 'Poke')).toBe('hit')
    expect(fxSprite('Absorbed', 'actor', 'Volley')).toBe('arrow-break')
    expect(fxSprite('Absorbed', 'actor', 'Stars')).toBe('star-break')
  })
})

describe('volleyProjectile', () => {
  it('над боссом висят снаряды — по одному на каждую цель удара по всем', () => {
    expect(volleyProjectile('Volley')).toBe('arrow')
    expect(volleyProjectile('Stars')).toBe('star')
    expect(volleyProjectile('Sword')).toBeNull()
  })
})

describe('projectileFlight (ТВ: полёт поверх арены)', () => {
  it('огненный шар — из мага в босса; стрела — из своего места над боссом в цель; в танка — разбивается', () => {
    expect(projectileFlight(fx('Fire', 'mage', null, 9), null)).toEqual({ from: { slot: null, fighter: 'mage' }, to: 'boss', sprite: 'fireball', end: 'hit' })
    expect(projectileFlight(fx('BossHit', null, 'ann', 5), 'Volley')).toEqual({ from: { slot: 'ann', fighter: null }, to: 'ann', sprite: 'arrow', end: 'hit' })
    expect(projectileFlight(fx('Absorbed', 'tank', 'bob', 0), 'Stars')).toEqual({ from: { slot: 'bob', fighter: null }, to: 'tank', sprite: 'star', end: 'break' })
    expect(projectileFlight(fx('Dodged', null, 'cid', 0), 'Volley')).toEqual({ from: { slot: 'cid', fighter: null }, to: 'cid', sprite: 'arrow', end: 'miss' })
    expect(projectileFlight(fx('BossHit', null, 'ann', 5), 'Sword')).toBeNull()
    expect(projectileFlight(fx('Attack', 'ann', null, 5), null)).toBeNull()
  })
})

describe('hangingProjectiles', () => {
  const order = ['a', 'b', 'c']
  it('на выборе висят снаряды по целям; в розыгрыше — пока до цели не дошла лента', () => {
    expect(hangingProjectiles(order, ['c', 'a'], [], 0)).toEqual(['a', 'c'])
    const feed = [fx('Attack', 'a', null, 3), fx('BossHit', null, 'a', 5), fx('Absorbed', 'b', 'c', 0)]
    expect(hangingProjectiles(order, null, feed, 0)).toEqual(['a', 'c'])
    expect(hangingProjectiles(order, null, feed, 1)).toEqual(['c'])
    expect(hangingProjectiles(order, null, feed, 2)).toEqual([])
  })
})

describe('coveringAt', () => {
  it('щит танка висит с момента заслона до конца хода', () => {
    const feed = [fx('Cover', 't1'), fx('Attack', 'a', null, 3), fx('BossHit', null, 'a', 5)]
    expect(coveringAt(feed, 0)).toEqual(new Set(['t1']))
    expect(coveringAt(feed, 2)).toEqual(new Set(['t1']))
    expect(coveringAt([fx('Attack', 'a', null, 3), fx('Cover', 't1')], 0)).toEqual(new Set())
  })
})

describe('bossPose', () => {
  const at = (file: string) => `/assets/boss/farquaad/${file}.png`
  const base = '/assets/boss/farquaad.png'
  const pose = (over: Partial<Parameters<typeof bossPose>[0]>) =>
    bossPose({ boss: 'Farquaad', phase: 'Choosing', hp: 100, max: 100, fx: null, move: null, ...over })

  it('стоит: картинка стадии по здоровью — больше 66 %, до 34 %, ниже; нет файла — стадия ниже, потом общая', () => {
    expect(pose({ hp: 67 })).toEqual([at('hp1'), base])
    expect(pose({ hp: 66 })).toEqual([at('hp2'), at('hp1'), base])
    expect(pose({ hp: 34 })).toEqual([at('hp2'), at('hp1'), base])
    expect(pose({ hp: 33 })).toEqual([at('hp3'), at('hp2'), at('hp1'), base])
  })

  it('делает ход — поза хода своей стадии, стадия 1 без суффикса', () => {
    expect(pose({ phase: 'Showing', hp: 50, fx: fx('BossHit', null, 'ann', 9), move: 'Sword' })).toEqual([
      at('move-sword-2'),
      at('move-sword'),
      at('hp2'),
      at('hp1'),
      base,
    ])
    expect(pose({ phase: 'Showing', hp: 90, fx: fx('BossShield'), move: 'Shield' })[0]).toBe(at('move-shield'))
    expect(pose({ phase: 'Showing', hp: 90, fx: fx('Dodged', null, 'ann'), move: 'Volley' })[0]).toBe(at('move-volley'))
  })

  it('получает удар — hurt своей стадии', () => {
    expect(pose({ phase: 'Showing', hp: 20, fx: fx('Attack', 'ann', null, 5), move: 'Sword' }).slice(0, 4)).toEqual([at('hurt-3'), at('hurt-2'), at('hurt'), at('hp3')])
  })

  it('побеждён — defeated; победил команду — victory своей стадии', () => {
    expect(pose({ phase: 'Won', hp: 0 }).slice(0, 4)).toEqual([at('defeated-3'), at('defeated-2'), at('defeated'), at('hp3')])
    expect(pose({ phase: 'Lost', hp: 50 }).slice(0, 2)).toEqual([at('victory-2'), at('victory')])
  })
})

describe('bossBackdrop', () => {
  it('у каждого боя свой задник: болото, Дюлок, бальный зал', () => {
    expect(bossBackdrop('Peasant')).toBe('/assets/boss/bg/peasant.jpg')
    expect(bossBackdrop('Farquaad')).toBe('/assets/boss/bg/farquaad.jpg')
    expect(bossBackdrop('Fairy')).toBe('/assets/boss/bg/fairy.jpg')
  })
})
