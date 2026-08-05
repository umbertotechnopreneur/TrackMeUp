import { ref } from 'vue'

export const themePreferences = ['system', 'light', 'dark'] as const
export type ThemePreference = (typeof themePreferences)[number]

export const themePreference = ref<ThemePreference>('system')

export function isThemePreference(value: unknown): value is ThemePreference {
  return typeof value === 'string' && themePreferences.includes(value as ThemePreference)
}

export function applyHostThemePreference(value: unknown): boolean {
  if (!isThemePreference(value)) return false
  themePreference.value = value
  return true
}

export function resolveThemeName(preference: ThemePreference, systemIsDark: boolean): 'trackLight' | 'trackDark' {
  if (preference === 'dark') return 'trackDark'
  if (preference === 'light') return 'trackLight'
  return systemIsDark ? 'trackDark' : 'trackLight'
}
