import type { MatchDto, TournamentDetails, TournamentRound } from './details'

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
/** Текущий матч на ТВ: тот, что сейчас играют, а если никто не играет — следующий, на который открыты ставки. */
export function currentMatchId(details: TournamentDetails): string | null {
  return details.matches.find((m) => m.status === 'Live')?.id ?? details.betMatchId
}

/** Своя ставка в блоке «Мои ставки». */
export interface MyBet {
  matchId: string
  round: TournamentRound
  on: string
  amount: number
  result: 'pending' | 'won' | 'lost'
}

/** Мои ставки по порядку сетки: на кого, сколько и чем кончилось. */
export function myBets(details: TournamentDetails): MyBet[] {
  return [...details.matches]
    .sort((a, b) => a.index - b.index)
    .flatMap((m) => {
      if (!m.myBet) {
        return []
      }
      const on = [m.playerA, m.playerB].find((p) => p?.id === m.myBet!.onPlayerId)?.login ?? '…'
      const result = m.status !== 'Done' || !m.winnerId ? 'pending' : m.winnerId === m.myBet.onPlayerId ? 'won' : 'lost'
      return [{ matchId: m.id, round: m.round, on, amount: m.myBet.amount, result }]
    })
}

/** Где карточка матча внутри сетки (px). */
export interface MatchBox {
  left: number
  top: number
  right: number
  bottom: number
}

/**
 * Линии сетки: от правого края матча к левому краю матча, куда проходит победитель —
 * вправо до середины промежутка, по вертикали, вправо. Матча нет на экране — линии нет.
 */
export function bracketLines(matches: MatchDto[], boxes: Map<number, MatchBox>): { key: string; d: string }[] {
  return matches.flatMap((m) => {
    const from = boxes.get(m.index)
    const to = m.nextIndex === null ? undefined : boxes.get(m.nextIndex)
    if (!from || !to) {
      return []
    }

    const y1 = (from.top + from.bottom) / 2
    const y2 = (to.top + to.bottom) / 2
    const mid = (from.right + to.left) / 2
    return [{ key: `${m.index}-${m.nextIndex}`, d: `M ${from.right} ${y1} H ${mid} V ${y2} H ${to.left}` }]
  })
}

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
