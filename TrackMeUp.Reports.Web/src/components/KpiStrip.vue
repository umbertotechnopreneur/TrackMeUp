<script setup lang="ts">
import { computed } from 'vue'
import {
  formatDuration,
  formatInteger,
  formatPercent,
  type ReportSnapshot,
} from '../reporting'
import { tr } from '../localization'

const props = defineProps<{
  snapshot: ReportSnapshot
}>()

const hasData = computed(() => props.snapshot.quality.hasData)
const unavailable = '—'

const items = computed(() => {
  const topApplication = props.snapshot.applications[0]
  const activeDays = props.snapshot.totals.activeDays
  const averageSeconds = activeDays > 0
    ? props.snapshot.totals.activeSeconds / activeDays
    : 0

  return [
    {
      icon: 'mdi-clock-check-outline',
      label: tr('Active time'),
      value: hasData.value ? formatDuration(props.snapshot.totals.activeSeconds) : unavailable,
      detail: tr('In the selected period'),
    },
    {
      icon: 'mdi-coffee-outline',
      label: tr('Idle time'),
      value: hasData.value ? formatDuration(props.snapshot.totals.idleSeconds) : unavailable,
      detail: tr('While tracking'),
    },
    {
      icon: 'mdi-calendar-check-outline',
      label: tr('Active days'),
      value: hasData.value
        ? `${formatInteger(activeDays)} / ${formatInteger(props.snapshot.range.dayCount)}`
        : unavailable,
      detail: tr('Days with activity'),
    },
    {
      icon: 'mdi-chart-timeline-variant',
      label: tr('Daily average'),
      value: hasData.value ? formatDuration(averageSeconds) : unavailable,
      detail: tr('Across active days'),
    },
    {
      icon: 'mdi-apps',
      label: tr('Top application'),
      value: hasData.value && topApplication ? topApplication.application : unavailable,
      detail: topApplication ? formatDuration(topApplication.activeSeconds) : tr('No data available'),
    },
    {
      icon: 'mdi-radar',
      label: tr('Coverage'),
      value: hasData.value ? formatPercent(props.snapshot.quality.coverageRatio) : unavailable,
      detail: tr('{count} samples', { count: formatInteger(props.snapshot.quality.sampleCount) }),
    },
  ]
})
</script>

<template>
  <section :aria-label="tr('Key indicators')">
    <v-row dense>
      <v-col
        v-for="item in items"
        :key="item.label"
        cols="12"
        sm="6"
        md="4"
        lg="2"
      >
        <v-card
          class="report-card kpi-card h-100"
          rounded="lg"
          variant="outlined"
          :aria-label="`${item.label}: ${item.value}. ${item.detail}`"
        >
          <v-card-text class="kpi-card__content">
            <v-icon :icon="item.icon" color="primary" size="20" aria-hidden="true" />
            <div class="kpi-card__label">{{ item.label }}</div>
            <div class="kpi-card__value" :title="item.value">{{ item.value }}</div>
            <div class="kpi-card__detail">{{ item.detail }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </section>
</template>

<style scoped>
.kpi-card__content {
  display: grid;
  grid-template-rows: auto auto minmax(2.1rem, auto) auto;
  gap: 6px;
  min-height: 138px;
  padding: 16px !important;
}

.kpi-card__label {
  color: rgb(var(--v-theme-on-surface-variant));
  font-size: 0.75rem;
  font-weight: 650;
  letter-spacing: 0.035em;
  text-transform: uppercase;
}

.kpi-card__value {
  overflow: hidden;
  color: rgb(var(--v-theme-on-surface));
  font-size: clamp(1.05rem, 1.9vw, 1.35rem);
  font-weight: 680;
  letter-spacing: -0.02em;
  line-height: 1.15;
  text-overflow: ellipsis;
}

.kpi-card__detail {
  align-self: end;
  color: rgb(var(--v-theme-on-surface-variant));
  font-size: 0.72rem;
}
</style>
