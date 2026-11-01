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

/** Комиссар уже проверил: список тот же, что у всех, но тапы молча игнорируются. */
export function nightLocked(details: MafiaDetails): boolean {
  return details.myRole === 'Commissar' && details.myNightTarget !== null
}
