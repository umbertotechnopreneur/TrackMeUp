<script setup lang="ts">
import { computed } from 'vue'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { useChartSettings } from '../composables/useChartSettings'
import { formatDuration, type ReportSnapshot } from '../reporting'

const props = defineProps<{
  snapshot: ReportSnapshot
}>()

const { palette, reducedMotion } = useChartSettings()
const dayNames = ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom']

const option = computed<EChartsOption>(() => {
  const cells = props.snapshot.hourOfWeek
  const maxMinutes = Math.max(1, ...cells.filter((cell) => cell.hasData).map((cell) => cell.activeSeconds / 60))
  const data = cells
    .map((cell, index) => ({ cell, index }))
    .filter(({ cell }) => cell.hasData)
    .map(({ cell, index }) => {
      const mondayFirstDay = cell.dayOfWeek === 0 ? 6 : cell.dayOfWeek - 1
      return [cell.hour, mondayFirstDay, cell.activeSeconds / 60, index]
    })

  return {
    animation: !reducedMotion.value,
    aria: {
      enabled: true,
      description: 'Mappa del tempo attivo medio per giorno della settimana e fascia oraria.',
    },
    grid: {
      top: 24,
      right: 24,
      bottom: 68,
      left: 48,
      containLabel: false,
    },
    tooltip: {
      confine: true,
      position: 'top',
      renderMode: 'richText',
      formatter: (parameters: any) => {
        const raw = parameters.data as [number, number, number, number]
        const cell = cells[raw[3]]
        if (!cell) return 'Dato non disponibile'
        const fullDayName = ['Domenica', 'Lunedì', 'Martedì', 'Mercoledì', 'Giovedì', 'Venerdì', 'Sabato'][cell.dayOfWeek]
        return [
          `${fullDayName}, ${cell.hour.toString().padStart(2, '0')}:00`,
          `Attivo medio: ${formatDuration(cell.activeSeconds)}`,
          `Inattivo medio: ${formatDuration(cell.idleSeconds)}`,
          `Giorni osservati: ${cell.observationDays}`,
        ].join('\n')
      },
    },
    visualMap: {
      min: 0,
      max: maxMinutes,
      calculable: false,
      orient: 'horizontal',
      left: 'center',
      bottom: 2,
      itemWidth: 130,
      itemHeight: 9,
      text: ['Più attività', 'Meno attività'],
      textGap: 9,
      textStyle: {
        color: palette.value.muted,
        fontFamily: 'Segoe UI',
        fontSize: 11,
      },
      inRange: { color: palette.value.heat },
    },
    xAxis: {
      type: 'category',
      data: Array.from({ length: 24 }, (_, hour) => hour.toString().padStart(2, '0')),
      axisLine: { lineStyle: { color: palette.value.border } },
      axisTick: { show: false },
      axisLabel: {
        color: palette.value.muted,
        fontSize: 10,
        interval: 1,
      },
      splitArea: {
        show: true,
        areaStyle: { color: [palette.value.noData] },
      },
    },
    yAxis: {
      type: 'category',
      inverse: true,
      data: dayNames,
      axisLine: { lineStyle: { color: palette.value.border } },
      axisTick: { show: false },
      axisLabel: { color: palette.value.muted, fontSize: 11 },
      splitArea: {
        show: true,
        areaStyle: { color: [palette.value.noData] },
      },
    },
    series: [{
      type: 'heatmap',
      data,
      emphasis: {
        itemStyle: {
          borderColor: palette.value.text,
          borderWidth: 2,
        },
      },
      itemStyle: {
        borderColor: palette.value.border,
        borderWidth: 1,
      },
    }],
  }
})
</script>

<template>
  <section aria-labelledby="hour-chart-title">
    <div class="chart-heading">
      <div>
        <h2 id="hour-chart-title" class="chart-title">Attività per giorno e ora</h2>
        <p class="chart-subtitle">Media del tempo attivo nelle fasce osservate del periodo.</p>
      </div>
      <span class="chart-no-data-legend" aria-label="Legenda: grigio uguale nessun dato">
        <span class="chart-no-data-swatch" aria-hidden="true" />
        Nessun dato
      </span>
    </div>
    <v-chart
      class="chart-canvas"
      :option="option"
      autoresize
      role="img"
      aria-label="Mappa del tempo attivo medio per giorno della settimana e ora"
    />
    <p class="chart-forced-colors-message">
      Il grafico è nascosto in modalità contrasto elevato. Apri la tabella accessibile qui sotto per consultare tutti i valori.
    </p>
  </section>
</template>
