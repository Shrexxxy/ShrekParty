import { describe, expect, it } from 'vitest'
import { CLOSING_PHRASES, FIRST_PHRASES, MIDDLE_PHRASES, closingGreetings, greetingFor } from './greetings'

const first = () => 0

describe('greetings', () => {
  it('всего 15 фраз: 2 первых, 11 средних, 2 последних', () => {
    expect(FIRST_PHRASES).toHaveLength(2)
    expect(MIDDLE_PHRASES).toHaveLength(11)
    expect(CLOSING_PHRASES).toHaveLength(2)
  })

  it('первым двум — свои фразы', () => {
    expect(greetingFor('bob', 1, [], first).text).toBe('Ого, ты быстр, bob! Первый на болоте!')
    expect(greetingFor('ann', 2, [], first).text).toBe('ann дышит в спину лидеру!')
  })

  it('средние — без повторов, пока не кончатся', () => {
    let used: number[] = []
    const texts = new Set<string>()
    for (let nth = 3; nth < 3 + MIDDLE_PHRASES.length; nth++) {
      const g = greetingFor('x', nth, used, first)
      used = g.used
      texts.add(g.text)
    }
    expect(texts.size).toBe(MIDDLE_PHRASES.length)
  })

  it('когда средние кончились — снова по кругу', () => {
    const used = MIDDLE_PHRASES.map((_, i) => i)
    const g = greetingFor('x', 20, used, first)
    expect(MIDDLE_PHRASES.map((p) => p.replace('{name}', 'x'))).toContain(g.text)
    expect(g.used).toHaveLength(1)
  })

  it('при закрытии — фразы предпоследнему и последнему', () => {
    expect(closingGreetings(['bob', 'ann', 'cid'])).toEqual([
      'ann — предпоследний. Не самый медленный!',
      'А cid у нас последний 🐢',
    ])
  })

  it('при меньше чем трёх игроках закрывающих фраз нет', () => {
    expect(closingGreetings(['bob', 'ann'])).toEqual([])
  })
})
