export interface TextPart {
  text: string
  login?: true
}

function escape(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/**
 * Режет текст на куски, помечая упомянутые логины — их выделяем цветом.
 */
export function highlightLogins(text: string, logins: string[]): TextPart[] {
  if (logins.length === 0) {
    return [{ text }]
  }

  // Длинные логины первыми: «ann» не должен откусить кусок от «anna».
  const pattern = new RegExp(`(${[...logins].sort((a, b) => b.length - a.length).map(escape).join('|')})`, 'g')
  return text
    .split(pattern)
    .filter((part) => part !== '')
    .map((part) => (logins.includes(part) ? { text: part, login: true as const } : { text: part }))
}
