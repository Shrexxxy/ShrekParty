import { describe, expect, it } from 'vitest'
import { bidButtons, goingCall } from './auction'

describe('goingCall', () => {
  it('раз… два… под конец таймера', () => {
    expect(goingCall(5000)).toBeNull()
    expect(goingCall(3000)).toBe('Раз…')
    expect(goingCall(1500)).toBe('Два…')
    expect(goingCall(1)).toBe('Два…')
    expect(goingCall(0)).toBeNull()
  })
})

describe('bidButtons', () => {
  it('ставок нет — одна кнопка по стартовой цене', () => {
    expect(bidButtons({ price: null, startPrice: 50, balance: 100, leader: false })).toEqual([{ step: 0, amount: 50, enabled: true }])
  })

  it('дальше — шаги от текущей цены, дороже «на руках» — недоступно', () => {
    expect(bidButtons({ price: 80, startPrice: 50, balance: 95, leader: false })).toEqual([
      { step: 5, amount: 85, enabled: true },
      { step: 10, amount: 90, enabled: true },
      { step: 25, amount: 105, enabled: false },
    ])
  })

  it('лидер сам себя не перебивает', () => {
    expect(bidButtons({ price: 80, startPrice: 50, balance: 500, leader: true }).every((b) => !b.enabled)).toBe(true)
  })
})
