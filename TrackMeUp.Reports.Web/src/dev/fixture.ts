import type {
  ReportApplicationSlice,
  AiUsageSummary,
  ReportCalendarCell,
  ReportEnvelope,
  ReportHourCell,
  ReportTrendBucket,
} from '../reporting'
import type { ReportLocale } from '../locales/catalogs'

const developmentFixtureCopy = {
  'en-US': ['Development preview', 'The displayed values are demonstration data isolated from the production build.'],
  'it-IT': ['Anteprima di sviluppo', 'I valori mostrati sono dati dimostrativi isolati dalla build di produzione.'],
  'fr-FR': ['Aperçu de développement', 'Les valeurs affichées sont des données de démonstration isolées de la version de production.'],
  'de-DE': ['Entwicklungsvorschau', 'Die angezeigten Werte sind Demodaten, die vom Produktionsbuild getrennt sind.'],
  'es-ES': ['Vista previa de desarrollo', 'Los valores mostrados son datos de demostración aislados de la compilación de producción.'],
  'zh-Hans': ['开发预览', '显示的值是与生产版本隔离的演示数据。'],
  'vi-VN': ['Bản xem trước phát triển', 'Các giá trị hiển thị là dữ liệu minh họa được tách biệt khỏi bản dựng sản xuất.'],
  'ko-KR': ['개발 미리 보기', '표시된 값은 프로덕션 빌드와 분리된 데모 데이터입니다.'],
  'pt-PT': ['Pré-visualização de desenvolvimento', 'Os valores apresentados são dados de demonstração isolados da compilação de produção.'],
  'pt-BR': ['Prévia de desenvolvimento', 'Os valores exibidos são dados de demonstração isolados da compilação de produção.'],
} as const satisfies Readonly<Record<ReportLocale, readonly [title: string, description: string]>>

export function getDevelopmentFixtureCopy(locale: ReportLocale): { title: string; description: string } {
  const [title, description] = developmentFixtureCopy[locale]
  return { title, description }
}

const startDate = new Date('2026-07-23T00:00:00Z')

const calendar: ReportCalendarCell[] = Array.from({ length: 14 }, (_, index) => {
  const date = new Date(startDate)
  date.setUTCDate(startDate.getUTCDate() + index)
  const weekend = date.getUTCDay() === 0 || date.getUTCDay() === 6
  const hasData = index !== 3
  const activeSeconds = hasData ? (weekend ? 5400 : 12600 + ((index * 1900) % 10800)) : 0
  const idleSeconds = hasData ? (weekend ? 1800 : 2700 + ((index * 400) % 1800)) : 0

  return {
    date: date.toISOString().slice(0, 10),
    activeSeconds,
    idleSeconds,
    trackedSeconds: activeSeconds + idleSeconds,
    keyPresses: hasData ? 420 + index * 63 : 0,
    mouseClicks: hasData ? 115 + index * 19 : 0,
    sampleCount: hasData ? Math.round((activeSeconds + idleSeconds) / 5) : 0,
    hasData,
    activityScore: hasData ? Math.min(100, 24 + index * 5) : null,
  }
})

const hourOfWeek: ReportHourCell[] = Array.from({ length: 7 * 24 }, (_, index) => {
  const dayOfWeek = Math.floor(index / 24)
  const hour = index % 24
  const workingDay = dayOfWeek >= 1 && dayOfWeek <= 5
  const inWorkingWindow = hour >= 8 && hour <= 18
  const hasData = workingDay && inWorkingWindow
  const activeSeconds = hasData ? 600 + ((dayOfWeek * 17 + hour * 23) % 47) * 45 : 0
  const idleSeconds = hasData ? 300 + ((hour * 11) % 19) * 30 : 0
  return {
    dayOfWeek,
    hour,
    activeSeconds,
    idleSeconds,
    trackedSeconds: activeSeconds + idleSeconds,
    observationDays: hasData ? 2 : 0,
    hasData,
  }
})

