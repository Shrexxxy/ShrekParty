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

export type QuizPhase =
  | 'Roulette'
  | 'Choosing'
  | 'Reading'
  | 'Answering'
  | 'Closed'
  | 'Done'
  | 'FinalBetting'
  | 'FinalQuestion'
  | 'FinalJudging'
  | 'FinalReveal'

export interface QuizScore {
  userId: string
  login: string
  avatarUrl: string | null
  score: number
}

export interface QuizFinalist {
  userId: string
  login: string
  avatarUrl: string | null
  scoreBefore: number
  /** Чужие — только админу и с раскрытия. */
  bet: number | null
  answer: string | null
  correct: boolean | null
}

export interface QuizDetails {
  phase: QuizPhase
  round: number
  rounds: number[]
  board: { themeId: string; name: string; cells: { questionId: string; price: number; played: boolean }[] }[]
  chooserId: string | null
  deadline: string | null
  question: {
    id: string
    themeName: string
    price: number
    text: string
    mediaType: 'Image' | 'Audio' | 'Video' | null
    /** Аудио/видео — только ТВ и админу; картинка — всем. */
    mediaUrl: string | null
  } | null
  readingStartedAt: string | null
  answererId: string | null
  wrong: string[]
  /** Админу всегда, остальным — после закрытия вопроса. */
  answer: string | null
  scores: QuizScore[]
  mediaCommand: { action: 'Play' | 'Pause'; seq: number } | null
  final: { themeName: string; finalists: QuizFinalist[]; iAmFinalist: boolean; myBet: number | null; myAnswer: string | null } | null
}

export interface AuctionLot {
  /** Приз за босса; null — финальный аукцион. */
  boss?: import('./boss').BossKind | null
  medal?: import('./boss').Medal | null
  id: string
  order: number
  name: string
  startPrice: number
  photoUrl: string | null
}

export interface AuctionDetails {
  phase: 'Idle' | 'Bidding' | 'Sold' | 'Unsold' | 'Done'
  lot: AuctionLot | null
  price: number | null
  leader: { userId: string; login: string; avatarUrl: string | null } | null
  deadline: string | null
  left: number
  last: { lotName: string; login: string | null; price: number | null; sold: boolean } | null
  balances: { userId: string; login: string; avatarUrl: string | null; balance: number }[]
  myBalance: number | null
  sales: { lotName: string; login: string; price: number }[]
}

export interface MafiaDetails {
  phase: import('./mafia').MafiaPhase
  day: number
  /** Номер перехода — пульт шлёт его с «Дальше». */
  step: number
  deadline: string | null
  players: { userId: string; login: string; avatarUrl: string | null; alive: boolean; role: import('./mafia').MafiaRole | null }[]
  myRole: import('./mafia').MafiaRole | null
  iAmAlive: boolean
  partners: string[]
  myNightTarget: string | null
  /** Ведущему — все ходы, мафии — ходы напарников. */
  nightActions: { userId: string; targetId: string }[]
  checks: { day: number; targetId: string; isMafia: boolean }[]
  lastHeal: string | null
  speakerId: string | null
  speechQueue: string[]
  votedCount: number
  iVoted: boolean
  /** После голосования — всем, ведущему — сразу. */
  votes: { voterId: string; targetId: string | null }[]
  eliminated: { userId: string; role: import('./mafia').MafiaRole } | null
  nightVictim: string | null
  winner: 'Civilians' | 'Mafia' | null
  myTasksDone: number
  /** Всего заданий этой ночью — для ТВ. */
  tasksTotal: number
  /** Задания по игрокам — только ведущему. */
  tasks: { userId: string; done: number }[]
  /** Копилка: игроку — своя, ведущему и в конце — все. */
  bonuses: { userId: string; kind: import('./mafia').MafiaBonusKind; count: number; amount: number }[]
  /** За победу каждому из команды. */
  winDonkeys: number
}
