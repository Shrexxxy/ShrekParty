/**
 * Сколько осталось до дедлайна по часам сервера: телефон может спешить или отставать,
 * поэтому считаем от времени сервера в снапшоте плюс сколько прошло локально с его прихода.
 */
export function remainingMs(deadlineIso: string, serverTimeIso: string, receivedAtLocalMs: number, nowLocalMs: number): number {
  const serverNow = Date.parse(serverTimeIso) + (nowLocalMs - receivedAtLocalMs)
  return Math.max(0, Date.parse(deadlineIso) - serverNow)
}

/** м:сс, секунды округляются вверх — «0:01» до последнего мгновения. */
export function formatClock(ms: number): string {
  const total = Math.ceil(ms / 1000)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}
