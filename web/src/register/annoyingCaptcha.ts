/** «Противная» фейковая капча: три сетки 3×3, выбрать ровно правильные клетки. */

/** Картинка клетки: файл /assets/captcha/{id}.jpg, пока его нет — эмодзи. */
export interface CaptchaImage {
  id: string
  emoji: string
}

export interface CaptchaRound {
  prompt: string
  correct: CaptchaImage[]
  wrong: CaptchaImage[]
}

export interface CaptchaTile extends CaptchaImage {
  correct: boolean
}

const img = (id: string, emoji: string): CaptchaImage => ({ id, emoji })

export const ROUNDS: CaptchaRound[] = [
  {
    prompt: 'Выберите все квадраты с луком',
    correct: [img('onion-1', '🧅'), img('onion-2', '🧅'), img('onion-3', '🧅')],
    wrong: [
      img('garlic', '🧄'),
      img('potato', '🥔'),
      img('apple', '🍎'),
      img('pear', '🍐'),
      img('carrot', '🥕'),
      img('egg', '🥚'),
      img('lemon', '🍋'),
    ],
  },
  {
    prompt: 'Выберите всех, кто живёт в болоте',
    correct: [img('ogre', '🧌'), img('frog', '🐸'), img('crocodile', '🐊')],
    wrong: [
      img('lion', '🦁'),
      img('penguin', '🐧'),
      img('camel', '🐫'),
      img('giraffe', '🦒'),
      img('panda', '🐼'),
      img('koala', '🐨'),
      img('kangaroo', '🦘'),
    ],
  },
  {
    prompt: 'Выберите все квадраты с ОСЛОМ (не с лошадью!)',
    correct: [img('donkey-1', '🫏'), img('donkey-2', '🫏'), img('donkey-3', '🫏')],
    wrong: [
      img('horse-1', '🐴'),
      img('horse-2', '🐴'),
      img('horse-3', '🐴'),
      img('horse-4', '🐴'),
      img('zebra', '🦓'),
      img('unicorn', '🦄'),
    ],
  },
]

const WRONG_PER_GRID = 6

function shuffle<T>(items: T[], random: () => number): T[] {
  const result = [...items]
  for (let i = result.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1))
    ;[result[i], result[j]] = [result[j], result[i]]
  }
  return result
}

/** Сетка из 9 клеток: 3 правильных + 6 случайных неправильных, перемешано. */
export function shuffleRound(round: CaptchaRound, random: () => number): CaptchaTile[] {
  const wrong = shuffle(round.wrong, random).slice(0, WRONG_PER_GRID)
  return shuffle(
    [...round.correct.map((i) => ({ ...i, correct: true })), ...wrong.map((i) => ({ ...i, correct: false }))],
    random,
  )
}

/** Верно — только если выбраны все правильные и ни одной лишней. */
export function isCorrect(tiles: CaptchaTile[], selected: Set<number>): boolean {
  return tiles.every((tile, i) => tile.correct === selected.has(i))
}
