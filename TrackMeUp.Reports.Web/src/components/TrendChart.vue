<script setup lang="ts">
import '../chartRuntime'
import { computed } from 'vue'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { useChartSettings } from '../composables/useChartSettings'
import { tr } from '../localization'
import {
  formatDate,
  formatDecimal,
  formatDuration,
  formatInteger,
  type ReportSnapshot,
} from '../reporting'

const props = defineProps<{
  snapshot: ReportSnapshot
}>()

const { palette, reducedMotion } = useChartSettings()

const option = computed<EChartsOption>(() => {
  const buckets = props.snapshot.trend
  const labels = buckets.map((bucket) => formatDate(bucket.start))
  const showZoom = buckets.length > 45

  return {
    animation: !reducedMotion.value,
    aria: {
      enabled: true,
      description: tr('Stacked bar chart of active and idle time by period.'),
    },
    color: [palette.value.active, palette.value.idle],
    grid: {
      top: 34,
      right: 24,
      bottom: showZoom ? 78 : 52,
      left: 56,
    },
    legend: {
      top: 0,
      right: 8,
      itemWidth: 12,
      itemHeight: 8,
      textStyle: { color: palette.value.muted, fontSize: 11 },
      data: [tr('Active'), tr('Idle')],
    },
    tooltip: {
      trigger: 'axis',
      confine: true,
      renderMode: 'richText',
      axisPointer: { type: 'shadow' },
      formatter: (parameters: any) => {
        const entries = Array.isArray(parameters) ? parameters : [parameters]
        const index = entries[0]?.dataIndex as number | undefined
        const bucket = index === undefined ? undefined : buckets[index]
        if (!bucket?.hasData) return tr('No observed data')
        const period = bucket.start === bucket.endInclusive
          ? formatDate(bucket.start)
          : `${formatDate(bucket.start)} – ${formatDate(bucket.endInclusive)}`
        return [
          period,
          `${tr('Active')}: ${formatDuration(bucket.activeSeconds)}`,
          `${tr('Idle')}: ${formatDuration(bucket.idleSeconds)}`,
          `${tr('Keys')}: ${formatInteger(bucket.keyPresses)}`,
          `${tr('Clicks')}: ${formatInteger(bucket.mouseClicks)}`,
        ].join('\n')
      },
    },
    dataZoom: showZoom
      ? [{
          type: 'slider',
          height: 18,
          bottom: 10,
          borderColor: palette.value.border,
          fillerColor: `${palette.value.active}40`,
          textStyle: { color: palette.value.muted },
          startValue: Math.max(0, buckets.length - 45),
          endValue: buckets.length - 1,
        }, {
          type: 'inside',
          startValue: Math.max(0, buckets.length - 45),
          endValue: buckets.length - 1,
        }]
      : undefined,
    xAxis: {
      type: 'category',
      data: labels,
      axisLine: { lineStyle: { color: palette.value.border } },
      axisTick: { show: false },
      axisLabel: {
        color: palette.value.muted,
        fontSize: 10,
        hideOverlap: true,
      },
    },
    yAxis: {
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
    series: [{
      name: tr('Active'),
      type: 'bar',
      stack: 'tracked',
      barMaxWidth: 24,
      data: buckets.map((bucket) => bucket.hasData ? bucket.activeSeconds / 3600 : '-'),
      emphasis: { focus: 'series' },
    }, {
      name: tr('Idle'),
      type: 'bar',
      stack: 'tracked',
      barMaxWidth: 24,
      data: buckets.map((bucket) => bucket.hasData ? bucket.idleSeconds / 3600 : '-'),
      emphasis: { focus: 'series' },
    }],
  }
})
</script>

<template>
  <section aria-labelledby="trend-chart-title">
    <div class="chart-heading">
      <div>
        <h2 id="trend-chart-title" class="chart-title">{{ tr('Tracked time in the period') }}</h2>
        <p class="chart-subtitle">{{ tr('Active and idle time are stacked; intervals without data remain empty.') }}</p>
      </div>
    </div>
    <v-chart
      class="chart-canvas"
      :option="option"
      autoresize
      role="img"
      :aria-label="tr('Active and idle time trend')"
    />
    <p class="chart-forced-colors-message">
      {{ tr('The chart is hidden in high contrast mode. Open the accessible table below to inspect all values.') }}
    </p>
  </section>
</template>
