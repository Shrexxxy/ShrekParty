const PODIUM = ['🥇', '🥈', '🥉']

/**
 * Медалька за скорость регистрации: 🥇🥈🥉, дальше 🏅 с номером, последнему — 🐢.
 */
export function medalFor(speedPlace: number, lastSpeedPlace: number): string {
  if (lastSpeedPlace >= 2 && speedPlace === lastSpeedPlace) {
    return '🐢'
  }

  return speedPlace <= PODIUM.length ? PODIUM[speedPlace - 1] : `🏅${speedPlace}`
}
