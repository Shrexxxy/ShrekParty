// Зеркало контрактов бэкенда. Enum'ы приходят строками.

export type UserRole = 'Player' | 'Admin' | 'Viewer'
export type RegistrationState = 'Closed' | 'Open' | 'Finished'
export type EventType =
  | 'Registration'
  | 'FightPoll'
  | 'Tournament'
  | 'Tote'
  | 'Laugh'
  | 'HideAndSeek'
  | 'PhotoHunt'
  | 'Ceremony'
export type EventStatus = 'Draft' | 'Active' | 'Finished'
export type ScoringScheme = 'Linear' | 'Podium' | 'Custom'

export interface MeDto {
  id: string
  login: string
  role: UserRole
  avatarUrl: string | null
}

export interface LeaderboardRowDto {
  userId: string
  login: string
  avatarUrl: string | null
  donkeys: number
  place: number
}

export interface EventDto {
  id: string
  type: EventType
  title: string
  status: EventStatus
  maxDonkeys: number
}

export interface RegistrationStatusDto {
  state: RegistrationState
  openedAt: string | null
  waitingCount: number
  closedAt: string | null
}

export interface ActiveEventDto {
  id: string
  type: EventType
  title: string
  startedAt: string | null
  /** Своя форма у каждого типа — см. модули ивентов. */
  details: unknown
}

export interface TvMessageDto {
  text: string
  imageUrl: string | null
  until: string
}

export interface AppSnapshot {
  registration: RegistrationStatusDto
  leaderboard: LeaderboardRowDto[]
  serverTime: string
  activeEvent?: ActiveEventDto | null
  tvMessage?: TvMessageDto | null
}

export interface ProgramItemDto {
  id: string
  type: EventType
  title: string
  status: EventStatus
  order: number
  maxDonkeys: number
  scheme: ScoringScheme
}

export interface FactDto {
  text: string
  logins: string[]
}

export interface RegistrationResultRowDto {
  userId: string
  login: string
  avatarUrl: string | null
  /** null — опоздал. */
  place: number | null
  speedPlace: number | null
  elapsedSeconds: number
  passwordScore: number
  passwordGuessesLog10: number
  speedDonkeys: number
  passwordDonkeys: number
  totalDonkeys: number
  isLate: boolean
}
