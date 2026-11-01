import { describe, expect, it } from 'vitest'
import type { ProgramItemDto } from '../api/types'
import { moveOrder, nextDraft } from './programOrder'

function item(id: string, order: number, status: ProgramItemDto['status']): ProgramItemDto {
  return { id, type: 'Laugh', title: id, status, order, maxDonkeys: 100 }
}

describe('nextDraft', () => {
  it('первый черновик по порядку, пропуская прошедшие', () => {
    const items = [item('b', 1, 'Draft'), item('a', 0, 'Finished'), item('c', 2, 'Draft')]
    expect(nextDraft(items)?.id).toBe('b')
  })

  it('пока что-то идёт — следующего нет', () => {
    expect(nextDraft([item('a', 0, 'Active'), item('b', 1, 'Draft')])).toBeNull()
  })

  it('всё прошло — null', () => {
    expect(nextDraft([item('a', 0, 'Finished')])).toBeNull()
  })
})

describe('moveOrder', () => {
  const items = [item('a', 0, 'Draft'), item('b', 1, 'Draft'), item('c', 2, 'Draft')]

  it('вверх — на позицию выше', () => {
    expect(moveOrder(items, 'b', -1)).toBe(0)
  })

  it('вниз — на позицию ниже', () => {
    expect(moveOrder(items, 'b', 1)).toBe(2)
  })

  it('за края не выходит', () => {
    expect(moveOrder(items, 'a', -1)).toBeNull()
    expect(moveOrder(items, 'c', 1)).toBeNull()
  })
})
