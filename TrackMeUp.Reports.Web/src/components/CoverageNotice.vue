<script setup lang="ts">
import { computed } from 'vue'
import {
  formatDateTime,
  formatPercent,
  type ReportDataQuality,
} from '../reporting'
import { tr } from '../localization'
import { reportIcons } from '../icons'

const props = defineProps<{
  quality: ReportDataQuality
}>()

const title = computed(() => props.quality.hasData
  ? tr('Time coverage: {percent}', { percent: formatPercent(props.quality.coverageRatio) })
  : tr('No data in the period'))

const description = computed(() => {
  if (!props.quality.hasData) {
    return tr('TrackMeUp detected no samples in the selected range. Missing data is not counted as idle time.')
  }

  const first = formatDateTime(props.quality.firstSampleAt)
  const last = formatDateTime(props.quality.lastSampleAt)
  return tr('Samples observed from {first} to {last}. Uncovered intervals are not interpreted as idle time.', { first, last })
})
</script>

<template>
  <v-alert
    class="coverage-notice mt-4"
    color="primary"
    :icon="quality.hasData ? reportIcons.information : reportIcons.databaseUnavailable"
    variant="tonal"
    :title="title"
    :text="description"
  />
</template>

<style scoped>
.coverage-notice :deep(.v-alert-title) {
  font-size: 0.9rem;
  font-weight: 650;
}

.coverage-notice :deep(.v-alert__content) {
  font-size: 0.82rem;
}
</style>
