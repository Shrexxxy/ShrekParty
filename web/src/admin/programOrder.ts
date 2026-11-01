import type { EventType, ProgramItemDto } from '../api/types'

/** Названия типов в админке. */
export const EVENT_TYPE_LABELS: Partial<Record<EventType, string>> = {
  Tournament: 'The Avatar Fighting Game',
  Laugh: 'Смехлыст',
  HideAndSeek: 'Прятки',
  PhotoHunt: 'Фотоохота',
  Reaction: 'Реакция Шрека',
  Tapper: 'Тапалка с драконом',
  Days: 'Сколько мне дней',
  NeuroPhoto: 'Угадай по нейрофото',
  Quiz: 'Своя игра',
  Mafia: 'Мафия',
  BossFight: 'Битва с боссом',
  Ceremony: 'Церемония',
  Auction: 'Аукцион',
}

/**
 * Следующий по программе — первый черновик по порядку; пока что-то идёт, следующего нет.
 */
export function nextDraft(items: ProgramItemDto[]): ProgramItemDto | null {
  if (items.some((i) => i.status === 'Active')) {
    return null
  }

  return [...items].sort((a, b) => a.order - b.order).find((i) => i.status === 'Draft') ?? null
}

/**
 * Позиция для перестановки на шаг вверх (-1) или вниз (+1); null — уже край.
 */
export function moveOrder(items: ProgramItemDto[], id: string, delta: -1 | 1): number | null {
  const sorted = [...items].sort((a, b) => a.order - b.order)
  const index = sorted.findIndex((i) => i.id === id)
  const target = index + delta
  return index < 0 || target < 0 || target >= sorted.length ? null : target
}
