/** Шаги ставки после первой. */
export const BID_STEPS = [5, 10, 25] as const

/** «Раз… два…» под конец таймера торгов; «ПРОДАНО!» показывает фаза Sold. */
export function goingCall(msLeft: number): string | null {
  if (msLeft <= 0 || msLeft > 3000) {
    return null
  }

  return msLeft > 1500 ? 'Раз…' : 'Два…'
}

/** Кнопка ставки: шаг (0 — первая, по стартовой цене), итоговая сумма, можно ли нажать. */
export interface BidButton {
  step: number
  amount: number
  enabled: boolean
}

/**
 * Кнопки ставок: без ставок — «по стартовой», дальше +5/+10/+25; дороже «на руках» и у лидера — недоступны.
 */
export function bidButtons(lot: { price: number | null; startPrice: number; balance: number; leader: boolean }): BidButton[] {
  const options = lot.price === null ? [{ step: 0, amount: lot.startPrice }] : BID_STEPS.map((step) => ({ step, amount: lot.price! + step }))
  return options.map((o) => ({ ...o, enabled: !lot.leader && o.amount <= lot.balance }))
}
