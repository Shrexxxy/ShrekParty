import { describe, expect, it } from 'vitest'
import { bestOffset, canPress, cellLabel, pressTime, rouletteFrames, typedChars } from './quiz'

describe('bestOffset', () => {
  it('берёт самую большую разницу — у неё меньше всего сетевой задержки', () => {
    expect(bestOffset([480, 500, 430])).toBe(500)
  })

  it('без замеров — ноль', () => {
    expect(bestOffset([])).toBe(0)
  })
})

describe('pressTime', () => {
  it('момент нажатия на часах сервера', () => {
    expect(pressTime(1_000, 500)).toBe(new Date(1_500).toISOString())
  })
})

describe('typedChars', () => {
  const start = '2026-11-06T18:00:00.000Z'
  const at = (ms: number) => Date.parse(start) + ms

  it('первая буква сразу', () => {
    expect(typedChars('Шрек', start, at(0), 60)).toBe(1)
  })

  it('буква каждые 60 мс', () => {
    expect(typedChars('Шрек', start, at(119), 60)).toBe(2)
    expect(typedChars('Шрек', start, at(120), 60)).toBe(3)
  })

  it('не больше длины текста', () => {
    expect(typedChars('Шрек', start, at(10_000), 60)).toBe(4)
  })

  it('часы телефона чуть отстали — не меньше первой буквы', () => {
    expect(typedChars('Шрек', start, at(-50), 60)).toBe(1)
  })
})

describe('canPress', () => {
  const base = { phase: 'Reading' as const, wrong: [] as string[], answererId: null as string | null }

  it('пока читают — можно', () => {
    expect(canPress(base, 'a')).toBe(true)
  })

  it('ошибся на этом вопросе — нельзя', () => {
    expect(canPress({ ...base, wrong: ['a'] }, 'a')).toBe(false)
  })

  it('не в фазе чтения и без входа — нельзя', () => {
    expect(canPress({ ...base, phase: 'Answering' }, 'a')).toBe(false)
    expect(canPress(base, undefined)).toBe(false)
  })
})

describe('cellLabel', () => {
  it('для подтверждения выбора', () => {
    expect(cellLabel('Шрек', 300)).toBe('«Шрек» за 300')
  })
})

describe('rouletteFrames', () => {
  it('крутится по кругу и останавливается на выбранном', () => {
    const frames = rouletteFrames(['a', 'b', 'c'], 'b', 7)
    expect(frames).toHaveLength(7)
    expect(frames.at(-1)).toBe('b')
    expect(frames).toEqual(['b', 'c', 'a', 'b', 'c', 'a', 'b'])
  })

  it('выбранного нет в списке — кадр только с ним', () => {
    expect(rouletteFrames(['a'], 'z', 5)).toEqual(['z'])
  })
})
