import { onBeforeUnmount, ref } from 'vue'

export function useIsMobile(query = '(max-width: 1024px)') {
  const isMobile = ref(false)

  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return { isMobile }
  }

  const mediaQuery = window.matchMedia(query)
  const listener = (event: MediaQueryListEvent) => {
    isMobile.value = event.matches
  }

  isMobile.value = mediaQuery.matches

  if (typeof mediaQuery.addEventListener === 'function') {
    mediaQuery.addEventListener('change', listener)
  } else {
    mediaQuery.addListener(listener)
  }

  onBeforeUnmount(() => {
    if (typeof mediaQuery.removeEventListener === 'function') {
      mediaQuery.removeEventListener('change', listener)
    } else {
      mediaQuery.removeListener(listener)
    }
  })

  return { isMobile }
}
