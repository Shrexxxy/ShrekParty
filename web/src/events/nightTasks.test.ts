import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/http'
import { GINGER_PARTS, isSwipe, makeFlies, nextTask, ONION_LAYERS, placePart, retryTask, snaps, swat, TASK_KINDS } from './nightTasks'

/** Псевдослучайность по кругу из заданных чисел. */
function seq(...values: number[]) {
  let i = 0
  return () => values[i++ % values.length]
}

describe('nextTask', () => {
  it('случайное задание из трёх', () => {
    expect(TASK_KINDS).toEqual(['onion', 'flies', 'gingerbread'])
    expect(nextTask(null, seq(0))).toBe('onion')
    expect(nextTask(null, seq(0.99))).toBe('gingerbread')
  })

  it('не повторяет прошлое подряд', () => {
    expect(nextTask('onion', seq(0))).toBe('flies')
    expect(nextTask('gingerbread', seq(0.99))).toBe('flies')
  })
})

describe('лук', () => {
  it('пять слоёв, слой снимается свайпом от 60 px в любую сторону', () => {
    expect(ONION_LAYERS).toBe(5)
    expect(isSwipe(60, 0)).toBe(true)
    expect(isSwipe(-30, -60)).toBe(true)
    expect(isSwipe(30, 30)).toBe(false)
  })
})

describe('мухи', () => {
  it('8 мух внутри поля, не у самого края', () => {
    const flies = makeFlies(seq(0, 1, 0.5))
    expect(flies).toHaveLength(8)
    expect(new Set(flies.map((f) => f.id)).size).toBe(8)
    for (const f of flies) {
      expect(f.x).toBeGreaterThanOrEqual(10)
      expect(f.x).toBeLessThanOrEqual(90)
      expect(f.y).toBeGreaterThanOrEqual(10)
      expect(f.y).toBeLessThanOrEqual(90)
    }
  })

  it('прихлопнутая муха исчезает', () => {
    const flies = makeFlies(seq(0.3))
    expect(swat(flies, flies[2].id)).toHaveLength(7)
    expect(swat(flies, flies[2].id).some((f) => f.id === flies[2].id)).toBe(false)
  })
})

describe('пряня', () => {
  it('голова, две руки и ноги', () => {
    expect(GINGER_PARTS.map((p) => p.id)).toEqual(['head', 'leftArm', 'rightArm', 'legs'])
  })

  it('деталь прилипает в радиусе 40 px от своего места', () => {
    const head = GINGER_PARTS[0]
    expect(snaps(head, { x: head.slot.x + 30, y: head.slot.y - 20 })).toBe(true)
    expect(snaps(head, { x: head.slot.x + 41, y: head.slot.y })).toBe(false)
  })

  it('собран, когда на местах все четыре; повтор не считается', () => {
    let placed: string[] = []
    for (const id of ['head', 'head', 'leftArm', 'rightArm']) {
      placed = placePart(placed, id)
    }
    expect(placed).toEqual(['head', 'leftArm', 'rightArm'])
    expect(placePart(placed, 'legs')).toHaveLength(GINGER_PARTS.length)
  })
})

describe('retryTask', () => {
  it('повторяем только «слишком быстро»; конец ночи, лимит и прочее — нет', () => {
    expect(retryTask(new ApiError(409, { error: 'too_fast' }))).toBe(true)
    expect(retryTask(new ApiError(409, { error: 'not_night' }))).toBe(false)
    expect(retryTask(new ApiError(409, { error: 'tasks_done' }))).toBe(false)
    expect(retryTask(new TypeError('Failed to fetch'))).toBe(false)
  })
})
