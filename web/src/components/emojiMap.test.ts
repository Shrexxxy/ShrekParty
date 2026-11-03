import { describe, expect, it } from 'vitest'
import { emojiSrc, splitEmoji } from './emojiMap'

describe('emojiSrc', () => {
  it('эмодзи с картинкой — путь к иконке; вариант с U+FE0F тот же', () => {
    expect(emojiSrc('🏆')).toBe('/assets/icons/trophy.png')
    expect(emojiSrc('✏️')).toBe('/assets/icons/pencil.png')
    expect(emojiSrc('✏')).toBe('/assets/icons/pencil.png')
    expect(emojiSrc('🧅')).toBe('/assets/game/onion.png')
  })

  it('стрелки и неизвестное — без картинки', () => {
    expect(emojiSrc('→')).toBeNull()
    expect(emojiSrc('🦄')).toBeNull()
  })
})

describe('splitEmoji', () => {
  it('медаль с номером: иконка и число', () => {
    expect(splitEmoji('🏅3')).toEqual([{ emoji: '🏅', src: '/assets/icons/medal.png' }, '3'])
  })

  it('эмодзи в конце фразы и в середине', () => {
    expect(splitEmoji('Город засыпает… 🌙')).toEqual(['Город засыпает… ', { emoji: '🌙', src: '/assets/icons/moon.png' }])
    expect(splitEmoji('⏹ Завершить')).toEqual([{ emoji: '⏹', src: '/assets/icons/stop.png' }, ' Завершить'])
  })

  it('текст без известных эмодзи остаётся одной строкой', () => {
    expect(splitEmoji('Ход 3 → всех 🦄')).toEqual(['Ход 3 → всех 🦄'])
    expect(splitEmoji('')).toEqual([])
  })
})
