/** Приветствия на ТВ. `{name}` заменяется логином. Фразы без рода — не знаем, кто он, а кто она. */

export const FIRST_PHRASES = ['Ого, ты быстр, {name}! Первый на болоте!', '{name} дышит в спину лидеру!']

export const MIDDLE_PHRASES = [
  'Привет, {name}!',
  '{name} вылезает из болота!',
  'Осёл одобряет: {name} в деле!',
  'Добро пожаловать в болото, {name}!',
  '{name}: проверка на огра пройдена',
  'Ещё один огр на борту: {name}!',
  '{name}: все печеньки пойманы!',
  'Кот в сапогах снимает шляпу перед {name}',
  'Фиона машет тебе, {name}!',
  '{name}, луковые слои на месте!',
  'Дракон заметил {name}!',
]

/** Предпоследнему и последнему — известны только при закрытии регистрации. */
export const CLOSING_PHRASES = ['{name} — предпоследний. Не самый медленный!', 'А {name} у нас последний 🐢']

function fill(phrase: string, name: string): string {
  return phrase.replace('{name}', name)
}

/**
 * Фраза для nth-го зарегистрировавшегося (с 1). `used` — индексы уже показанных средних фраз.
 */
export function greetingFor(name: string, nth: number, used: number[], random: () => number): { text: string; used: number[] } {
  if (nth <= FIRST_PHRASES.length) {
    return { text: fill(FIRST_PHRASES[nth - 1], name), used }
  }

  const pool = used.length >= MIDDLE_PHRASES.length ? [] : used
  const available = MIDDLE_PHRASES.map((_, i) => i).filter((i) => !pool.includes(i))
  const index = available[Math.min(available.length - 1, Math.floor(random() * available.length))]
  return { text: fill(MIDDLE_PHRASES[index], name), used: [...pool, index] }
}

/**
 * Фразы при закрытии: предпоследнему и последнему по скорости. Меньше трёх игроков — без них.
 */
export function closingGreetings(loginsBySpeed: string[]): string[] {
  if (loginsBySpeed.length < 3) {
    return []
  }

  const [penultimate, last] = loginsBySpeed.slice(-2)
  return [fill(CLOSING_PHRASES[0], penultimate), fill(CLOSING_PHRASES[1], last)]
}
