/** Монета-ослик — валюта вечеринки: золотая монета с головой осла. */
export function DonkeyIcon({ size = '1.15em' }: { size?: string }) {
  return (
    <svg className="donkey-icon" viewBox="0 0 32 32" width={size} height={size} role="img" aria-label="ослики">
      <circle cx="16" cy="16" r="14.5" fill="#ffd166" stroke="#c99a2e" strokeWidth="2.5" />
      <circle cx="16" cy="16" r="10.8" fill="none" stroke="#c99a2e" strokeWidth="1" opacity="0.6" />
      <g fill="#0a1824" opacity="0.82">
        <ellipse cx="12.2" cy="10.4" rx="2.2" ry="5.2" transform="rotate(-18 12.2 10.4)" />
        <ellipse cx="19.8" cy="10.4" rx="2.2" ry="5.2" transform="rotate(18 19.8 10.4)" />
        <ellipse cx="16" cy="17.6" rx="5.6" ry="5.8" />
        <ellipse cx="16" cy="22.2" rx="4.3" ry="3.2" />
      </g>
      <g fill="#ffd166">
        <circle cx="13.7" cy="16.8" r="0.9" />
        <circle cx="18.3" cy="16.8" r="0.9" />
        <circle cx="14.5" cy="22.6" r="0.7" />
        <circle cx="17.5" cy="22.6" r="0.7" />
      </g>
    </svg>
  )
}

/** Число осликов с монетой. */
export function Donkeys({ value }: { value: number }) {
  return (
    <span className="donkeys">
      {value} <DonkeyIcon />
    </span>
  )
}
