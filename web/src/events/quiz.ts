/** Скорость «печатной машинки»: одна буква за 60 мс, как читает вслух именинник. */
export const MS_PER_CHAR = 60

/**
 * Лучшая оценка «часы сервера минус часы телефона» по нескольким снапшотам: задержка сети
 * только уменьшает разницу, поэтому самая большая — самая точная.
 */
export function bestOffset(samples: number[]): number {
  return samples.length === 0 ? 0 : Math.max(...samples)
}

/** Момент нажатия на часах сервера — его и сравнивает сервер. */
export function pressTime(localNowMs: number, offsetMs: number): string {
  return new Date(localNowMs + offsetMs).toISOString()
}

/** Сколько букв вопроса уже «напечатано» к моменту serverNowMs (первая — сразу). */
export function typedChars(text: string, readingStartedAtIso: string, serverNowMs: number, msPerChar = MS_PER_CHAR): number {
  const elapsed = Math.max(0, serverNowMs - Date.parse(readingStartedAtIso))
  return Math.min(text.length, Math.floor(elapsed / msPerChar) + 1)
}

/** Можно ли жать кнопку: идёт чтение и игрок ещё не ошибся на этом вопросе. */
export function canPress(details: { phase: string; wrong: string[]; answererId: string | null }, myId: string | undefined): boolean {
  return myId !== undefined && details.phase === 'Reading' && !details.wrong.includes(myId)
}

/** Подпись клетки для подтверждения: «Шрек» за 300. */
export function cellLabel(themeName: string, price: number): string {
  return `«${themeName}» за ${price}`
}

/**
 * Кадры рулетки: игроки по кругу, последний кадр — выбранный сервером.
 */
export function rouletteFrames(playerIds: string[], chooserId: string, count: number): string[] {
  const index = playerIds.indexOf(chooserId)
  if (index < 0) {
    return [chooserId]
  }

  const n = playerIds.length
  return Array.from({ length: count }, (_, i) => playerIds[(((index - (count - 1 - i)) % n) + n) % n])
}
