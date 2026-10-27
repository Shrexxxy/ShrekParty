import { describe, expect, it } from 'vitest'
import { missingMedia, singleFlight, staleMedia } from './quizCache'

describe('missingMedia', () => {
  it('качаем только то, чего ещё нет в кэше', () => {
    expect(missingMedia(['/media/a', '/media/b', '/media/c'], new Set(['/media/b']))).toEqual(['/media/a', '/media/c'])
  })

  it('повторы в списке — один раз', () => {
    expect(missingMedia(['/media/a', '/media/a'], new Set())).toEqual(['/media/a'])
  })
})

describe('staleMedia', () => {
  it('из кэша убираем то, чего больше нет в игре', () => {
    expect(staleMedia(['/media/a', '/media/old'], ['/media/a', '/media/b'])).toEqual(['/media/old'])
  })
})

describe('singleFlight', () => {
  it('пока прошлый запуск идёт, новый к нему присоединяется', async () => {
    let calls = 0
    let finish = () => {}
    const run = singleFlight(() => {
      calls += 1
      return new Promise<void>((resolve) => {
        finish = resolve
      })
    })

    const first = run()
    const second = run()
    expect(calls).toBe(1)
    finish()
    await Promise.all([first, second])

    void run()
    expect(calls).toBe(2)
  })
})
