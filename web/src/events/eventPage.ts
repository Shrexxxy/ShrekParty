import type { ActiveEventDto, EventDto } from '../api/types'

export type EventPageState = 'live' | 'finished' | 'upcoming' | 'unknown' | 'loading'

/**
 * Что показать на странице ивента: идущий — по снапшоту; прошедший, будущий или чужой адрес — по программе
 * (пока она не загрузилась — ждём, а не «нет такого»). Ивенты без осликов в программу не попадают —
 * если страница видела такой идущим (`wasLive`), значит, он закончился.
 */
export function eventPageState(
  id: string,
  active: ActiveEventDto | null | undefined,
  events: EventDto[] | null,
  wasLive = false,
): EventPageState {
  if (active?.id === id) {
    return 'live'
  }

  if (events === null) {
    return 'loading'
  }

  const event = events.find((e) => e.id === id)
  return !event ? (wasLive ? 'finished' : 'unknown') : event.status === 'Finished' ? 'finished' : event.status === 'Draft' ? 'upcoming' : 'live'
}
