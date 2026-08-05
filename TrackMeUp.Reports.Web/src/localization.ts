import { computed, ref } from 'vue'

export type ReportLanguage = 'en' | 'it'

const browserLanguage = navigator.language.toLowerCase().startsWith('it') ? 'it' : 'en'

export const reportLanguage = ref<ReportLanguage>(browserLanguage)
export const reportLocale = computed(() => reportLanguage.value === 'it' ? 'it-IT' : 'en-US')

/** Unsupported explicit application languages intentionally use English instead of the Windows locale. */
export function setReportLanguage(value: unknown): boolean {
  if (typeof value !== 'string') return false
  reportLanguage.value = value.toLowerCase().startsWith('it') ? 'it' : 'en'
  document.documentElement.lang = reportLanguage.value
  return true
}

export function tr(english: string, italian: string): string {
  return reportLanguage.value === 'it' ? italian : english
}

export function reportDayNames(short = false): string[] {
  if (short) {
    return reportLanguage.value === 'it'
      ? ['Dom', 'Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab']
      : ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
  }

  return reportLanguage.value === 'it'
    ? ['Domenica', 'Lunedì', 'Martedì', 'Mercoledì', 'Giovedì', 'Venerdì', 'Sabato']
    : ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
}

export function reportMonthNames(): string[] {
  return reportLanguage.value === 'it'
    ? ['Gen', 'Feb', 'Mar', 'Apr', 'Mag', 'Giu', 'Lug', 'Ago', 'Set', 'Ott', 'Nov', 'Dic']
    : ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
}
