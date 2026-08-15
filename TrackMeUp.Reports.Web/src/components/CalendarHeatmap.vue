<script setup lang="ts">
import { computed } from 'vue'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { useChartSettings } from '../composables/useChartSettings'
import { reportDayNames, reportMonthNames, tr } from '../localization'
import {
  formatDate,
  formatDuration,
  formatInteger,
  type ReportSnapshot,
} from '../reporting'

const props = defineProps<{
  snapshot: ReportSnapshot
}>()

const { palette, reducedMotion } = useChartSettings()

const option = computed<EChartsOption>(() => {
  const cells = props.snapshot.calendar
  const maxMinutes = Math.max(1, ...cells.filter((cell) => cell.hasData).map((cell) => cell.activeSeconds / 60))
  const data = cells
    .map((cell, index) => ({ cell, index }))
    .filter(({ cell }) => cell.hasData)
    .map(({ cell, index }) => [cell.date, cell.activeSeconds / 60, index])

  return {
    animation: !reducedMotion.value,
    aria: {
      enabled: true,
      description: tr('Daily activity map. A stronger shade indicates more active time.', 'Mappa dell’attività giornaliera. Una tonalità più intensa indica più tempo attivo.'),
    },
    tooltip: {
      confine: true,
      position: 'top',
      renderMode: 'richText',
      formatter: (parameters: any) => {
        const raw = parameters.data as [string, number, number]
        const cell = cells[raw[2]]
        if (!cell) return tr('Data unavailable', 'Dato non disponibile')
        return [
          formatDate(cell.date),
          `${tr('Activity score', 'Punteggio attività')}: ${formatInteger(cell.activityScore ?? 0)}/100`,
          `${tr('Active', 'Attivo')}: ${formatDuration(cell.activeSeconds)}`,
          `${tr('Idle', 'Inattivo')}: ${formatDuration(cell.idleSeconds)}`,
          `${tr('Coverage', 'Copertura')}: ${formatDuration(cell.trackedSeconds)}`,
          `${tr('Samples', 'Campioni')}: ${formatInteger(cell.sampleCount)}`,
        ].join('\n')
      },
    },
    visualMap: {
      min: 0,
      max: maxMinutes,
      calculable: false,
      orient: 'horizontal',
      left: 'center',
      bottom: 4,
      itemWidth: 130,
      itemHeight: 9,
      text: [tr('More activity', 'Più attività'), tr('Less activity', 'Meno attività')],
      textGap: 9,
      textStyle: {
        color: palette.value.muted,
        fontFamily: 'Segoe UI',
        fontSize: 11,
      },
      inRange: { color: palette.value.heat },
    },
    calendar: {
      range: [props.snapshot.range.from, props.snapshot.range.toInclusive],
      top: 48,
      left: 42,
      right: 26,
      bottom: 62,
      cellSize: ['auto', 17],
      orient: 'horizontal',
      itemStyle: {
        color: palette.value.noData,
        borderColor: palette.value.border,
        borderWidth: 2,
      },
      splitLine: { show: false },
      yearLabel: { show: false },
      monthLabel: {
        color: palette.value.muted,
        fontSize: 11,
        margin: 10,
        nameMap: reportMonthNames(),
      },
      dayLabel: {
        firstDay: 1,
        color: palette.value.muted,
        fontSize: 10,
        margin: 8,
        nameMap: reportDayNames(true),
      },
    },
    series: [{
      type: 'heatmap',
      coordinateSystem: 'calendar',
      data,
      emphasis: {
        itemStyle: {
          borderColor: palette.value.text,
          borderWidth: 2,
        },
      },
    }],
  }
})
</script>

<template>
  <section aria-labelledby="calendar-chart-title">
    <div class="chart-heading">
      <div>
        <h2 id="calendar-chart-title" class="chart-title">{{ tr('Activity calendar', 'Calendario dell’attività') }}</h2>
        <p class="chart-subtitle">{{ tr('Active time by day; gray cells indicate missing data.', 'Tempo attivo per giorno; le celle grigie indicano assenza di dati.') }}</p>
      </div>
      <span class="chart-no-data-legend" :aria-label="tr('Legend: gray means no data', 'Legenda: grigio uguale nessun dato')">
        <span class="chart-no-data-swatch" aria-hidden="true" />
        {{ tr('No data', 'Nessun dato') }}
      </span>
    </div>
    <v-chart
      class="chart-canvas chart-canvas--calendar"
      :option="option"
      autoresize
      role="img"
      :aria-label="tr('Active time map by day', 'Mappa del tempo attivo per giorno')"
    />
    <p class="chart-forced-colors-message">
      {{ tr('The chart is hidden in high contrast mode. Open the accessible table below to inspect all values.', 'Il grafico è nascosto in modalità contrasto elevato. Apri la tabella accessibile qui sotto per consultare tutti i valori.') }}
    </p>
  </section>
</template>
