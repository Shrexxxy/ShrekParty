import type { ActiveEventDto } from '../api/types'
import type { HideDetails } from './details'

/**
 * Прятки в темноте: пока прячутся и идёт съёмка, телефоны игроков не должны светиться.
 */
export function isDarkPhase(event: ActiveEventDto | null | undefined): boolean {
  if (event?.type !== 'HideAndSeek') {
    return false
  }

  const phase = (event.details as HideDetails).phase
  return phase === 'Hiding' || phase === 'Shooting'
}
