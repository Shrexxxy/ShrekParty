import { ApiError } from '../api/http'

const MESSAGES: Record<string, string> = {
  already_active: 'Сначала заверши текущий ивент',
  not_active: 'Ивент не идёт',
  not_draft: 'Ивент уже начинался',
  not_complete: 'Ещё не всё доиграно',
  not_enough_players: 'Мало игроков',
  not_ready: 'Матч ещё не готов — нет обоих игроков',
  next_started: 'Следующий матч уже начался — отменить нельзя',
  already_decided: 'Решение уже принято',
  not_shooting: 'Съёмка не идёт',
  voting_started: 'Уже идёт голосование — удалять нельзя',
  not_voting: 'Голосование ещё не началось',
  not_a_player: 'Отмечать можно только игроков',
  not_green: 'Ещё не зелёный',
  already_green: 'Уже зелёный',
  already_tried: 'В этом раунде попытка уже была',
  bad_time: 'Неверное время',
  not_running: 'Забег ещё не начался',
  bad_count: 'Неверное число тапов',
  disqualified: 'Ты дисквалифицирован',
  bad_days: 'Число дней не может быть отрицательным',
  neuro_not_ready: 'Нужны нейрофото, и на каждом отмечено, кто это',
  in_game: 'Идёт игра — удалять нельзя',
  too_late: 'Время вышло',
  closed: 'Уже закрыто',
  end: 'Это последний шаг',
  unsupported_type: 'Такой ивент не поддерживается',
  bad_file: 'Файл не подходит — нужна картинка до 10 МБ',
  bad_message: 'Нужен текст и время от 1 до 600 секунд',
  balance: 'Не хватает осликов',
  limit: 'Не больше 25 осликов на матч',
  own_match: 'На свой матч ставить нельзя',
  already: 'Ты уже поставил на этот матч',
  bad_amount: 'Ставка — от 1 ослика',
  own_photo: 'Своё фото лайкать нельзя',
  not_yet: 'Лайкать можно, когда закончится съёмка',
}

/**
 * Понятный текст ошибки для админки.
 */
export function errorText(e: unknown): string {
  if (e instanceof ApiError) {
    if (e.code) {
      return MESSAGES[e.code] ?? `Ошибка: ${e.code}`
    }
    if (e.status === 403) {
      return 'Нет доступа'
    }
    if (e.status === 404) {
      return 'Не найдено'
    }
  }

  return 'Что-то сломалось, попробуй ещё раз'
}
