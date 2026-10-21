import { describe, expect, it } from 'vitest'
import { highlightLogins } from './highlight'

describe('highlightLogins', () => {
  it('выделяет логин в тексте', () => {
    expect(highlightLogins('Первый пароль petya: 1*34*6', ['petya'])).toEqual([
      { text: 'Первый пароль ' },
      { text: 'petya', login: true },
      { text: ': 1*34*6' },
    ])
  })

  it('без логинов — весь текст одним куском', () => {
    expect(highlightLogins('Просто текст', [])).toEqual([{ text: 'Просто текст' }])
  })

  it('спецсимволы в логине не ломают поиск', () => {
    expect(highlightLogins('Привет, a.b_c!', ['a.b_c'])).toEqual([
      { text: 'Привет, ' },
      { text: 'a.b_c', login: true },
      { text: '!' },
    ])
  })
})
