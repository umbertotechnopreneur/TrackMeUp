<script setup lang="ts">
import { computed } from 'vue'
import {
  formatDateTime,
  formatPercent,
  type ReportDataQuality,
} from '../reporting'
import { tr } from '../localization'

const props = defineProps<{
  quality: ReportDataQuality
}>()

const title = computed(() => props.quality.hasData
  ? `${tr('Time coverage', 'Copertura temporale')} ${formatPercent(props.quality.coverageRatio)}`
  : tr('No data in the period', 'Nessun dato nel periodo'))

const description = computed(() => {
  if (!props.quality.hasData) {
    return tr('TrackMeUp detected no samples in the selected range. Missing data is not counted as idle time.', 'TrackMeUp non ha rilevato campioni nel range selezionato. L’assenza di dati non viene conteggiata come inattività.')
  }

  const first = formatDateTime(props.quality.firstSampleAt)
  const last = formatDateTime(props.quality.lastSampleAt)
  return tr(`Samples observed from ${first} to ${last}. Uncovered intervals are not interpreted as idle time.`, `Campioni osservati dal ${first} al ${last}. Gli intervalli non coperti non vengono interpretati come tempo inattivo.`)
})
</script>

<template>
  <v-alert
    class="coverage-notice mt-4"
    color="primary"
    :icon="quality.hasData ? 'mdi-information-outline' : 'mdi-database-off-outline'"
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
