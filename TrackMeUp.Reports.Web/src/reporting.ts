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
  byProvider: AiUsageSlice[]
  byOrigin: AiUsageSlice[]
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

const isDateText = (value: unknown): value is string =>
  typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)

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

const isAiUsageSlice = (value: unknown): value is AiUsageSlice => {
  if (!isObject(value)) return false
  return typeof value.label === 'string'
    && value.label.length > 0
    && value.label.length <= 256
    && hasNumericFields(value, ['requestCount', 'inputTokens', 'outputTokens', 'totalTokens'])
    && isNullableNonNegative(value.actualCostUsd)
}

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
  ])
    && (value.successfulRequestCount as number) + (value.failedRequestCount as number) === (value.requestCount as number)
    && (value.actualCostRequestCount as number) <= (value.requestCount as number)
    && isNullableNonNegative(value.actualCostUsd)
    && Array.isArray(value.byProvider)
    && value.byProvider.every(isAiUsageSlice)
    && Array.isArray(value.byOrigin)
    && value.byOrigin.every(isAiUsageSlice)
}

/** Validates the versioned host envelope before any value reaches the chart renderer. */
export function validateReportEnvelope(value: unknown): EnvelopeValidationResult {
  if (!isObject(value) || value.type !== 'report.snapshot') {
    return { error: tr('The received message is not a TrackMeUp report.', 'Il messaggio ricevuto non è un report TrackMeUp.') }
  }

  if (typeof value.view !== 'string' || !reportViews.includes(value.view as ReportView)) {
    return { error: tr('The requested view is not supported.', 'La vista richiesta non è supportata.') }
  }

  const snapshot = value.snapshot
  if (!isObject(snapshot)) {
    return { error: tr('The report does not contain a valid snapshot.', 'Il report non contiene uno snapshot valido.') }
  }

  if (snapshot.contractVersion !== 2) {
    return { error: tr('The report version is not compatible with this application.', 'La versione del report non è compatibile con questa applicazione.') }
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
    return { error: tr('The report content is incomplete or invalid.', 'Il contenuto del report è incompleto o non valido.') }
  }

  return { envelope: value as unknown as ReportEnvelope }
}

export function formatDate(value: string): string {
  const date = new Date(`${value}T00:00:00Z`)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(reportLocale.value, {
    day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC',
  }).format(date)
}

export function formatDateTime(value: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : new Intl.DateTimeFormat(reportLocale.value, {
    day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  }).format(date)
}

export function formatDuration(seconds: number): string {
  const safeSeconds = Math.max(0, Math.round(seconds))
  if (safeSeconds < 60) return `${safeSeconds} s`

  const hours = Math.floor(safeSeconds / 3600)
  const minutes = Math.floor((safeSeconds % 3600) / 60)

  if (hours === 0) return `${minutes} min`
  if (minutes === 0) return `${hours} h`
  return `${hours} h ${minutes} min`
}

export function formatHours(seconds: number): string {
  return `${(Math.max(0, seconds) / 3600).toLocaleString(reportLocale.value, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 1,
  })} h`
}

export function formatInteger(value: number): string {
  return new Intl.NumberFormat(reportLocale.value).format(Math.max(0, Math.round(value)))
}

export function formatPercent(value: number): string {
  const safeValue = Math.min(1, Math.max(0, value))
  return safeValue.toLocaleString(reportLocale.value, {
    style: 'percent',
    maximumFractionDigits: 0,
  })
}

export function formatRange(range: ReportRange): string {
  if (range.from === range.toInclusive) return formatDate(range.from)
  return `${formatDate(range.from)} – ${formatDate(range.toInclusive)}`
}
