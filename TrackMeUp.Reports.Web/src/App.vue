<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import AccessibleDataTable from './components/AccessibleDataTable.vue'
import AiUsageCard from './components/AiUsageCard.vue'
import ApplicationBars from './components/ApplicationBars.vue'
import CalendarHeatmap from './components/CalendarHeatmap.vue'
import CoverageNotice from './components/CoverageNotice.vue'
import HourOfWeekHeatmap from './components/HourOfWeekHeatmap.vue'
import KpiStrip from './components/KpiStrip.vue'
import TrendChart from './components/TrendChart.vue'
import {
  formatRange,
  type ReportEnvelope,
  type ReportView,
  validateReportEnvelope,
} from './reporting'
import {
  applyHostThemePreference,
  isThemePreference,
  themePreference,
  type ThemePreference,
} from './themePreference'
import { reportLocale, setReportLanguage, tr } from './localization'

type LoadState = 'waiting' | 'ready' | 'error'

const envelope = shallowRef<ReportEnvelope>()
const loadState = shallowRef<LoadState>('waiting')
const errorMessage = shallowRef('')
const developmentFixtureTitle = shallowRef('')
const developmentFixtureDescription = shallowRef('')
const searchQuery = ref('')
const themePreferenceNotice = ref('')
const themeSavePending = ref(!import.meta.env.DEV)
let missingSnapshotTimer: number | undefined

const themeOptions = computed<{ title: string; value: ThemePreference }[]>(() => [
  { title: tr('System', 'Sistema'), value: 'system' },
  { title: tr('Light', 'Chiaro'), value: 'light' },
  { title: tr('Dark', 'Scuro'), value: 'dark' },
])

const selectedTheme = computed<ThemePreference>({
  get: () => themePreference.value,
  set: (value) => requestThemePreference(value),
})

const viewCopy = computed<Record<ReportView, { title: string; description: string }>>(() => ({
  calendar: {
    title: tr('Daily activity', 'Attività giornaliera'),
    description: tr('Compare days and quickly spot continuity, peaks and interruptions.', 'Confronta i giorni del periodo e individua rapidamente continuità, picchi e interruzioni.'),
  },
  hourOfWeek: {
    title: tr('Time bands', 'Fasce orarie'),
    description: tr('See which hours and weekdays contain most of your active time.', 'Scopri in quali ore e giorni della settimana si concentra il tempo attivo.'),
  },
  trend: {
    title: tr('Trend', 'Andamento'),
    description: tr('Follow the balance between detected activity and inactivity over time.', 'Segui nel tempo il rapporto fra attività rilevata e inattività.'),
  },
  applications: {
    title: tr('Applications', 'Applicazioni'),
    description: tr('Compare the applications with the most active time in the period.', 'Confronta le applicazioni che hanno raccolto più tempo attivo nel periodo.'),
  },
}))

const snapshot = computed(() => envelope.value?.snapshot)
const selectedView = computed(() => envelope.value?.view ?? 'calendar')
const selectedCopy = computed(() => viewCopy.value[selectedView.value])
const rangeLabel = computed(() => snapshot.value ? formatRange(snapshot.value.range) : '')
const normalizedSearch = computed(() => searchQuery.value.trim().toLocaleLowerCase(reportLocale.value))
const searchLabel = computed(() => selectedView.value === 'applications'
  ? tr('Search applications', 'Cerca applicazione')
  : tr('Search the table', 'Cerca nella tabella'))

const displayedSnapshot = computed(() => {
  if (!snapshot.value || selectedView.value !== 'applications' || normalizedSearch.value.length === 0) {
    return snapshot.value
  }

  return {
    ...snapshot.value,
    applications: snapshot.value.applications.filter((application) =>
      application.application.toLocaleLowerCase(reportLocale.value).includes(normalizedSearch.value)),
  }
})

const selectedViewHasData = computed(() => {
  if (!displayedSnapshot.value?.quality.hasData) return false
  switch (selectedView.value) {
    case 'calendar':
      return displayedSnapshot.value.calendar.some((cell) => cell.hasData)
    case 'hourOfWeek':
      return displayedSnapshot.value.hourOfWeek.some((cell) => cell.hasData)
    case 'trend':
      return displayedSnapshot.value.trend.some((bucket) => bucket.hasData)
    case 'applications':
      return displayedSnapshot.value.applications.length > 0
  }
})

