import type { RegistrationStatusDto } from '../api/types'

/**
 * Что показать на странице регистрации. До открытия — только ожидание (полей нет);
 * ссылка «Войти» — только после закрытия: ведущий и ТВ заходят по прямому адресу /login.
 */
export function registerView(state: RegistrationStatusDto['state'] | undefined): {
  connecting: boolean
  waiting: boolean
  form: boolean
  loginLink: boolean
} {
  return {
    connecting: state === undefined,
    waiting: state === 'Closed',
    form: state === 'Open' || state === 'Finished',
    loginLink: state === 'Finished',
  }
}
