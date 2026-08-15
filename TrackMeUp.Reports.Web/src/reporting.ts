import { reportLocale, tr } from './localization'

export const reportViews = ['calendar', 'hourOfWeek', 'trend', 'applications'] as const

export type ReportView = (typeof reportViews)[number]

export interface ReportRange {
  from: string
  toInclusive: string
  timeZoneId: string
  dayCount: number
}

export interface ReportTotals {
  activeSeconds: number
  idleSeconds: number
  trackedSeconds: number
  keyPresses: number
  mouseClicks: number
  activeDays: number
}

export interface ReportCalendarCell {
  date: string
  activeSeconds: number
  idleSeconds: number
  trackedSeconds: number
  keyPresses: number
  mouseClicks: number
  sampleCount: number
  hasData: boolean
  activityScore: number | null
}

export interface ReportHourCell {
  dayOfWeek: number
  hour: number
  activeSeconds: number
  idleSeconds: number
  trackedSeconds: number
  observationDays: number
  hasData: boolean
}

export interface ReportTrendBucket {
  start: string
  endInclusive: string
  activeSeconds: number
  idleSeconds: number
  trackedSeconds: number
  keyPresses: number
  mouseClicks: number
  hasData: boolean
}

export interface ReportApplicationSlice {
  application: string
  activeSeconds: number
}

export interface ReportDataQuality {
  hasData: boolean
  firstSampleAt: string | null
  lastSampleAt: string | null
  sampleCount: number
  coveredSeconds: number
  requestedSeconds: number
  coverageRatio: number
}

export interface AiUsageSlice {
  label: string
  requestCount: number
  inputTokens: number
  outputTokens: number
  totalTokens: number
  actualCostUsd: number | null
  estimatedCostUsd: number | null
}

export const aiUsageOrigins = [
  'winui.operations',
  'cli.ai',
  'snapshot.manual',
  'snapshot.scheduled',
  'snapshot.reprocess',
  'runtime.ai',
  'manual',
  'unknown',
] as const

export type AiUsageOrigin = (typeof aiUsageOrigins)[number]

export interface AiUsageOriginSlice extends Omit<AiUsageSlice, 'label'> {
  label: AiUsageOrigin
}

export interface AiUsageSummary {
  requestCount: number
  successfulRequestCount: number
  failedRequestCount: number
  inputTokens: number
  outputTokens: number
  totalTokens: number
  cachedInputTokens: number
  reasoningTokens: number
  thinkingTokens: number
  actualCostUsd: number | null
  actualCostRequestCount: number
  estimatedCostUsd: number | null
  estimatedCostRequestCount: number
  estimatedCostPricingUpdatedAt: string | null
  byProvider: AiUsageSlice[]
  byOrigin: AiUsageOriginSlice[]
}

export interface ReportSnapshot {
  contractVersion: number
  range: ReportRange
  totals: ReportTotals
  calendar: ReportCalendarCell[]
  hourOfWeek: ReportHourCell[]
  trend: ReportTrendBucket[]
  applications: ReportApplicationSlice[]
  quality: ReportDataQuality
  aiUsage: AiUsageSummary
}

export interface ReportEnvelope {
  type: 'report.snapshot'
  view: ReportView
  snapshot: ReportSnapshot
}

export interface EnvelopeValidationResult {
  envelope?: ReportEnvelope
  error?: string
}

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const isFiniteNonNegative = (value: unknown): value is number =>
  typeof value === 'number' && Number.isFinite(value) && value >= 0

const isIntegerInRange = (value: unknown, minimum: number, maximum: number): value is number =>
  Number.isInteger(value) && typeof value === 'number' && value >= minimum && value <= maximum

const isDateText = (value: unknown): value is string => {
  if (typeof value !== 'string') return false
  try {
    parseCalendarDate(value)
    return true
  }
  catch (error) {
    if (error instanceof RangeError) return false
    throw error
  }
}

const isNullableDateTimeText = (value: unknown): value is string | null =>
  value === null || (typeof value === 'string' && value.length > 0 && !Number.isNaN(Date.parse(value)))

const hasNumericFields = (value: Record<string, unknown>, fields: string[]): boolean =>
  fields.every((field) => isFiniteNonNegative(value[field]))

const isRange = (value: unknown): value is ReportRange => {
  if (!isObject(value)) return false
  return isDateText(value.from)
    && isDateText(value.toInclusive)
    && typeof value.timeZoneId === 'string'
    && value.timeZoneId.length > 0
    && isIntegerInRange(value.dayCount, 1, 366)
}

