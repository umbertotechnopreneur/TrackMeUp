<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useChartSettings } from '../composables/useChartSettings'
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

const dayNames = ['Domenica', 'Lunedì', 'Martedì', 'Mercoledì', 'Giovedì', 'Venerdì', 'Sabato']

const headers = computed<DataTableHeader[]>(() => {
  switch (props.view) {
    case 'calendar':
      return [
        { title: 'Data', key: 'period' },
        { title: 'Disponibilità', key: 'availability' },
        { title: 'Attivo', key: 'active' },
        { title: 'Inattivo', key: 'idle' },
        { title: 'Tracciato', key: 'tracked' },
        { title: 'Campioni', key: 'samples' },
      ]
    case 'hourOfWeek':
      return [
        { title: 'Giorno', key: 'day' },
        { title: 'Ora', key: 'hour' },
        { title: 'Disponibilità', key: 'availability' },
        { title: 'Attivo medio', key: 'active' },
        { title: 'Inattivo medio', key: 'idle' },
        { title: 'Giorni osservati', key: 'observations' },
      ]
    case 'trend':
      return [
        { title: 'Periodo', key: 'period' },
        { title: 'Disponibilità', key: 'availability' },
        { title: 'Attivo', key: 'active' },
        { title: 'Inattivo', key: 'idle' },
        { title: 'Tasti', key: 'keys' },
        { title: 'Click', key: 'clicks' },
      ]
    case 'applications':
      return [
        { title: 'Applicazione', key: 'application' },
        { title: 'Tempo attivo', key: 'active' },
      ]
  }
})

const items = computed<Record<string, string>[]>(() => {
  switch (props.view) {
    case 'calendar':
      return props.snapshot.calendar.map((cell) => ({
        period: formatDate(cell.date),
        availability: cell.hasData ? 'Dati presenti' : 'Nessun dato',
        active: cell.hasData ? formatDuration(cell.activeSeconds) : '—',
        idle: cell.hasData ? formatDuration(cell.idleSeconds) : '—',
        tracked: cell.hasData ? formatDuration(cell.trackedSeconds) : '—',
        samples: cell.hasData ? formatInteger(cell.sampleCount) : '—',
      }))
    case 'hourOfWeek':
      return props.snapshot.hourOfWeek.map((cell) => ({
        day: dayNames[cell.dayOfWeek] ?? String(cell.dayOfWeek),
        hour: `${cell.hour.toString().padStart(2, '0')}:00`,
        availability: cell.hasData ? 'Dati presenti' : 'Nessun dato',
        active: cell.hasData ? formatDuration(cell.activeSeconds) : '—',
        idle: cell.hasData ? formatDuration(cell.idleSeconds) : '—',
        observations: cell.hasData ? formatInteger(cell.observationDays) : '—',
      }))
    case 'trend':
      return props.snapshot.trend.map((bucket) => ({
        period: bucket.start === bucket.endInclusive
          ? formatDate(bucket.start)
          : `${formatDate(bucket.start)} – ${formatDate(bucket.endInclusive)}`,
        availability: bucket.hasData ? 'Dati presenti' : 'Nessun dato',
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
            Dati in formato tabellare
          </span>
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <p class="fallback-table__note">
            Alternativa accessibile al grafico. Lo zero indica un valore misurato; “Nessun dato” indica un intervallo non osservato.
          </p>
          <v-data-table
            :headers="headers"
            :items="items"
            :search="search"
            :items-per-page="25"
            density="compact"
            aria-label="Dati del report in formato tabellare"
          />
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </v-card>
</template>
