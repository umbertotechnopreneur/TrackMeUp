<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useChartSettings } from '../composables/useChartSettings'
import { reportDayNames, tr } from '../localization'
import {
  formatDate,
  formatDuration,
  formatInteger,
  type ReportSnapshot,
  type ReportView,
} from '../reporting'

interface DataTableHeader {
  title: string
  key: string
  sortable?: boolean
}

const props = defineProps<{
  snapshot: ReportSnapshot
  view: ReportView
  search: string
}>()

const { forcedColors } = useChartSettings()
const expanded = ref<number[]>(forcedColors.value ? [0] : [])

watch(forcedColors, (enabled) => {
  if (enabled && !expanded.value.includes(0)) expanded.value = [0]
})

const dayNames = computed(() => reportDayNames())

const headers = computed<DataTableHeader[]>(() => {
  switch (props.view) {
    case 'calendar':
      return [
        { title: tr('Date', 'Data'), key: 'period' },
        { title: tr('Availability', 'Disponibilità'), key: 'availability' },
        { title: tr('Activity score', 'Punteggio attività'), key: 'score' },
        { title: tr('Active', 'Attivo'), key: 'active' },
        { title: tr('Idle', 'Inattivo'), key: 'idle' },
        { title: tr('Tracked', 'Tracciato'), key: 'tracked' },
        { title: tr('Samples', 'Campioni'), key: 'samples' },
      ]
    case 'hourOfWeek':
      return [
        { title: tr('Day', 'Giorno'), key: 'day' },
        { title: tr('Hour', 'Ora'), key: 'hour' },
        { title: tr('Availability', 'Disponibilità'), key: 'availability' },
        { title: tr('Average active', 'Attivo medio'), key: 'active' },
        { title: tr('Average idle', 'Inattivo medio'), key: 'idle' },
        { title: tr('Observed days', 'Giorni osservati'), key: 'observations' },
      ]
    case 'trend':
      return [
        { title: tr('Period', 'Periodo'), key: 'period' },
        { title: tr('Availability', 'Disponibilità'), key: 'availability' },
        { title: tr('Active', 'Attivo'), key: 'active' },
        { title: tr('Idle', 'Inattivo'), key: 'idle' },
        { title: tr('Keys', 'Tasti'), key: 'keys' },
        { title: 'Click', key: 'clicks' },
      ]
    case 'applications':
      return [
        { title: tr('Application', 'Applicazione'), key: 'application' },
        { title: tr('Active time', 'Tempo attivo'), key: 'active' },
      ]
  }
})

const items = computed<Record<string, string>[]>(() => {
  switch (props.view) {
    case 'calendar':
      return props.snapshot.calendar.map((cell) => ({
        period: formatDate(cell.date),
        availability: cell.hasData ? tr('Data available', 'Dati presenti') : tr('No data', 'Nessun dato'),
        score: cell.activityScore === null ? '—' : `${formatInteger(cell.activityScore)}/100`,
        active: cell.hasData ? formatDuration(cell.activeSeconds) : '—',
        idle: cell.hasData ? formatDuration(cell.idleSeconds) : '—',
        tracked: cell.hasData ? formatDuration(cell.trackedSeconds) : '—',
        samples: cell.hasData ? formatInteger(cell.sampleCount) : '—',
      }))
    case 'hourOfWeek':
      return props.snapshot.hourOfWeek.map((cell) => ({
        day: dayNames.value[cell.dayOfWeek] ?? String(cell.dayOfWeek),
        hour: `${cell.hour.toString().padStart(2, '0')}:00`,
        availability: cell.hasData ? tr('Data available', 'Dati presenti') : tr('No data', 'Nessun dato'),
        active: cell.hasData ? formatDuration(cell.activeSeconds) : '—',
        idle: cell.hasData ? formatDuration(cell.idleSeconds) : '—',
        observations: cell.hasData ? formatInteger(cell.observationDays) : '—',
      }))
    case 'trend':
      return props.snapshot.trend.map((bucket) => ({
        period: bucket.start === bucket.endInclusive
          ? formatDate(bucket.start)
          : `${formatDate(bucket.start)} – ${formatDate(bucket.endInclusive)}`,
        availability: bucket.hasData ? tr('Data available', 'Dati presenti') : tr('No data', 'Nessun dato'),
        active: bucket.hasData ? formatDuration(bucket.activeSeconds) : '—',
        idle: bucket.hasData ? formatDuration(bucket.idleSeconds) : '—',
        keys: bucket.hasData ? formatInteger(bucket.keyPresses) : '—',
        clicks: bucket.hasData ? formatInteger(bucket.mouseClicks) : '—',
      }))
    case 'applications':
      return props.snapshot.applications.map((application) => ({
        application: application.application,
        active: formatDuration(application.activeSeconds),
      }))
  }
})
</script>

<template>
  <v-card class="report-card fallback-table" rounded="xl" variant="outlined">
    <v-expansion-panels v-model="expanded" multiple variant="accordion">
      <v-expansion-panel :value="0">
        <v-expansion-panel-title>
          <span>
            <v-icon icon="mdi-table-eye" start color="primary" aria-hidden="true" />
            {{ tr('Tabular data', 'Dati in formato tabellare') }}
          </span>
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <p class="fallback-table__note">
            {{ tr('Accessible alternative to the chart. Zero indicates a measured value; “No data” indicates an unobserved interval.', 'Alternativa accessibile al grafico. Lo zero indica un valore misurato; “Nessun dato” indica un intervallo non osservato.') }}
          </p>
          <v-data-table
            :headers="headers"
            :items="items"
            :search="search"
            :items-per-page="25"
            density="compact"
            :aria-label="tr('Report data in tabular format', 'Dati del report in formato tabellare')"
          />
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </v-card>
</template>