const isTotals = (value: unknown): value is ReportTotals => {
  if (!isObject(value)) return false
  return hasNumericFields(value, [
    'activeSeconds',
    'idleSeconds',
    'trackedSeconds',
    'keyPresses',
    'mouseClicks',
    'activeDays',
  ])
}

const isCalendarCell = (value: unknown): value is ReportCalendarCell => {
  if (!isObject(value)) return false
  return isDateText(value.date)
    && typeof value.hasData === 'boolean'
    && isNullableActivityScore(value.activityScore)
    && (value.hasData ? value.activityScore !== null : value.activityScore === null)
    && hasNumericFields(value, [
      'activeSeconds',
      'idleSeconds',
      'trackedSeconds',
      'keyPresses',
      'mouseClicks',
      'sampleCount',
    ])
}

const isHourCell = (value: unknown): value is ReportHourCell => {
  if (!isObject(value)) return false
  return isIntegerInRange(value.dayOfWeek, 0, 6)
    && isIntegerInRange(value.hour, 0, 23)
    && typeof value.hasData === 'boolean'
    && hasNumericFields(value, [
      'activeSeconds',
      'idleSeconds',
      'trackedSeconds',
      'observationDays',
    ])
}

const isTrendBucket = (value: unknown): value is ReportTrendBucket => {
  if (!isObject(value)) return false
  return isDateText(value.start)
    && isDateText(value.endInclusive)
    && typeof value.hasData === 'boolean'
    && hasNumericFields(value, [
      'activeSeconds',
      'idleSeconds',
      'trackedSeconds',
      'keyPresses',
      'mouseClicks',
    ])
}

const isApplicationSlice = (value: unknown): value is ReportApplicationSlice => {
  if (!isObject(value)) return false
  return typeof value.application === 'string'
    && value.application.length > 0
    && value.application.length <= 256
    && isFiniteNonNegative(value.activeSeconds)
}

const isQuality = (value: unknown): value is ReportDataQuality => {
  if (!isObject(value)) return false
  return typeof value.hasData === 'boolean'
    && isNullableDateTimeText(value.firstSampleAt)
    && isNullableDateTimeText(value.lastSampleAt)
    && hasNumericFields(value, ['sampleCount', 'coveredSeconds', 'requestedSeconds', 'coverageRatio'])
    && (value.coverageRatio as number) <= 1
}

const isNullableNonNegative = (value: unknown): value is number | null =>
  value === null || isFiniteNonNegative(value)

const isNullableActivityScore = (value: unknown): value is number | null =>
  value === null || isIntegerInRange(value, 0, 100)

const isAiUsageSlice = (value: unknown): value is AiUsageSlice => {
  if (!isObject(value)) return false
  return typeof value.label === 'string'
    && value.label.length > 0
    && value.label.length <= 256
    && hasNumericFields(value, ['requestCount', 'inputTokens', 'outputTokens', 'totalTokens'])
    && isNullableNonNegative(value.actualCostUsd)
    && isNullableNonNegative(value.estimatedCostUsd)
}

const isAiUsageOriginSlice = (value: unknown): value is AiUsageOriginSlice =>
  isAiUsageSlice(value) && aiUsageOrigins.includes(value.label as AiUsageOrigin)

const isAiUsage = (value: unknown): value is AiUsageSummary => {
  if (!isObject(value)) return false
  return hasNumericFields(value, [
    'requestCount',
    'successfulRequestCount',
    'failedRequestCount',
    'inputTokens',
    'outputTokens',
    'totalTokens',
    'cachedInputTokens',
    'reasoningTokens',
    'thinkingTokens',
    'actualCostRequestCount',
    'estimatedCostRequestCount',
  ])
    && (value.successfulRequestCount as number) + (value.failedRequestCount as number) === (value.requestCount as number)
    && (value.actualCostRequestCount as number) <= (value.requestCount as number)
    && (value.estimatedCostRequestCount as number) <= (value.requestCount as number)
    && isNullableNonNegative(value.actualCostUsd)
    && isNullableNonNegative(value.estimatedCostUsd)
    && isNullableDateTimeText(value.estimatedCostPricingUpdatedAt)
    && Array.isArray(value.byProvider)
    && value.byProvider.every(isAiUsageSlice)
    && Array.isArray(value.byOrigin)
    && value.byOrigin.every(isAiUsageOriginSlice)
}

