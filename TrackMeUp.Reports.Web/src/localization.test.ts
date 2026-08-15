import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import {
  reportCatalogs,
  reportLocales,
} from './locales/catalogs'
import {
  reportDayNames,
  reportHourLabel,
  reportLanguage,
  reportMonthNames,
  setReportLanguage,
  tr,
  translateMessage,
} from './localization'

const expectedLocales = [
  'en-US',
  'it-IT',
  'fr-FR',
  'de-DE',
  'es-ES',
  'zh-Hans',
  'vi-VN',
  'ko-KR',
  'pt-PT',
  'pt-BR',
] as const

describe('report localization catalogs', () => {
  it('contains exactly every supported BCP-47 locale with the full English key set', () => {
    expect(reportLocales).toEqual(expectedLocales)
    const englishKeys = Object.keys(reportCatalogs['en-US']).sort()
    expect(englishKeys.length).toBeGreaterThan(100)

    for (const locale of reportLocales) {
      expect(Object.keys(reportCatalogs[locale]).sort()).toEqual(englishKeys)
      expect(Object.values(reportCatalogs[locale]).every((value) => value.trim().length > 0)).toBe(true)
    }
  })

  it('keeps European and Brazilian Portuguese as separate product catalogs', () => {
    expect(reportCatalogs['pt-PT'].Applications).toBe('Aplicações')
    expect(reportCatalogs['pt-BR'].Applications).toBe('Aplicativos')
  })

  it('formats named placeholders and rejects missing or unexpected values', () => {
    expect(translateMessage('de-DE', '{count} samples', { count: '1.234' })).toBe('1.234 Stichproben')
    expect(() => (translateMessage as (...args: unknown[]) => string)('fr-FR', '{count} samples'))
      .toThrow(/Invalid placeholders/)
    expect(() => (translateMessage as (...args: unknown[]) => string)(
      'fr-FR',
      '{count} samples',
      { count: 1, extra: 2 },
    )).toThrow(/Invalid placeholders/)
  })
})

describe('report locale state', () => {
  let originalDocument: Document | undefined

  beforeEach(() => {
    originalDocument = globalThis.document
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: { documentElement: { lang: '' }, title: '' },
    })
    setReportLanguage('en-US')
  })

  afterEach(() => {
    if (originalDocument === undefined) {
      Reflect.deleteProperty(globalThis, 'document')
    }
    else {
      Object.defineProperty(globalThis, 'document', { configurable: true, value: originalDocument })
    }
  })

  it('accepts only exact supported host locales and preserves state on invalid input', () => {
    expect(setReportLanguage('fr-FR')).toBe(true)
    expect(reportLanguage.value).toBe('fr-FR')
    expect(document.documentElement.lang).toBe('fr-FR')
    expect(document.title).toBe('Rapports TrackMeUp')
    expect(tr('System')).toBe('Système')

    expect(setReportLanguage('fr')).toBe(false)
    expect(setReportLanguage('pt')).toBe(false)
    expect(setReportLanguage('zh-Hant')).toBe(false)
    expect(reportLanguage.value).toBe('fr-FR')
  })

  it.each(expectedLocales)('derives calendar names and clock labels through Intl for %s', (locale) => {
    expect(setReportLanguage(locale)).toBe(true)
    expect(reportDayNames()).toHaveLength(7)
    expect(reportDayNames(true)).toHaveLength(7)
    expect(reportMonthNames()).toHaveLength(12)
    expect(reportHourLabel(0).length).toBeGreaterThan(0)
    expect(reportHourLabel(23).length).toBeGreaterThan(0)
  })

  it('rejects unsupported clock values', () => {
    expect(() => reportHourLabel(-1)).toThrow(RangeError)
    expect(() => reportHourLabel(24)).toThrow(RangeError)
    expect(() => reportHourLabel(1.5)).toThrow(RangeError)
  })
})
