<script setup lang="ts">
import '../chartRuntime'
import { computed } from 'vue'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { useChartSettings } from '../composables/useChartSettings'
import { reportDayNames, reportHourLabel, tr } from '../localization'
import { formatDuration, formatInteger, type ReportSnapshot } from '../reporting'

const props = defineProps<{
  snapshot: ReportSnapshot
}>()

const { palette, reducedMotion } = useChartSettings()
const dayNames = computed(() => {
  const names = reportDayNames(true)
  return [names[1], names[2], names[3], names[4], names[5], names[6], names[0]]
})

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
      description: tr('Average active-time map by weekday and hour.'),
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
        if (!cell) return tr('Data unavailable')
        const fullDayName = reportDayNames()[cell.dayOfWeek]
        return [
          `${fullDayName}, ${reportHourLabel(cell.hour)}`,
          `${tr('Average active')}: ${formatDuration(cell.activeSeconds)}`,
          `${tr('Average idle')}: ${formatDuration(cell.idleSeconds)}`,
          `${tr('Observed days')}: ${formatInteger(cell.observationDays)}`,
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
      text: [tr('More activity'), tr('Less activity')],
      textGap: 9,
      textStyle: {
        color: palette.value.muted,
        fontFamily: '"Segoe UI Variable", "Segoe UI", "Microsoft YaHei UI", "Malgun Gothic", sans-serif',
        fontSize: 11,
      },
      inRange: { color: palette.value.heat },
    },
    xAxis: {
      type: 'category',
      data: Array.from({ length: 24 }, (_, hour) => reportHourLabel(hour)),
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
      data: dayNames.value,
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
        <h2 id="hour-chart-title" class="chart-title">{{ tr('Activity by day and hour') }}</h2>
        <p class="chart-subtitle">{{ tr('Average active time in the observed time bands.') }}</p>
      </div>
      <span class="chart-no-data-legend" :aria-label="tr('Legend: gray means no data')">
        <span class="chart-no-data-swatch" aria-hidden="true" />
        {{ tr('No data') }}
      </span>
    </div>
    <v-chart
      class="chart-canvas"
      :option="option"
      autoresize
      role="img"
      :aria-label="tr('Average active-time map by weekday and hour')"
    />
    <p class="chart-forced-colors-message">
      {{ tr('The chart is hidden in high contrast mode. Open the accessible table below to inspect all values.') }}
    </p>
  </section>
</template>
