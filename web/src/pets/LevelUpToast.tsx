import { useEffect, useState } from 'react'
import type { PetDto } from '../api/types'
import { lastSeenPet, levelUpText, rememberPet } from './pets'
import { Emoji } from '../components/Emoji'

/** «Дракоша вырос до N уровня!» — один раз на рост, закрывается тапом. */
export function LevelUpToast({ pet }: { pet: PetDto }) {
  const [prev, setPrev] = useState<PetDto | null>(lastSeenPet)
  const [text, setText] = useState<string | null>(null)

  // Сравниваем с тем, каким игрок видел питомца прошлый раз (первый раз на телефоне или другой игрок — без всплывашки).
  if (prev !== pet) {
    const message = prev && prev.userId === pet.userId && pet.hatched ? levelUpText(prev, pet) : null
    setPrev(pet)
    if (message) {
      setText(message)
    }
  }

  useEffect(() => rememberPet(pet), [pet])

  if (!text) {
    return null
  }

  return (
    <button className="pet-toast" onClick={() => setText(null)}>
      <Emoji e="🎉" /> {text}
    </button>
  )
}
