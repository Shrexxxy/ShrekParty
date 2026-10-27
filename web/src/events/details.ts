// Формы activeEvent.details по типам ивентов — зеркало DTO бэкенда.

export interface PollDetails {
  options: { name: string; votes: number }[]
  myVote: string | null
  closed: boolean
  winner: string | null
  tie: boolean
}

export type TournamentRound = 'Qualifier' | 'Quarter' | 'Semi' | 'ThirdPlace' | 'Final' | 'Challenge'
export type MatchStatus = 'Pending' | 'Live' | 'Done'
export type ChallengeState = 'None' | 'Offered' | 'Declined' | 'Accepted'

export interface TournamentPlayer {
  id: string
  login: string
  avatarUrl: string | null
  isAdmin: boolean
}

export interface MatchDto {
  id: string
  index: number
  round: TournamentRound
  roundIndex: number
  playerA: TournamentPlayer | null
  playerB: TournamentPlayer | null
  isBye: boolean
  status: MatchStatus
  winnerId: string | null
  score: string | null
  photos: number
  pool: number
  oddsA: number | null
  oddsB: number | null
  myBet: { onPlayerId: string; amount: number } | null
}

export interface TournamentDetails {
  game: string
  matches: MatchDto[]
  myNextMatchId: string | null
  challenge: { state: ChallengeState; offeredToId: string | null; offeredToMe: boolean }
}

export interface LaughDetails {
  rows: { userId: string; login: string; avatarUrl: string | null; games: (number | null)[]; best: number | null }[]
}

export interface HideShotDto {
  id: string
  url: string
  userIds: string[]
  votes: number
}

export interface HideHitsDto {
  userId: string
  login: string
  avatarUrl: string | null
  hits: number
}

export interface HideDetails {
  phase: 'Hiding' | 'Shooting' | 'Voting'
  deadline: string
  /** Только админу. */
  players: HideHitsDto[]
  /** Админу всегда, игрокам — с голосования. */
  shots: HideShotDto[]
  myVote: string | null
}

export interface HuntPhoto {
  userId: string
  login: string
  avatarUrl: string | null
  photoUrl: string
  likes: number
  likedByMe: boolean
}

export interface PhotoHuntDetails {
  task: string
  phase: 'Shooting' | 'Rating'
  deadline: string
  myPhotoUrl: string | null
  submitted: number
  total: number
  photos: HuntPhoto[]
}

export interface StandingDto {
  userId: string
  login: string
  avatarUrl: string | null
  donkeys: number
  place: number
  achievement: string
}

export interface CeremonyDetails {
  step: number
  totalSteps: number
  standings: StandingDto[]
}

export interface ReactionRow {
  userId: string
  login: string
  avatarUrl: string | null
  attempts: { round: number; ms: number | null; falseStart: boolean }[]
  best: number | null
}

export interface ReactionDetails {
  round: number
  totalRounds: number
  phase: 'Red' | 'Green' | 'Result'
  /** В Red — null: момент зелёного не раскрывается. */
  deadline: string | null
  rows: ReactionRow[]
  myAttempt: number | null
  myFalseStart: boolean
}

export interface TapperDetails {
  phase: 'Countdown' | 'Running'
  deadline: string
  rows: { userId: string; login: string; avatarUrl: string | null; taps: number }[]
}

export interface DaysDetails {
  phase: 'Answering' | 'Reveal'
  deadline: string
  myAnswer: number | null
  iAmDisqualified: boolean
  answered: number
  total: number
  disqualified: string[]
  /** Админу всегда, остальным — с раскрытия. */
  truth: number | null
  /** С раскрытия: от самого далёкого к самому близкому. */
  answers: { login: string; avatarUrl: string | null; days: number; diff: number }[]
}

export interface NeuroDetails {
  index: number
  total: number
  phase: 'Guessing' | 'Reveal'
  deadline: string
  photoUrl: string
  options: { userId: string; login: string; avatarUrl: string | null }[]
  myGuess: string | null
  guessed: number
  subjectId: string | null
  correct: string[]
}
