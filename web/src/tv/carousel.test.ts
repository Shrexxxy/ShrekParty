import { describe, expect, it } from 'vitest'
import { factsForCycle } from './carousel'

const facts = ['a', 'b', 'c', 'd', 'e'].map((text) => ({ text, logins: [] }))

describe('factsForCycle', () => {
  it('по два факта за круг, по порядку', () => {
    expect(factsForCycle(facts, 0).map((f) => f.text)).toEqual(['a', 'b'])
    expect(factsForCycle(facts, 1).map((f) => f.text)).toEqual(['c', 'd'])
  })

  it('по кругу через конец списка', () => {
    expect(factsForCycle(facts, 2).map((f) => f.text)).toEqual(['e', 'a'])
  })

  it('мало фактов — без повторов внутри круга', () => {
    expect(factsForCycle(facts.slice(0, 1), 3).map((f) => f.text)).toEqual(['a'])
  })

  it('нет фактов — пусто', () => {
    expect(factsForCycle([], 0)).toEqual([])
  })
})
