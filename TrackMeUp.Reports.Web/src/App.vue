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

const themeOptions: { title: string; value: ThemePreference }[] = [
  { title: 'Sistema', value: 'system' },
  { title: 'Chiaro', value: 'light' },
  { title: 'Scuro', value: 'dark' },
]

const selectedTheme = computed<ThemePreference>({
  get: () => themePreference.value,
  set: (value) => requestThemePreference(value),
})

const viewCopy: Record<ReportView, { title: string; description: string }> = {
  calendar: {
    title: 'Attività giornaliera',
    description: 'Confronta i giorni del periodo e individua rapidamente continuità, picchi e interruzioni.',
  },
  hourOfWeek: {
    title: 'Fasce orarie',
    description: 'Scopri in quali ore e giorni della settimana si concentra il tempo attivo.',
  },
  trend: {
    title: 'Andamento',
    description: 'Segui nel tempo il rapporto fra attività rilevata e inattività.',
  },
  applications: {
    title: 'Applicazioni',
    description: 'Confronta le applicazioni che hanno raccolto più tempo attivo nel periodo.',
  },
}

const snapshot = computed(() => envelope.value?.snapshot)
const selectedView = computed(() => envelope.value?.view ?? 'calendar')
const selectedCopy = computed(() => viewCopy[selectedView.value])
const rangeLabel = computed(() => snapshot.value ? formatRange(snapshot.value.range) : '')
const normalizedSearch = computed(() => searchQuery.value.trim().toLocaleLowerCase('it-IT'))
const searchLabel = computed(() => selectedView.value === 'applications'
  ? 'Cerca applicazione'
  : 'Cerca nella tabella')

const displayedSnapshot = computed(() => {
  if (!snapshot.value || selectedView.value !== 'applications' || normalizedSearch.value.length === 0) {
    return snapshot.value
  }

  return {
    ...snapshot.value,
    applications: snapshot.value.applications.filter((application) =>
      application.application.toLocaleLowerCase('it-IT').includes(normalizedSearch.value)),
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
  ? 'Nessun risultato'
  : 'Nessun dato per questa vista')

const emptyStateText = computed(() => normalizedSearch.value.length > 0
  ? 'La ricerca non corrisponde ad alcun elemento. Modifica o cancella il testo cercato.'
  : 'Il periodo selezionato non contiene campioni sufficienti. Prova un intervallo diverso.')

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
    themePreferenceNotice.value = 'La preferenza del tema richiesta non è valida.'
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

    themePreferenceNotice.value = 'Il tema non può essere salvato perché il collegamento con TrackMeUp non è disponibile.'
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
      themePreferenceNotice.value = 'TrackMeUp ha restituito una preferenza del tema non valida.'
      return true
    }

    themePreferenceNotice.value = ''
    return true
  }

  if (value.type === 'report.theme.error') {
    themeSavePending.value = false
    if ('theme' in value) applyHostThemePreference(value.theme)
    const code = 'code' in value && typeof value.code === 'string' ? ` (${value.code})` : ''
    themePreferenceNotice.value = `La preferenza del tema non è stata salvata${code}. La scelta precedente resta attiva.`
    return true
  }

  return false
}

function handleWebViewMessage(event: MessageEvent<unknown>) {
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
    errorMessage.value = validation.error ?? 'Il report ricevuto non è valido.'
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
      errorMessage.value = 'Il report non è disponibile: TrackMeUp non ha inviato uno snapshot valido.'
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
          <div class="report-toolbar" aria-label="Strumenti del report">
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
              label="Tema"
              prepend-inner-icon="mdi-theme-light-dark"
              hide-details
              density="compact"
              variant="outlined"
              aria-label="Scegli il tema dei report"
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
              <v-progress-circular color="primary" indeterminate aria-label="Caricamento report" />
              <div>
                <h1 class="text-h6">Preparazione del report</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">In attesa dei dati da TrackMeUp…</p>
              </div>
            </v-card-text>
          </v-card>

          <v-alert
            v-else-if="loadState === 'error'"
            class="state-alert"
            type="error"
            variant="tonal"
            icon="mdi-alert-circle-outline"
            title="Impossibile mostrare il report"
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
