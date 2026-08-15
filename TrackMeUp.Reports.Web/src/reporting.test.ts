import { describe, expect, it } from 'vitest'
import { buildDevelopmentEnvelope } from './dev/fixture'
import { reportLocales } from './locales/catalogs'
import { setReportLanguage } from './localization'
import {
  formatDate,
  formatDateTime,
  formatDecimal,
  formatDuration,
  formatHours,
  formatInteger,
  formatPercent,
  formatRange,
  validateReportEnvelope,
} from './reporting'

describe('locale-aware report formatting', () => {
  it.each(reportLocales)('formats dates, numbers, units, and ranges for %s', (locale) => {
    expect(setReportLanguage(locale)).toBe(true)
    expect(formatDate('2026-08-15')).not.toContain('NaN')
    expect(formatDateTime('2026-08-15T12:30:00Z')).not.toContain('NaN')
    expect(formatInteger(1_234_567)).not.toContain('NaN')
    expect(formatDecimal(1_234.5)).not.toContain('NaN')
    expect(formatPercent(0.42)).not.toContain('NaN')
    expect(formatDuration(30)).not.toContain('NaN')
    expect(formatDuration(90)).not.toContain('NaN')
    expect(formatDuration(3_660)).not.toContain('NaN')
    expect(formatHours(5_400)).not.toContain('NaN')
    expect(formatRange({
      from: '2026-08-01',
      toInclusive: '2026-08-15',
      timeZoneId: 'Asia/Saigon',
      dayCount: 15,
    })).not.toContain('NaN')
  })

  it('uses locale-specific separators and unit names', () => {
    setReportLanguage('en-US')
    const englishDecimal = formatDecimal(1_234.5)
    setReportLanguage('de-DE')
    const germanDecimal = formatDecimal(1_234.5)
    expect(germanDecimal).not.toBe(englishDecimal)

    setReportLanguage('zh-Hans')
    expect(formatDuration(3_660)).toMatch(/小时|分钟/)
  })

  it('fails fast instead of normalizing invalid values', () => {
    expect(() => formatDate('2026-02-30')).toThrow(RangeError)
    expect(() => formatDateTime('not-a-date')).toThrow(RangeError)
    expect(() => formatDateTime('')).toThrow(RangeError)
    expect(() => formatDuration(-1)).toThrow(RangeError)
    expect(() => formatHours(Number.NaN)).toThrow(RangeError)
    expect(() => formatInteger(Number.POSITIVE_INFINITY)).toThrow(RangeError)
    expect(() => formatPercent(1.01)).toThrow(RangeError)
    expect(() => formatDecimal(10, 21)).toThrow(RangeError)
  })
})

describe('localized report-envelope validation', () => {
  it('accepts every supported AI origin in the development fixture', () => {
    setReportLanguage('en-US')
    const result = validateReportEnvelope(buildDevelopmentEnvelope())
    expect(result.error).toBeUndefined()
    expect(result.envelope).toBeDefined()
  })

  it('rejects unsupported AI origin codes instead of rendering raw technical text', () => {
    setReportLanguage('en-US')
    const invalidEnvelope = structuredClone(buildDevelopmentEnvelope()) as any
    invalidEnvelope.snapshot.aiUsage.byOrigin[0].label = 'custom.origin'
    const result = validateReportEnvelope(invalidEnvelope)
    expect(result.envelope).toBeUndefined()
    expect(result.error).toBe('The report content is incomplete or invalid.')
  })

  it('rejects impossible calendar dates before they reach Intl', () => {
    setReportLanguage('en-US')
    const invalidEnvelope = structuredClone(buildDevelopmentEnvelope()) as any
    invalidEnvelope.snapshot.calendar[0].date = '2026-02-30'
    const result = validateReportEnvelope(invalidEnvelope)
    expect(result.envelope).toBeUndefined()
    expect(result.error).toBe('The report content is incomplete or invalid.')
  })
})
