/** Ошибка ответа API: статус и тело (если было JSON). */
export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, body: unknown) {
    super(`API ${status}`)
    this.status = status
    this.body = body
  }

  /** Код ошибки из тела `{ error: "..." }`, если он есть. */
  get code(): string | undefined {
    const body = this.body as { error?: unknown } | null
    return typeof body?.error === 'string' ? body.error : undefined
  }
}

/**
 * Запрос к API с cookie. Объект в `body` уходит JSON-ом, FormData — как есть.
 */
export async function api<T = void>(
  path: string,
  init: Omit<RequestInit, 'body'> & { body?: unknown } = {},
): Promise<T> {
  const { body, headers, ...rest } = init
  const isForm = body instanceof FormData
  const response = await fetch(path, {
    credentials: 'include',
    ...rest,
    headers: body !== undefined && !isForm ? { 'Content-Type': 'application/json', ...headers } : headers,
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  })

  const text = await response.text()
  const data: unknown = text ? safeJson(text) : undefined
  if (!response.ok) {
    throw new ApiError(response.status, data)
  }

  return data as T
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}
