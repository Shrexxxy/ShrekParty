import { describe, expect, it } from 'vitest'
import { medalFor } from './medals'

describe('medalFor', () => {
  it('золото, серебро, бронза', () => {
    expect(medalFor(1, 5)).toBe('🥇')
    expect(medalFor(2, 5)).toBe('🥈')
    expect(medalFor(3, 5)).toBe('🥉')
  })

  it('4-е и дальше — медаль с номером', () => {
    expect(medalFor(4, 5)).toBe('🏅4')
  })

  it('последнему — черепаха, даже если он третий', () => {
    expect(medalFor(5, 5)).toBe('🐢')
    expect(medalFor(3, 3)).toBe('🐢')
  })

  it('единственный игрок — золото, а не черепаха', () => {
    expect(medalFor(1, 1)).toBe('🥇')
  })
})