/** Validates the versioned host envelope before any value reaches the chart renderer. */
export function validateReportEnvelope(value: unknown): EnvelopeValidationResult {
  if (!isObject(value) || value.type !== 'report.snapshot') {
    return { error: tr('The received message is not a TrackMeUp report.') }
  }

  if (typeof value.view !== 'string' || !reportViews.includes(value.view as ReportView)) {
    return { error: tr('The requested view is not supported.') }
  }

  const snapshot = value.snapshot
  if (!isObject(snapshot)) {
    return { error: tr('The report does not contain a valid snapshot.') }
  }

  if (snapshot.contractVersion !== 4) {
    return { error: tr('The report version is not compatible with this application.') }
  }

  if (!isRange(snapshot.range)
    || !isTotals(snapshot.totals)
    || !Array.isArray(snapshot.calendar)
    || !snapshot.calendar.every(isCalendarCell)
    || !Array.isArray(snapshot.hourOfWeek)
    || !snapshot.hourOfWeek.every(isHourCell)
    || !Array.isArray(snapshot.trend)
    || !snapshot.trend.every(isTrendBucket)
    || !Array.isArray(snapshot.applications)
    || !snapshot.applications.every(isApplicationSlice)
    || !isQuality(snapshot.quality)
    || !isAiUsage(snapshot.aiUsage)) {
    return { error: tr('The report content is incomplete or invalid.') }
  }

  return { envelope: value as unknown as ReportEnvelope }
}

function parseCalendarDate(value: string): Date {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) throw new RangeError('Report date must use YYYY-MM-DD.')
  const year = Number(match[1])
  const month = Number(match[2]) - 1
  const day = Number(match[3])
  const date = new Date(Date.UTC(year, month, day))
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month || date.getUTCDate() !== day) {
    throw new RangeError('Report date is outside the calendar.')
  }

  return date
}

function requireFinite(value: number, minimum = 0, maximum = Number.POSITIVE_INFINITY): number {
  if (!Number.isFinite(value) || value < minimum || value > maximum) {
    throw new RangeError(`Report value must be between ${minimum} and ${maximum}.`)
  }

  return value
}

export function formatDate(value: string): string {
  return new Intl.DateTimeFormat(reportLocale.value, {
    day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC',
  }).format(parseCalendarDate(value))
}

export function formatDateTime(value: string | null): string {
  if (value === null) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) throw new RangeError('Report date-time is invalid.')
  return new Intl.DateTimeFormat(reportLocale.value, {
    day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  }).format(date)
}

export function formatDuration(seconds: number): string {
  const safeSeconds = Math.round(requireFinite(seconds))
  const unit = (value: number, name: 'second' | 'minute' | 'hour') => new Intl.NumberFormat(reportLocale.value, {
    style: 'unit',
    unit: name,
    unitDisplay: 'short',
    maximumFractionDigits: 0,
  }).format(value)
  if (safeSeconds < 60) return unit(safeSeconds, 'second')

  const hours = Math.floor(safeSeconds / 3600)
  const minutes = Math.floor((safeSeconds % 3600) / 60)

  if (hours === 0) return unit(minutes, 'minute')
  if (minutes === 0) return unit(hours, 'hour')
  return new Intl.ListFormat(reportLocale.value, { style: 'short', type: 'unit' })
    .format([unit(hours, 'hour'), unit(minutes, 'minute')])
}

export function formatHours(seconds: number): string {
  return new Intl.NumberFormat(reportLocale.value, {
    style: 'unit',
    unit: 'hour',
    unitDisplay: 'short',
    maximumFractionDigits: 1,
  }).format(requireFinite(seconds) / 3600)
}

export function formatDecimal(value: number, maximumFractionDigits = 1): string {
  if (!Number.isInteger(maximumFractionDigits) || maximumFractionDigits < 0 || maximumFractionDigits > 20) {
    throw new RangeError('Maximum fraction digits must be between 0 and 20.')
  }

  return new Intl.NumberFormat(reportLocale.value, { maximumFractionDigits })
    .format(requireFinite(value))
}

export function formatInteger(value: number): string {
  return new Intl.NumberFormat(reportLocale.value).format(Math.round(requireFinite(value)))
}

export function formatPercent(value: number): string {
  return requireFinite(value, 0, 1).toLocaleString(reportLocale.value, {
    style: 'percent',
    maximumFractionDigits: 0,
  })
}

export function formatRange(range: ReportRange): string {
  const formatter = new Intl.DateTimeFormat(reportLocale.value, {
    day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC',
  })
  return formatter.formatRange(parseCalendarDate(range.from), parseCalendarDate(range.toInclusive))
}
