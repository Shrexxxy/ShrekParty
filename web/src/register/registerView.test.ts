import { describe, expect, it } from 'vitest'
import { registerView } from './registerView'

describe('registerView', () => {
  it('нет данных — подключаемся; до открытия — только ожидание; идёт — форма; закрыта — форма для опоздавших и вход', () => {
    expect(registerView(undefined)).toEqual({ connecting: true, waiting: false, form: false, loginLink: false })
    expect(registerView('Closed')).toEqual({ connecting: false, waiting: true, form: false, loginLink: false })
    expect(registerView('Open')).toEqual({ connecting: false, waiting: false, form: true, loginLink: false })
    expect(registerView('Finished')).toEqual({ connecting: false, waiting: false, form: true, loginLink: true })
  })
})
