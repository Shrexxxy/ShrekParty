/** Пути к сгенерированным картинкам (web/public/assets). Нет файла — компоненты показывают запасной вариант. */
export const ASSETS = {
  game: {
    background: '/assets/game/swamp-bg.jpg',
    body: '/assets/game/ogre-body.png',
    /** Кисть: предплечье тянется полосой, кисть не искажается. */
    hand: '/assets/game/ogre-hand.png',
    basket: '/assets/game/basket.png',
    onion: '/assets/game/onion.png',
  },
  tapper: {
    dragonUp: '/assets/tapper/dragon-up.png',
    dragonDown: '/assets/tapper/dragon-down.png',
    sky: '/assets/tapper/sky.jpg',
  },
  ui: {
    donkey: '/assets/ui/donkey.png',
    defaultAvatar: '/assets/ui/avatar-default.png',
    /** Общее фото компании — фон страницы регистрации. */
    registerBackground: '/assets/ui/register-bg.jpg',
  },
  /** Клетка противной капчи: /assets/captcha/{id}.jpg */
  captcha: (id: string) => `/assets/captcha/${id}.jpg`,
} as const
