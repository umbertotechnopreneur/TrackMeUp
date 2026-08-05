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

const option = computed<EChartsOption>(() => {
  const applications = [...props.snapshot.applications]
    .sort((left, right) => right.activeSeconds - left.activeSeconds)

  return {
    animation: !reducedMotion.value,
    aria: {
      enabled: true,
      description: 'Grafico a barre orizzontali delle applicazioni con più tempo attivo.',
    },
    grid: {
      top: 14,
      right: 36,
      bottom: 30,
      left: 132,
      containLabel: false,
    },
    tooltip: {
      trigger: 'axis',
      confine: true,
      renderMode: 'richText',
      axisPointer: { type: 'shadow' },
      formatter: (parameters: any) => {
        const entry = Array.isArray(parameters) ? parameters[0] : parameters
        const application = applications[entry?.dataIndex as number]
        return application
          ? `${application.application}\nTempo attivo: ${formatDuration(application.activeSeconds)}`
          : 'Dato non disponibile'
      },
    },
    xAxis: {
      type: 'value',
      name: 'ore',
      nameTextStyle: { color: palette.value.muted, fontSize: 10 },
      axisLabel: { color: palette.value.muted, fontSize: 10 },
      splitLine: { lineStyle: { color: palette.value.border, type: 'dashed' } },
    },
    yAxis: {
      type: 'category',
      inverse: true,
      data: applications.map((application) => application.application),
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: {
        color: palette.value.text,
        fontSize: 11,
        width: 112,
        overflow: 'truncate',
      },
    },
    series: [{
      name: 'Tempo attivo',
      type: 'bar',
      data: applications.map((application) => application.activeSeconds / 3600),
      barMaxWidth: 22,
      itemStyle: {
        color: palette.value.active,
        borderRadius: [0, 5, 5, 0],
      },
      emphasis: {
        itemStyle: { color: palette.value.active },
      },
    }],
  }
})
</script>

<template>
  <section aria-labelledby="applications-chart-title">
    <div class="chart-heading">
      <div>
        <h2 id="applications-chart-title" class="chart-title">Applicazioni principali</h2>
        <p class="chart-subtitle">Prime dodici applicazioni e aggregato delle altre, ordinati per tempo attivo.</p>
      </div>
    </div>
    <v-chart
      class="chart-canvas chart-canvas--applications"
      :option="option"
      autoresize
      role="img"
      aria-label="Applicazioni ordinate per tempo attivo"
    />
    <p class="chart-forced-colors-message">
      Il grafico è nascosto in modalità contrasto elevato. Apri la tabella accessibile qui sotto per consultare tutti i valori.
    </p>
  </section>
</template>