const emptyStateTitle = computed(() => normalizedSearch.value.length > 0
  ? tr('No results', 'Nessun risultato')
  : tr('No data for this view', 'Nessun dato per questa vista'))

const emptyStateText = computed(() => normalizedSearch.value.length > 0
  ? tr('No items match your search. Change or clear the search text.', 'La ricerca non corrisponde ad alcun elemento. Modifica o cancella il testo cercato.')
  : tr('The selected period does not contain enough samples. Try a different range.', 'Il periodo selezionato non contiene campioni sufficienti. Prova un intervallo diverso.'))

watch(selectedView, () => {
  searchQuery.value = ''
})

function showEnvelope(value: ReportEnvelope) {
  if (missingSnapshotTimer !== undefined) window.clearTimeout(missingSnapshotTimer)
  envelope.value = value
  errorMessage.value = ''
  loadState.value = 'ready'
}

function requestThemePreference(value: unknown) {
  if (!isThemePreference(value)) {
    themePreferenceNotice.value = tr('The requested theme preference is invalid.', 'La preferenza del tema richiesta non è valida.')
    return
  }

  const bridge = window.chrome?.webview
  if (!bridge) {
    themeSavePending.value = false
    if (import.meta.env.DEV) {
      applyHostThemePreference(value)
      themePreferenceNotice.value = ''
      return
    }

    themePreferenceNotice.value = tr('The theme cannot be saved because the TrackMeUp connection is unavailable.', 'Il tema non può essere salvato perché il collegamento con TrackMeUp non è disponibile.')
    return
  }

  themeSavePending.value = true
  themePreferenceNotice.value = ''
  bridge.postMessage({ type: 'report.theme.set', theme: value })
}

function handleThemeHostMessage(value: unknown): boolean {
  if (typeof value !== 'object' || value === null || !('type' in value)) return false

  if (value.type === 'report.theme.state') {
    themeSavePending.value = false
    if (!('theme' in value) || !applyHostThemePreference(value.theme)) {
      themePreferenceNotice.value = tr('TrackMeUp returned an invalid theme preference.', 'TrackMeUp ha restituito una preferenza del tema non valida.')
      return true
    }

    themePreferenceNotice.value = ''
    return true
  }

  if (value.type === 'report.theme.error') {
    themeSavePending.value = false
    if ('theme' in value) applyHostThemePreference(value.theme)
    const code = 'code' in value && typeof value.code === 'string' ? ` (${value.code})` : ''
    themePreferenceNotice.value = tr(`The theme preference was not saved${code}. The previous choice remains active.`, `La preferenza del tema non è stata salvata${code}. La scelta precedente resta attiva.`)
    return true
  }

  return false
}

function handleWebViewMessage(event: MessageEvent<unknown>) {
  if (typeof event.data === 'object'
    && event.data !== null
    && 'type' in event.data
    && event.data.type === 'report.language.state') {
    if ('language' in event.data) setReportLanguage(event.data.language)
    return
  }

  if (handleThemeHostMessage(event.data)) return

  const validation = validateReportEnvelope(event.data)
  if (validation.envelope) {
    showEnvelope(validation.envelope)
    return
  }

  if (typeof event.data === 'object'
    && event.data !== null
    && 'type' in event.data
    && event.data.type === 'report.snapshot') {
    if (missingSnapshotTimer !== undefined) window.clearTimeout(missingSnapshotTimer)
    envelope.value = undefined
    errorMessage.value = validation.error ?? tr('The received report is invalid.', 'Il report ricevuto non è valido.')
    loadState.value = 'error'
  }
}

onMounted(async () => {
  const bridge = window.chrome?.webview
  bridge?.addEventListener('message', handleWebViewMessage)
  bridge?.postMessage({ type: 'report.ready' })

  if (import.meta.env.DEV) {
    const fixture = await import('./dev/fixture')
    developmentFixtureTitle.value = fixture.developmentFixtureTitle
    developmentFixtureDescription.value = fixture.developmentFixtureDescription
    showEnvelope(fixture.buildDevelopmentEnvelope())
    return
  }

  missingSnapshotTimer = window.setTimeout(() => {
    if (loadState.value === 'waiting') {
      errorMessage.value = tr('The report is unavailable: TrackMeUp did not send a valid snapshot.', 'Il report non è disponibile: TrackMeUp non ha inviato uno snapshot valido.')
      loadState.value = 'error'
    }
  }, 5000)
})

