/** Пути к сгенерированным картинкам (web/public/assets). Нет файла — компоненты показывают запасной вариант. */
export const ASSETS = {
  game: {
    background: '/assets/game/swamp-bg.jpg',
    body: '/assets/game/ogre-body.png',
    arm: '/assets/game/ogre-arm.png',
    basket: '/assets/game/basket.png',
    onion: '/assets/game/onion.png',
  },
  ui: {
    donkey: '/assets/ui/donkey.png',
    defaultAvatar: '/assets/ui/avatar-default.png',
  },
  /** Клетка противной капчи: /assets/captcha/{id}.jpg */
  captcha: (id: string) => `/assets/captcha/${id}.jpg`,
} as const
