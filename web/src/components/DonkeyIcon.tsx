import { ASSETS } from '../assets'
import { Sprite } from './Sprite'

/** Иконка осла — валюта вечеринки. */
export function DonkeyIcon({ size = '1em' }: { size?: string }) {
  return (
    <Sprite
      className="donkey-icon"
      src={ASSETS.ui.donkey}
      fallback="🫏"
      alt="ослики"
      style={{ fontSize: size, width: '1.2em', height: '1.2em', verticalAlign: '-0.2em' }}
    />
  )
}

/** Число осликов с иконкой. */
export function Donkeys({ value }: { value: number }) {
  return (
    <span className="donkeys">
      {value} <DonkeyIcon />
    </span>
  )
}
