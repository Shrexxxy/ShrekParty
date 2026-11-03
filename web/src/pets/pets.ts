import type { PetClass, PetDto } from '../api/types'
import { actionText, type FighterAction } from '../events/boss'
import type { IconName } from '../components/Icon'

/** Классы по-русски. */
export const CLASS_NAMES: Record<PetClass, string> = {
  Tank: 'Танк',
  Fighter: 'Боец',
  Mage: 'Маг',
}

/** Иконка класса — везде одна и та же, как и его цвет (класс CSS `class-<имя>`). */
export const CLASS_ICONS: Record<PetClass, IconName> = {
  Tank: 'shield',
  Fighter: 'sword',
  Mage: 'spark',
}

/** Картинка питомца: свой рисунок внутри класса. */
export function petArt(pet: Pick<PetDto, 'class' | 'variant'>): string {
  return `/assets/pets/${pet.class.toLowerCase()}-${pet.variant}.png`
}

/** Яйцо того же цвета, что и питомец из него. */
export function eggArt(pet: PetDto): string {
  return `/assets/pets/egg-${pet.class.toLowerCase()}-${pet.variant}.png`
}

/** Тапов, чтобы вылупился, — столько же ждёт сервер. */
export const EGG_TAPS = 15

const XP_PER_LEVEL = 50

/** Характеристики по-русски, в порядке показа. */
export const STATS = [
  { key: 'strength', icon: 'strength', name: 'сила' },
  { key: 'agility', icon: 'agility', name: 'ловкость' },
  { key: 'intellect', icon: 'spark', name: 'интеллект' },
] as const satisfies readonly { key: string; icon: IconName; name: string }[]

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

/** «Дракоша Кот вырос до 4 уровня! +2 интеллект, +1 сила»; не вырос — null. */
export function levelUpText(prev: PetDto, next: PetDto): string | null {
  if (next.level <= prev.level) {
    return null
  }

  const gains = STATS.map((s) => ({ ...s, gain: next[s.key] - prev[s.key] }))
    .filter((s) => s.gain > 0)
    .sort((a, b) => b.gain - a.gain)
  return `${next.name} вырос до ${next.level} уровня! ${gains.map((g) => `+${g.gain} ${g.name}`).join(', ')}`
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

/** Скилл в гайде на вкладке питомца: картинка по action, число — для своего питомца (null — без числа). */
export interface SkillGuide {
  action: FighterAction
  name: string
  amount: number | null
  /** Цена в мане словами; null — бесплатно. */
  cost: string | null
  text: string
}

const CLASS_SKILLS: Record<PetClass, FighterAction[]> = {
  Tank: ['Attack', 'Dodge', 'Cover'],
  Fighter: ['Attack', 'Dodge', 'Power'],
  Mage: ['Attack', 'Dodge', 'Fire', 'Heal'],
}

/**
 * Что умеет питомец в бою с боссом. Числа считаются так же, как на сервере (BossRules.Amount),
 * но без поддержки с того света — она бывает только в бою.
 */
export function skillGuide(pet: Pick<PetDto, 'class' | 'damage' | 'intellect'>): SkillGuide[] {
  return CLASS_SKILLS[pet.class].map((action) => {
    const name = actionText(action, null).name
    switch (action) {
      case 'Attack':
        return { action, name, amount: pet.damage, cost: null, text: `Бьёт босса копытом на ${pet.damage}. Обычный удар, есть у всех.` }
      case 'Dodge':
        return {
          action,
          name,
          amount: null,
          cost: null,
          text: 'Удар босса по тебе пролетит мимо: и одиночный, и стрелы со звёздами. Два хода подряд уворачиваться нельзя.',
        }
      case 'Cover':
        return {
          action,
          name,
          amount: null,
          cost: null,
          text:
            'Закрываешь команду собой. Удар по одному или двоим целиком уходит на тебя, но вдвое слабее; несколько танков делят его поровну. ' +
            'Удар по всем (стрелы, звёзды) ты ловишь щитом: свою и ещё одну за самого раненого, и тебе не больно.',
        }
      case 'Power':
        return {
          action,
          name,
          amount: 2 * pet.damage,
          cost: null,
          text: `Разбег и удар вдвое сильнее обычного: ${2 * pet.damage}. Пока разбегаешься, увернуться в этот ход нельзя.`,
        }
      case 'Fire':
        return { action, name, amount: 3 * pet.intellect, cost: '4 маны', text: `Огненный шар в босса: ${3 * pet.intellect} урона (интеллект × 3).` }
      case 'Heal':
        return {
          action,
          name,
          amount: 2 * pet.intellect,
          cost: '3 маны',
          text: `Луковое облако лечит любого союзника или тебя самого на ${2 * pet.intellect} (интеллект × 2).`,
        }
    }
  })
}
