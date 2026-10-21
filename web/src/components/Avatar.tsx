import { ASSETS } from '../assets'
import { Sprite } from './Sprite'

/** Кружок с аватаркой; без фото — дефолтный осёл. */
export function Avatar({ url, login, size = 40 }: { url: string | null; login: string; size?: number }) {
  return (
    <span className="avatar" style={{ width: size, height: size, fontSize: size * 0.6 }} title={login}>
      {url ? <img src={url} alt={login} /> : <Sprite src={ASSETS.ui.defaultAvatar} fallback="🫏" />}
    </span>
  )
}
