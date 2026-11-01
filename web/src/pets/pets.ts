import type { PetClass, PetDto } from '../api/types'

/** Классы по-русски. */
export const CLASS_NAMES: Record<PetClass, string> = {
  Tank: '🛡 Танк',
  Fighter: '⚔️ Боец',
  Mage: '✨ Маг',
}

/** Тапов, чтобы вылупился, — столько же ждёт сервер. */
export const EGG_TAPS = 15

const XP_PER_LEVEL = 50

/** Характеристики по-русски, в порядке показа. */
export const STATS = [
  { key: 'strength', icon: '💪', name: 'сила' },
  { key: 'agility', icon: '🏹', name: 'ловкость' },
  { key: 'intellect', icon: '✨', name: 'интеллект' },
] as const

/** Стадия яйца: 0 — целое, 1 и 2 — трещины (5 и 10 тапов), 3 — вылупился. */
export function eggStage(taps: number): 0 | 1 | 2 | 3 {
  return taps >= EGG_TAPS ? 3 : taps >= 10 ? 2 : taps >= 5 ? 1 : 0
}

/** Сколько осликов до следующего уровня и сколько процентов уровня пройдено. */
export function xpProgress(pet: PetDto): { left: number; percent: number } {
  const levelStart = (pet.level - 1) * XP_PER_LEVEL
  return {
    left: pet.nextLevelAt - pet.earned,
    percent: Math.round(((pet.earned - levelStart) / XP_PER_LEVEL) * 100),
  }
}

/** «Дракоша Кот вырос до 4 уровня! +2 ✨ интеллект, +1 💪 сила»; не вырос — null. */
export function levelUpText(prev: PetDto, next: PetDto): string | null {
  if (next.level <= prev.level) {
    return null
  }

  const gains = STATS.map((s) => ({ ...s, gain: next[s.key] - prev[s.key] }))
    .filter((s) => s.gain > 0)
    .sort((a, b) => b.gain - a.gain)
  return `${next.name} вырос до ${next.level} уровня! ${gains.map((g) => `+${g.gain} ${g.icon} ${g.name}`).join(', ')}`
}

/** Начался новый ивент — телефон сам возвращается на «Игру», чтобы гость его не пропустил. */
export function switchToGame(prevEventId: string | null, nextEventId: string | null): boolean {
  return nextEventId !== null && nextEventId !== prevEventId
}

/** Видел ли этот игрок вкладку питомца (своя метка у каждого — на одном телефоне могут играть двое). */
export function petOpened(userId: string): boolean {
  try {
    return localStorage.getItem(`shrek.petOpened.${userId}`) === '1'
  } catch {
    return false
  }
}

export function markPetOpened(userId: string): void {
  try {
    localStorage.setItem(`shrek.petOpened.${userId}`, '1')
  } catch {
    // Приватный режим — уведомление просто покажется ещё раз.
  }
}

const SEEN_KEY = 'shrek.pet'

/** Каким питомец был, когда игрок последний раз видел, — для «вырос до N уровня» (удобство, не состояние). */
export function lastSeenPet(): PetDto | null {
  try {
    const raw = localStorage.getItem(SEEN_KEY)
    return raw ? (JSON.parse(raw) as PetDto) : null
  } catch {
    return null
  }
}

export function rememberPet(pet: PetDto): void {
  try {
    localStorage.setItem(SEEN_KEY, JSON.stringify(pet))
  } catch {
    // Приватный режим — просто покажем всплывашку ещё раз.
  }
}
