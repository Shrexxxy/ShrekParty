import { describe, expect, it } from 'vitest'
import { crackTimeText, plural } from './crackTime'

describe('plural', () => {
  it('русские формы', () => {
    const forms: [string, string, string] = ['секунда', 'секунды', 'секунд']
    expect(plural(1, forms)).toBe('секунда')
    expect(plural(2, forms)).toBe('секунды')
    expect(plural(5, forms)).toBe('секунд')
    expect(plural(11, forms)).toBe('секунд')
    expect(plural(21, forms)).toBe('секунда')
    expect(plural(24, forms)).toBe('секунды')
  })
})

describe('crackTimeText (10⁴ попыток в секунду)', () => {
  it('меньше секунды — мгновенно', () => {
    expect(crackTimeText(2)).toBe('мгновенно')
  })

  it('секунды', () => {
    expect(crackTimeText(Math.log10(3e4))).toBe('~3 секунды')
  })

  it('часы', () => {
    expect(crackTimeText(Math.log10(5 * 3600 * 1e4))).toBe('~5 часов')
  })

  it('годы', () => {
    expect(crackTimeText(Math.log10(21 * 365 * 86400 * 1e4))).toBe('~21 год')
  })

  it('века', () => {
    expect(crackTimeText(Math.log10(300 * 365 * 86400 * 1e4))).toBe('~3 века')
  })

  it('тысячелетия', () => {
    expect(crackTimeText(Math.log10(3300 * 365 * 86400 * 1e4))).toBe('~3 тысячелетия')
  })

  it('миллионы лет', () => {
    expect(crackTimeText(Math.log10(5.8e7 * 365 * 86400 * 1e4))).toBe('~58 миллионов лет')
  })

  it('миллиард лет', () => {
    expect(crackTimeText(Math.log10(1.2e9 * 365 * 86400 * 1e4))).toBe('~1 миллиард лет')
  })

  it('очень много — дольше жизни вселенной', () => {
    expect(crackTimeText(40)).toBe('дольше, чем живёт вселенная')
  })
})
