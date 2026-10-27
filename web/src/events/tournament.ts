import type { MatchDto, TournamentRound } from './details'

const COLUMNS: { title: string; rounds: TournamentRound[] }[] = [
  { title: 'Отбор', rounds: ['Qualifier'] },
  { title: '1/4', rounds: ['Quarter'] },
  { title: '1/2', rounds: ['Semi'] },
  { title: 'Финал', rounds: ['Final', 'ThirdPlace'] },
  { title: 'Вызов имениннику', rounds: ['Challenge'] },
]

export interface BracketColumn {
  title: string
  matches: MatchDto[]
}

/**
 * Колонки сетки слева направо; матч за 3-е — под финалом; пустые раунды не показываем.
 */
export function bracketColumns(matches: MatchDto[]): BracketColumn[] {
  return COLUMNS.map((column) => ({
    title: column.title,
    matches: column.rounds.flatMap((round) =>
      matches.filter((m) => m.round === round).sort((a, b) => a.roundIndex - b.roundIndex),
    ),
  })).filter((c) => c.matches.length > 0)
}

/** Коэффициент тотализатора для кнопки ставки. */
export function oddsText(odds: number | null): string {
  return odds === null ? '—' : `×${Math.round(odds * 10) / 10}`
}

/** Подписи раундов в карточке матча. */
export const ROUND_TITLES: Record<TournamentRound, string> = {
  Qualifier: 'Отбор',
  Quarter: '1/4',
  Semi: '1/2',
  ThirdPlace: 'За 3-е место',
  Final: 'Финал',
  Challenge: 'Вызов имениннику',
}
