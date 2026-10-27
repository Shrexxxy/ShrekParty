/**
 * Факты для очередного круга карусели — по кругу, без повторов внутри одного круга.
 */
export function factsForCycle<T>(facts: T[], cycle: number, perCycle = 2): T[] {
  if (facts.length === 0) {
    return []
  }

  const count = Math.min(perCycle, facts.length)
  const start = (cycle * perCycle) % facts.length
  return Array.from({ length: count }, (_, i) => facts[(start + i) % facts.length])
}
