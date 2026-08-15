<script setup lang="ts">
import { tr } from '../localization'
import { formatInteger, type AiUsageSlice } from '../reporting'

interface AiUsageBreakdownRow extends AiUsageSlice {
  displayLabel?: string
}

defineProps<{
  titleId: string
  title: string
  caption: string
  firstColumnLabel: string
  rows: readonly AiUsageBreakdownRow[]
  emptyText: string
  formatCost: (value: number | null) => string
}>()
</script>

<template>
  <section class="ai-usage-table" :aria-labelledby="titleId">
    <h3 :id="titleId" class="ai-usage-table__title">{{ title }}</h3>
    <v-table density="compact" class="ai-usage-table__table">
      <caption class="visually-hidden">{{ caption }}</caption>
      <thead>
        <tr>
          <th scope="col">{{ firstColumnLabel }}</th>
          <th scope="col" class="text-right">{{ tr('Requests') }}</th>
          <th scope="col" class="text-right">{{ tr('Input') }}</th>
          <th scope="col" class="text-right">{{ tr('Output') }}</th>
          <th scope="col" class="text-right">{{ tr('Total') }}</th>
          <th scope="col" class="text-right">{{ tr('Reported') }}</th>
          <th scope="col" class="text-right">{{ tr('Estimated') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="slice in rows" :key="slice.label">
          <th scope="row">{{ slice.displayLabel ?? slice.label }}</th>
          <td class="text-right">{{ formatInteger(slice.requestCount) }}</td>
          <td class="text-right">{{ formatInteger(slice.inputTokens) }}</td>
          <td class="text-right">{{ formatInteger(slice.outputTokens) }}</td>
          <td class="text-right">{{ formatInteger(slice.totalTokens) }}</td>
          <td class="text-right">{{ formatCost(slice.actualCostUsd) }}</td>
          <td class="text-right">{{ formatCost(slice.estimatedCostUsd) }}</td>
        </tr>
        <tr v-if="rows.length === 0">
          <td colspan="7" class="text-medium-emphasis">{{ emptyText }}</td>
        </tr>
      </tbody>
    </v-table>
  </section>
</template>

<style scoped>
.ai-usage-table {
  overflow: hidden;
  border: 1px solid rgb(var(--v-theme-outline-variant));
  border-radius: 10px;
}

.ai-usage-table__title {
  margin: 0;
  padding: 12px 12px 5px;
  color: rgb(var(--v-theme-on-surface));
  font-size: 0.88rem;
  font-weight: 680;
}

.ai-usage-table__table {
  font-size: 0.78rem;
}

.ai-usage-table__table :deep(th),
.ai-usage-table__table :deep(td) {
  padding-inline: 8px;
  white-space: nowrap;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@media (max-width: 640px) {
  .ai-usage-table {
    overflow-x: auto;
  }
}
</style>
