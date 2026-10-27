/**
 * Уменьшает фото до maxSide по большей стороне и жмёт в JPEG: фото с телефонов весят мегабайты.
 */
export async function downscale(file: File, maxSide: number): Promise<Blob> {
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('toBlob'))), 'image/jpeg', 0.85),
  )
}

/** FormData с уменьшенным фото в поле file (+ дополнительные поля). */
export async function photoForm(file: File, maxSide: number, fields: Record<string, string> = {}): Promise<FormData> {
  const form = new FormData()
  form.append('file', await downscale(file, maxSide), 'photo.jpg')
  for (const [key, value] of Object.entries(fields)) {
    form.append(key, value)
  }
  return form
}
