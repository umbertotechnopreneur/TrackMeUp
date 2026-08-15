import { computed, ref } from 'vue'
import {
  isReportLocale,
  messagePlaceholders,
  reportCatalogs,
  type MessageKey,
  type ReportLocale,
} from './locales/catalogs'

type PlaceholderNames<Value extends string> =
  Value extends `${string}{${infer Name}}${infer Rest}`
    ? Name | PlaceholderNames<Rest>
    : never

type MessageArguments<Key extends MessageKey> =
  [PlaceholderNames<Key>] extends [never]
    ? []
    : [values: Readonly<Record<PlaceholderNames<Key>, string | number>>]

function resolveBrowserReportLocale(languages: readonly string[]): ReportLocale {
  for (const languageTag of languages) {
    try {
      const locale = new Intl.Locale(languageTag).maximize()
      switch (locale.language) {
        case 'en': return 'en-US'
        case 'it': return 'it-IT'
        case 'fr': return 'fr-FR'
        case 'de': return 'de-DE'
        case 'es': return 'es-ES'
        case 'vi': return 'vi-VN'
        case 'ko': return 'ko-KR'
        case 'pt': return locale.region === 'BR' ? 'pt-BR' : 'pt-PT'
        case 'zh':
          // Traditional Chinese is a separate localization and must not silently receive
          // Simplified Chinese product copy.
          if (locale.script === 'Hans') return 'zh-Hans'
          break
      }
    }
    catch (error) {
      if (!(error instanceof RangeError)) throw error
    }
  }

  return 'en-US'
}

const browserLocale = typeof navigator === 'undefined'
  ? 'en-US'
  : resolveBrowserReportLocale(navigator.languages?.length ? navigator.languages : [navigator.language])

export const reportLanguage = ref<ReportLocale>(browserLocale)
export const reportLocale = computed(() => reportLanguage.value)

function applyDocumentLocale(locale: ReportLocale): void {
  if (typeof document === 'undefined') return
  document.documentElement.lang = locale
  document.title = reportCatalogs[locale]['TrackMeUp Reports']
}

/** Applies an exact BCP-47 locale received from the native host. */
export function setReportLanguage(value: unknown): boolean {
  if (!isReportLocale(value)) return false
  reportLanguage.value = value
  applyDocumentLocale(value)
  return true
}

/** Formats one strict catalog message and rejects missing or unexpected placeholders. */
export function translateMessage<Key extends MessageKey>(
  locale: ReportLocale,
  key: Key,
  ...args: MessageArguments<Key>
): string {
  const expected = [...messagePlaceholders(key)].sort()
  const values = (args[0] ?? {}) as Readonly<Record<string, string | number>>
  const provided = Object.keys(values).sort()
  if (expected.join('|') !== provided.join('|')) {
    throw new Error(`Invalid placeholders for report message: ${key}`)
  }

  const template = reportCatalogs[locale][key]
  if (template === undefined) throw new Error(`Missing ${locale} report message: ${key}`)
  return template.replace(/\{([a-z][a-zA-Z0-9]*)\}/g, (_match, name: string) => String(values[name]))
}

export function tr<Key extends MessageKey>(key: Key, ...args: MessageArguments<Key>): string {
  return translateMessage(reportLanguage.value, key, ...args)
}

export function reportDayNames(short = false): string[] {
  const formatter = new Intl.DateTimeFormat(reportLocale.value, {
    weekday: short ? 'short' : 'long',
    timeZone: 'UTC',
  })
  const firstSunday = Date.UTC(2024, 0, 7)
  return Array.from({ length: 7 }, (_, index) => formatter.format(new Date(firstSunday + index * 86_400_000)))
}

export function reportMonthNames(): string[] {
  const formatter = new Intl.DateTimeFormat(reportLocale.value, {
    month: 'short',
    timeZone: 'UTC',
  })
  return Array.from({ length: 12 }, (_, month) => formatter.format(new Date(Date.UTC(2024, month, 1))))
}

export function reportHourLabel(hour: number): string {
  if (!Number.isInteger(hour) || hour < 0 || hour > 23) throw new RangeError('Report hour must be between 0 and 23.')
  return new Intl.DateTimeFormat(reportLocale.value, {
    hour: 'numeric',
    minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(2024, 0, 1, hour)))
}

applyDocumentLocale(reportLanguage.value)
