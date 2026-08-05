import { computed, onBeforeUnmount, ref } from 'vue'
import { useTheme } from 'vuetify'

function useMediaQuery(query: string) {
  const media = window.matchMedia(query)
  const matches = ref(media.matches)
  const update = (event: MediaQueryListEvent) => {
    matches.value = event.matches
  }

  media.addEventListener('change', update)
  onBeforeUnmount(() => media.removeEventListener('change', update))
  return matches
}

export function useChartSettings() {
  const theme = useTheme()
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')
  const forcedColors = useMediaQuery('(forced-colors: active)')

  const palette = computed(() => {
    const colors = theme.current.value.colors
    const dark = theme.current.value.dark
    return {
      text: String(colors['on-surface']),
      muted: String(colors['on-surface-variant']),
      border: dark ? '#3b4652' : '#d6dfe8',
      noData: String(colors['surface-variant']),
      active: dark ? '#78b8ed' : '#286da8',
      idle: dark ? '#3d6381' : '#a8c9e3',
      heat: dark
        ? ['#243544', '#315473', '#417ba8', '#5ca1d9', '#9bcef4']
        : ['#e1edf7', '#b8d6ed', '#82b8e3', '#4b96cf', '#1f649b'],
    }
  })

  return {
    forcedColors,
    palette,
    reducedMotion,
  }
}
