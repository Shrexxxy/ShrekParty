/** Скорость подбора: медленный хэш на сервере, как в «offline slow hashing» zxcvbn. */
const GUESSES_PER_SECOND = 1e4

const UNIVERSE_AGE_YEARS = 1.4e10

type Forms = [one: string, few: string, many: string]

/** Русская форма слова для числа: 1 секунда, 2 секунды, 5 секунд. */
export function plural(n: number, [one, few, many]: Forms): string {
  const mod10 = n % 10
  const mod100 = n % 100
  if (mod10 === 1 && mod100 !== 11) {
    return one
  }
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
    return few
  }
  return many
}

const MINUTE = 60
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR
const MONTH = 30 * DAY
const YEAR = 365 * DAY
const CENTURY = 100 * YEAR
const MILLENNIUM = 1000 * YEAR
const MILLION_YEARS = 1e6 * YEAR
const BILLION_YEARS = 1e9 * YEAR

const UNITS: [seconds: number, forms: Forms][] = [
  [BILLION_YEARS, ['миллиард лет', 'миллиарда лет', 'миллиардов лет']],
  [MILLION_YEARS, ['миллион лет', 'миллиона лет', 'миллионов лет']],
  [MILLENNIUM, ['тысячелетие', 'тысячелетия', 'тысячелетий']],
  [CENTURY, ['век', 'века', 'веков']],
  [YEAR, ['год', 'года', 'лет']],
  [MONTH, ['месяц', 'месяца', 'месяцев']],
  [DAY, ['день', 'дня', 'дней']],
  [HOUR, ['час', 'часа', 'часов']],
  [MINUTE, ['минута', 'минуты', 'минут']],
  [1, ['секунда', 'секунды', 'секунд']],
]

/**
 * «Взломают за …» по log10 числа попыток подбора.
 */
export function crackTimeText(guessesLog10: number): string {
  const seconds = 10 ** guessesLog10 / GUESSES_PER_SECOND
  if (seconds < 1) {
    return 'мгновенно'
  }
  if (seconds / YEAR > UNIVERSE_AGE_YEARS) {
    return 'дольше, чем живёт вселенная'
  }

  const [unit, forms] = UNITS.find(([size]) => seconds >= size)!
  const n = Math.round(seconds / unit)
  return `~${n} ${plural(n, forms)}`
}