onBeforeUnmount(() => {
  if (missingSnapshotTimer !== undefined) window.clearTimeout(missingSnapshotTimer)
  window.chrome?.webview?.removeEventListener('message', handleWebViewMessage)
})
</script>

<template>
  <v-app>
    <v-main>
      <main class="report-dashboard" aria-live="polite">
        <v-container fluid class="report-container">
          <div class="report-toolbar" :aria-label="tr('Report tools', 'Strumenti del report')">
            <v-text-field
              v-if="loadState === 'ready' && snapshot?.quality.hasData"
              v-model="searchQuery"
              class="report-search"
              :label="searchLabel"
              prepend-inner-icon="mdi-magnify"
              clearable
              hide-details
              density="compact"
              variant="outlined"
            />
            <v-spacer />
            <v-select
              v-model="selectedTheme"
              class="theme-selector"
              :items="themeOptions"
              item-title="title"
              item-value="value"
              :label="tr('Theme', 'Tema')"
              prepend-inner-icon="mdi-theme-light-dark"
              hide-details
              density="compact"
              variant="outlined"
              :aria-label="tr('Choose the report theme', 'Scegli il tema dei report')"
              :disabled="themeSavePending"
              :loading="themeSavePending"
            />
          </div>

          <v-alert
            v-if="themePreferenceNotice"
            class="mb-4"
            type="error"
            density="compact"
            variant="tonal"
            closable
            :text="themePreferenceNotice"
            role="status"
            @click:close="themePreferenceNotice = ''"
          />

          <v-alert
            v-if="developmentFixtureTitle"
            class="mb-4"
            color="primary"
            icon="mdi-flask-outline"
            variant="tonal"
            :title="developmentFixtureTitle"
            :text="developmentFixtureDescription"
          />

          <v-card
            v-if="loadState === 'waiting'"
            class="state-card"
            rounded="xl"
            variant="outlined"
          >
            <v-card-text class="state-card__content">
              <v-progress-circular color="primary" indeterminate :aria-label="tr('Loading report', 'Caricamento report')" />
              <div>
                <h1 class="text-h6">{{ tr('Preparing the report', 'Preparazione del report') }}</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">{{ tr('Waiting for data from TrackMeUp…', 'In attesa dei dati da TrackMeUp…') }}</p>
              </div>
            </v-card-text>
          </v-card>

          <v-alert
            v-else-if="loadState === 'error'"
            class="state-alert"
            type="error"
            variant="tonal"
            icon="mdi-alert-circle-outline"
            :title="tr('Unable to display the report', 'Impossibile mostrare il report')"
            :text="errorMessage"
          />

          <template v-else-if="snapshot">
            <section class="report-intro" aria-labelledby="report-view-title">
              <div>
                <p class="report-eyebrow">{{ rangeLabel }}</p>
                <h1 id="report-view-title" class="report-title">{{ selectedCopy.title }}</h1>
                <p class="report-description">{{ selectedCopy.description }}</p>
              </div>
              <v-chip
                class="report-timezone"
                color="primary"
                prepend-icon="mdi-map-clock-outline"
                size="small"
                variant="tonal"
              >
                {{ snapshot.range.timeZoneId }}
              </v-chip>
            </section>

            <KpiStrip :snapshot="snapshot" />
            <CoverageNotice :quality="snapshot.quality" />
            <AiUsageCard :ai-usage="snapshot.aiUsage" />

            <v-card
              v-if="selectedViewHasData"
              class="report-card chart-card"
              rounded="xl"
              variant="outlined"
            >
              <v-card-text>
                <CalendarHeatmap
                  v-if="selectedView === 'calendar'"
                  :snapshot="displayedSnapshot ?? snapshot"
                />
                <HourOfWeekHeatmap
                  v-else-if="selectedView === 'hourOfWeek'"
                  :snapshot="displayedSnapshot ?? snapshot"
                />
                <TrendChart
                  v-else-if="selectedView === 'trend'"
                  :snapshot="displayedSnapshot ?? snapshot"
                />
                <ApplicationBars
                  v-else
                  :snapshot="displayedSnapshot ?? snapshot"
                />
              </v-card-text>
            </v-card>

            <v-empty-state
              v-else
              class="report-card empty-state"
              icon="mdi-chart-box-outline"
              :headline="emptyStateTitle"
              :text="emptyStateText"
            />

            <AccessibleDataTable
              :snapshot="snapshot"
              :view="selectedView"
              :search="searchQuery"
            />
          </template>
        </v-container>
      </main>
    </v-main>
  </v-app>
</template>
