import { describe, expect, it } from 'vitest'
import { easeSpeed, flapTap, flapTick, flyerArt, myTaps, skySpeed, tapRate, raceLanes, raceProgress, reactionTap, revealedCount, selfieAllowed } from './layer3'

describe('revealedCount', () => {
  it('открывает по одному, потом правду', () => {
    expect(revealedCount(0, 3, 2000)).toBe(1)
    expect(revealedCount(1999, 3, 2000)).toBe(1)
    expect(revealedCount(2000, 3, 2000)).toBe(2)
    expect(revealedCount(6000, 3, 2000)).toBe(4)
  })

  it('дальше правды не идёт', () => {
    expect(revealedCount(60_000, 3, 2000)).toBe(4)
  })

  it('без ответов сразу правда', () => {
    expect(revealedCount(0, 0, 2000)).toBe(1)
  })
})

describe('raceProgress', () => {
  it('доля от лидера', () => {
    expect(raceProgress(50, 200)).toBe(0.25)
    expect(raceProgress(200, 200)).toBe(1)
  })

  it('пока никто не тапал — все на старте', () => {
    expect(raceProgress(0, 0)).toBe(0)
  })
})

describe('reactionTap', () => {
  it('время от кадра позеленения', () => {
    expect(reactionTap(1000, 1312.4)).toBe(312)
  })

  it('тап раньше, чем экран позеленел, не засчитывается (а не 1 мс)', () => {
    expect(reactionTap(null, 500)).toBeNull()
  })
})

describe('selfieAllowed', () => {
  it('после регистрации, когда ничего не идёт', () => {
    expect(selfieAllowed('Finished', null)).toBe(true)
  })

  it('во время ивента камера свернула бы страницу', () => {
    expect(selfieAllowed('Finished', { id: 'e', type: 'Days', title: 'Дни', startedAt: null, details: {} })).toBe(false)
  })

  it('до конца регистрации — нет', () => {
    expect(selfieAllowed('Open', null)).toBe(false)
  })
})

describe('myTaps', () => {
  it('счёт игрока с сервера', () => {
    expect(myTaps([{ userId: 'a', taps: 250 }], 'a')).toBe(250)
  })

  it('нет в таблице — с нуля', () => {
    expect(myTaps([], 'a')).toBe(0)
    expect(myTaps([{ userId: 'a', taps: 5 }], undefined)).toBe(0)
  })
})

describe('raceLanes', () => {
  const row = (login: string, taps: number) => ({ userId: login, login, avatarUrl: null, taps, petClass: null, petVariant: null })

  it('дорожки на ТВ не прыгают: порядок по логину, а место — отдельной цифрой', () => {
    const lanes = raceLanes([row('Bob', 30), row('ann', 50), row('Cid', 0)])

    expect(lanes.map((l) => l.login)).toEqual(['ann', 'Bob', 'Cid'])
    expect(lanes.map((l) => l.place)).toEqual([1, 2, null])
  })

  it('порядок тот же, когда тапы меняются; равные делят место', () => {
    expect(raceLanes([row('Bob', 70), row('ann', 50), row('Cid', 70)]).map((l) => [l.login, l.place])).toEqual([
      ['ann', 3],
      ['Bob', 1],
      ['Cid', 1],
    ])
  })
})

describe('flyerArt', () => {
  it('кадры питомца: 0 — обычная картинка, 1–3 — взмах (fly-1…3)', () => {
    expect(flyerArt({ petClass: 'Mage', petVariant: 3 }, 0)).toBe('/assets/pets/mage-3.png')
    expect(flyerArt({ petClass: 'Mage', petVariant: 3 }, 1)).toBe('/assets/pets/mage-3-fly-1.png')
    expect(flyerArt({ petClass: 'Mage', petVariant: 3 }, 3)).toBe('/assets/pets/mage-3-fly-3.png')
  })

  it('без питомца — общий дракон: крылья вверх в покое и в начале взмаха, вниз — в конце', () => {
    expect([0, 1, 2, 3].map((f) => flyerArt({ petClass: null, petVariant: null }, f as 0 | 1 | 2 | 3))).toEqual([
      '/assets/tapper/dragon-up.png',
      '/assets/tapper/dragon-up.png',
      '/assets/tapper/dragon-down.png',
      '/assets/tapper/dragon-down.png',
    ])
  })
})

describe('flapTap / flapTick', () => {
  const run = (state: { frame: 0 | 1 | 2 | 3; queue: number }, ticks: number) => {
    const frames: number[] = []
    for (let i = 0; i < ticks; i++) {
      state = flapTick(state)
      frames.push(state.frame)
    }
    return frames
  }

  it('один тап — один взмах из трёх кадров и снова покой', () => {
    expect(run(flapTap({ frame: 0, queue: 0 }), 5)).toEqual([1, 2, 3, 0, 0])
  })

  it('частые тапы — тап посреди взмаха продолжает махать без остановки в покое', () => {
    let s = flapTick(flapTap({ frame: 0, queue: 0 }))
    s = flapTick(s)
    const before = [1, 2]
    s = flapTap(s)
    expect([...before, ...run(s, 5)]).toEqual([1, 2, 3, 1, 2, 3, 0])
  })

  it('телефон: перестал тапать — доигрывает текущий взмах и ещё максимум один, дальше покой', () => {
    let s = { frame: 0 as const, queue: 0 } as { frame: 0 | 1 | 2 | 3; queue: number }
    for (let i = 0; i < 20; i++) {
      s = flapTap(s)
    }
    expect(run(s, 8)).toEqual([1, 2, 3, 0, 0, 0, 0, 0])
    expect(run(flapTap({ frame: 2, queue: 0 }), 6)).toEqual([3, 1, 2, 3, 0, 0])
  })

  it('ТВ: тапы приходят пачкой раз в секунду — до трёх взмахов вперёд, чтобы между обновлениями не замирал', () => {
    expect(flapTap({ frame: 0, queue: 0 }, 50, 3).queue).toBe(3)
  })
})

describe('скорость неба', () => {
  it('темп — тапы за последнюю секунду', () => {
    expect(tapRate([100, 400, 900, 1500, 1900], 2000)).toBe(2)
    expect(tapRate([], 2000)).toBe(0)
  })

  it('чем чаще тапаешь, тем быстрее небо; не тапаешь — стоит; есть потолок', () => {
    expect(skySpeed(0)).toBe(0)
    expect(skySpeed(4)).toBeLessThan(skySpeed(8))
    expect(skySpeed(100)).toBe(skySpeed(30))
  })

  it('скорость меняется плавно: за кадр — часть пути к цели, за секунду — почти до цели', () => {
    const step = easeSpeed(0, 400, 1 / 60)
    expect(step).toBeGreaterThan(0)
    expect(step).toBeLessThan(400)
    let v = 0
    for (let i = 0; i < 60; i++) {
      v = easeSpeed(v, 400, 1 / 60)
    }
    expect(v).toBeGreaterThan(380)
  })
})
