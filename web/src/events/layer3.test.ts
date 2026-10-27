import { describe, expect, it } from 'vitest'
import { myTaps, raceProgress, reactionTap, revealedCount, selfieAllowed, tapperBanner } from './layer3'

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

describe('tapperBanner', () => {
  it('отсечка «30» держится три секунды', () => {
    expect(tapperBanner(31_000)).toBeNull()
    expect(tapperBanner(30_000)).toBe('Осталось 30 секунд!')
    expect(tapperBanner(27_500)).toBe('Осталось 30 секунд!')
    expect(tapperBanner(26_000)).toBeNull()
  })

  it('последние 10 — до конца', () => {
    expect(tapperBanner(10_000)).toBe('Последние 10!')
    expect(tapperBanner(1)).toBe('Последние 10!')
    expect(tapperBanner(0)).toBeNull()
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
