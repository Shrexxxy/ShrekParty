/** Вопрос в редакторе — зеркало QuizQuestionDto бэкенда. */
export interface QuizQuestionDto {
  id: string
  order: number
  price: number
  text: string
  answer: string
  mediaUrl: string | null
  mediaType: 'Image' | 'Audio' | 'Video' | null
}

/** Тема в редакторе; раунд 0 — финал. */
export interface QuizThemeDto {
  id: string
  round: number
  order: number
  name: string
  questions: QuizQuestionDto[]
}

/** Номер раунда финала. */
export const FINAL_ROUND = 0

/** Пример JSON для импорта — показываем в редакторе. */
export const IMPORT_EXAMPLE = `{
  "themes": [
    { "round": 1, "name": "Шрек", "questions": [
      { "text": "Как зовут осла?", "answer": "Осёл" },
      { "text": "Кто жена Шрека?", "answer": "Фиона", "price": 250 }
    ] },
    { "round": 0, "name": "Финал", "questions": [
      { "text": "Финальный вопрос", "answer": "Ответ" }
    ] }
  ]
}`

/** Темы по раундам (по порядку) и финал отдельно. */
export function byRounds(themes: QuizThemeDto[]): { rounds: { round: number; themes: QuizThemeDto[] }[]; final: QuizThemeDto | null } {
  const regular = themes.filter((t) => t.round !== FINAL_ROUND)
  const numbers = [...new Set(regular.map((t) => t.round))].sort((a, b) => a - b)
  return {
    rounds: numbers.map((round) => ({ round, themes: regular.filter((t) => t.round === round).sort((a, b) => a.order - b.order) })),
    final: themes.find((t) => t.round === FINAL_ROUND) ?? null,
  }
}

/** Номер для нового раунда — следующий после последнего. */
export function nextRound(themes: QuizThemeDto[]): number {
  return Math.max(0, ...themes.map((t) => t.round)) + 1
}
