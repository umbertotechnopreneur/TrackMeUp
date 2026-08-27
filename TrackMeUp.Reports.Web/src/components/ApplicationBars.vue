<script setup lang="ts">
import '../chartRuntime'
import { computed } from 'vue'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { useChartSettings } from '../composables/useChartSettings'
import { tr } from '../localization'
import { formatDecimal, formatDuration, type ReportSnapshot } from '../reporting'

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
      description: tr('Horizontal bar chart of applications with the most active time.'),
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
          ? `${application.application}\n${tr('Active time')}: ${formatDuration(application.activeSeconds)}`
          : tr('Data unavailable')
      },
    },
    xAxis: {
      type: 'value',
      name: tr('hours'),
      nameTextStyle: { color: palette.value.muted, fontSize: 10 },
      axisLabel: {
        color: palette.value.muted,
        fontSize: 10,
        formatter: (value: number) => formatDecimal(value),
      },
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
      name: tr('Active time'),
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
        <h2 id="applications-chart-title" class="chart-title">{{ tr('Top applications') }}</h2>
        <p class="chart-subtitle">{{ tr('Top twelve applications plus the remaining aggregate, ordered by active time.') }}</p>
      </div>
    </div>
    <v-chart
      class="chart-canvas chart-canvas--applications"
      :option="option"
      autoresize
      role="img"
      :aria-label="tr('Applications ordered by active time')"
    />
    <p class="chart-forced-colors-message">
      {{ tr('The chart is hidden in high contrast mode. Open the accessible table below to inspect all values.') }}
    </p>
  </section>
</template>
