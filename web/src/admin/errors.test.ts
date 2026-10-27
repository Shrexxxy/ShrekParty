import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/http'
import { errorText } from './errors'

describe('errorText', () => {
  it('известный код — по-русски', () => {
    expect(errorText(new ApiError(409, { error: 'already_active' }))).toBe('Сначала заверши текущий ивент')
    expect(errorText(new ApiError(409, { error: 'not_complete' }))).toBe('Ещё не всё доиграно')
  })

  it('неизвестный код — сам код, чтобы было что гуглить', () => {
    expect(errorText(new ApiError(409, { error: 'weird' }))).toBe('Ошибка: weird')
  })

  it('без кода — по статусу', () => {
    expect(errorText(new ApiError(403, null))).toBe('Нет доступа')
    expect(errorText(new Error('boom'))).toBe('Что-то сломалось, попробуй ещё раз')
  })
})
