// Зеркало контрактов бэкенда. Enum'ы приходят строками.

export type UserRole = 'Player' | 'Admin' | 'Viewer'
export type RegistrationState = 'Closed' | 'Open' | 'Finished'
export type EventType = 'Registration'
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

export interface AppSnapshot {
  registration: RegistrationStatusDto
  leaderboard: LeaderboardRowDto[]
  serverTime: string
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
