import type { MafiaDetails } from './details'

export type MafiaRole = 'Mafia' | 'Doctor' | 'Commissar' | 'Civilian'
export type MafiaPhase = 'Intro' | 'Night' | 'Morning' | 'Speech' | 'Voting' | 'VoteResult' | 'LastWord' | 'Ended'

/** Роли по-русски. */
export const ROLE_NAMES: Record<MafiaRole, string> = {
  Mafia: 'Мафия',
  Doctor: 'Доктор',
  Commissar: 'Комиссар',
  Civilian: 'Мирный житель',
}

const TITLES: Record<MafiaPhase, string> = {
  Intro: 'Ночь знакомства 🌙',
  Night: 'Город засыпает… 🌙',
  Morning: 'Город просыпается ☀️',
  Speech: 'Обсуждение',
  Voting: 'Голосование',
  VoteResult: 'Итоги голосования',
  LastWord: 'Последнее слово',
  Ended: 'Игра окончена',
}

/** Заголовок фазы — ТВ показывает только текст, без звука. */
export function phaseTitle(phase: MafiaPhase): string {
  return TITLES[phase]
}

/** Голоса по игрокам (больше голосов — выше) и воздержавшиеся — для раскрытия на ТВ. */
export function voteTally(votes: { voterId: string; targetId: string | null }[]): {
  counts: { targetId: string; voters: string[] }[]
  abstained: string[]
} {
  const byTarget = new Map<string, string[]>()
  for (const v of votes) {
    if (v.targetId !== null) {
      byTarget.set(v.targetId, [...(byTarget.get(v.targetId) ?? []), v.voterId])
    }
  }

  return {
    counts: [...byTarget].map(([targetId, voters]) => ({ targetId, voters })).sort((a, b) => b.voters.length - a.voters.length),
    abstained: votes.filter((v) => v.targetId === null).map((v) => v.voterId),
  }
}

/** Кто я в партии: не за столом (пришёл после старта), выбыл или жив. */
export function myStatus(details: MafiaDetails): 'outside' | 'dead' | 'alive' {
  return !details.myRole ? 'outside' : details.iAmAlive ? 'alive' : 'dead'
}

/** Кого можно выбрать ночью: живые кроме себя, без напарника мафии и без вчерашнего пациента доктора — чтобы не ловить ошибку, которая выдаст роль. */
export function nightOptions(details: MafiaDetails, myId: string | undefined): MafiaDetails['players'] {
  return details.players.filter(
    (p) =>
      p.alive &&
      p.userId !== myId &&
      !(details.myRole === 'Mafia' && details.partners.includes(p.userId)) &&
      !(details.myRole === 'Doctor' && p.userId === details.lastHeal),
  )
}

export type MafiaBonusKind = 'Tasks' | 'Kill' | 'Check' | 'Save' | 'Oust'

const BONUS_NAMES: Record<MafiaBonusKind, string> = {
  Tasks: 'Задания',
  Kill: 'Убийства',
  Check: 'Проверки',
  Save: 'Спасения',
  Oust: 'Посадки',
}

const BONUS_ORDER: MafiaBonusKind[] = ['Tasks', 'Kill', 'Check', 'Save', 'Oust']

export interface BonusRow {
  userId: string
  login: string
  role: MafiaRole | null
  /** За победу команды (0 — проиграл или победителя ещё нет). */
  win: number
  /** «Убийства: 2 × 10 = 20». */
  lines: string[]
  total: number
}

/** Кто за что сколько получил за партию: по игроку, больше — выше. Видны только те бонусы, что прислал сервер. */
export function bonusSheet(details: MafiaDetails): BonusRow[] {
  return details.players
    .map((p) => {
      const mine = details.bonuses.filter((b) => b.userId === p.userId).sort((a, b) => BONUS_ORDER.indexOf(a.kind) - BONUS_ORDER.indexOf(b.kind))
      const team = p.role === 'Mafia' ? 'Mafia' : 'Civilians'
      const win = details.winner !== null && p.role !== null && details.winner === team ? details.winDonkeys : 0
      return {
        userId: p.userId,
        login: p.login,
        role: p.role,
        win,
        lines: mine.map((b) => `${BONUS_NAMES[b.kind]}: ${b.count} × ${b.amount / b.count} = ${b.amount}`),
        total: win + mine.reduce((sum, b) => sum + b.amount, 0),
      }
    })
    .sort((a, b) => b.total - a.total)
}