const trend: ReportTrendBucket[] = calendar.map<ReportTrendBucket>((cell) => ({
  start: cell.date,
  endInclusive: cell.date,
  activeSeconds: cell.activeSeconds,
  idleSeconds: cell.idleSeconds,
  trackedSeconds: cell.trackedSeconds,
  keyPresses: cell.keyPresses,
  mouseClicks: cell.mouseClicks,
  hasData: cell.hasData,
}))

const applications: ReportApplicationSlice[] = [
  { application: 'Visual Studio', activeSeconds: 61200 },
  { application: 'Microsoft Edge', activeSeconds: 28800 },
  { application: 'Terminal', activeSeconds: 21600 },
  { application: 'Outlook', activeSeconds: 12600 },
  { application: 'Altro', activeSeconds: 9000 },
]

const aiUsage: AiUsageSummary = {
  requestCount: 14,
  successfulRequestCount: 13,
  failedRequestCount: 1,
  inputTokens: 38240,
  outputTokens: 6010,
  totalTokens: 44250,
  cachedInputTokens: 8040,
  reasoningTokens: 1180,
  thinkingTokens: 0,
  actualCostUsd: 0.1274,
  actualCostRequestCount: 9,
  estimatedCostUsd: 0.07152,
  estimatedCostRequestCount: 5,
  estimatedCostPricingUpdatedAt: '2026-08-05T08:30:00Z',
  byProvider: [
    { label: 'openrouter', requestCount: 9, inputTokens: 24200, outputTokens: 3850, totalTokens: 28050, actualCostUsd: 0.1274, estimatedCostUsd: null },
    { label: 'openai', requestCount: 5, inputTokens: 14040, outputTokens: 2160, totalTokens: 16200, actualCostUsd: null, estimatedCostUsd: 0.07152 },
  ],
  byOrigin: [
    { label: 'winui.operations', requestCount: 8, inputTokens: 22000, outputTokens: 3410, totalTokens: 25410, actualCostUsd: 0.082, estimatedCostUsd: 0.04118 },
    { label: 'snapshot.scheduled', requestCount: 5, inputTokens: 13240, outputTokens: 2100, totalTokens: 15340, actualCostUsd: 0.0454, estimatedCostUsd: 0.03034 },
    { label: 'cli.ai', requestCount: 1, inputTokens: 3000, outputTokens: 500, totalTokens: 3500, actualCostUsd: null, estimatedCostUsd: null },
  ],
}

export function buildDevelopmentEnvelope(): ReportEnvelope {
  const totals = calendar.reduce(
    (accumulator, cell) => ({
      activeSeconds: accumulator.activeSeconds + cell.activeSeconds,
      idleSeconds: accumulator.idleSeconds + cell.idleSeconds,
      trackedSeconds: accumulator.trackedSeconds + cell.trackedSeconds,
      keyPresses: accumulator.keyPresses + cell.keyPresses,
      mouseClicks: accumulator.mouseClicks + cell.mouseClicks,
      activeDays: accumulator.activeDays + (cell.activeSeconds > 0 ? 1 : 0),
    }),
    {
      activeSeconds: 0,
      idleSeconds: 0,
      trackedSeconds: 0,
      keyPresses: 0,
      mouseClicks: 0,
      activeDays: 0,
    },
  )

  return {
    type: 'report.snapshot',
    view: 'calendar',
    snapshot: {
      contractVersion: 4,
      range: {
        from: '2026-07-23',
        toInclusive: '2026-08-05',
        timeZoneId: 'Asia/Saigon',
        dayCount: 14,
      },
      totals,
      calendar,
      hourOfWeek,
      trend,
      applications,
      quality: {
        hasData: true,
        firstSampleAt: '2026-07-23T08:00:00+07:00',
        lastSampleAt: '2026-08-05T17:45:00+07:00',
        sampleCount: calendar.reduce((total, cell) => total + cell.sampleCount, 0),
        coveredSeconds: totals.trackedSeconds,
        requestedSeconds: 14 * 24 * 60 * 60,
        coverageRatio: totals.trackedSeconds / (14 * 24 * 60 * 60),
      },
      aiUsage,
    },
  }
}
