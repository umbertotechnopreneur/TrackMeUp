<script setup lang="ts">
import { computed } from 'vue'
import { formatInteger, type AiUsageSummary } from '../reporting'

const props = defineProps<{
  aiUsage: AiUsageSummary
}>()

const currencyFormatter = new Intl.NumberFormat('it-IT', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 6,
})

const formatUsd = (value: number | null): string =>
  value === null ? 'Non disponibile' : currencyFormatter.format(value)

const costNotice = computed(() => {
  const usage = props.aiUsage
  if (usage.requestCount === 0) {
    return {
      color: 'primary',
      icon: 'mdi-information-outline',
      title: 'Nessuna richiesta AI nel periodo',
      text: 'Non ci sono richieste AI da contabilizzare nel periodo selezionato.',
    }
  }

  if (usage.actualCostRequestCount === 0 || usage.actualCostUsd === null) {
    return {
      color: 'warning',
      icon: 'mdi-cash-remove',
      title: 'Costo non disponibile',
      text: 'Nessun provider ha restituito un costo per richiesta. I token restano disponibili per l’analisi di utilizzo.',
    }
  }

  if (usage.actualCostRequestCount < usage.requestCount) {
    return {
      color: 'warning',
      icon: 'mdi-cash-clock',
      title: 'Costo parziale',
      text: `Il provider ha dichiarato il costo per ${formatInteger(usage.actualCostRequestCount)} di ${formatInteger(usage.requestCount)} richieste. Il totale mostrato non copre le altre richieste.`,
    }
  }

  return {
    color: 'success',
    icon: 'mdi-cash-check',
    title: 'Costo dichiarato dal provider',
    text: 'Il provider ha restituito un costo per tutte le richieste AI del periodo.',
  }
})

const summaryItems = computed(() => [
  {
    label: 'Richieste',
    value: formatInteger(props.aiUsage.requestCount),
    detail: `${formatInteger(props.aiUsage.successfulRequestCount)} riuscite, ${formatInteger(props.aiUsage.failedRequestCount)} non riuscite`,
  },
  {
    label: 'Token input',
    value: formatInteger(props.aiUsage.inputTokens),
    detail: `Cache: ${formatInteger(props.aiUsage.cachedInputTokens)}`,
  },
  {
    label: 'Token output',
    value: formatInteger(props.aiUsage.outputTokens),
    detail: `Reasoning: ${formatInteger(props.aiUsage.reasoningTokens)} · Thinking: ${formatInteger(props.aiUsage.thinkingTokens)}`,
  },
  {
    label: 'Token totali',
    value: formatInteger(props.aiUsage.totalTokens),
    detail: 'Input e output riportati dal provider',
  },
  {
    label: 'Costo dichiarato',
    value: formatUsd(props.aiUsage.actualCostUsd),
    detail: `${formatInteger(props.aiUsage.actualCostRequestCount)} richieste con costo`,
  },
])
</script>

<template>
  <section class="ai-usage-section mt-4" aria-labelledby="ai-usage-title">
    <v-card class="report-card" rounded="xl" variant="outlined">
      <v-card-text>
        <div class="ai-usage-header">
          <div>
            <p class="ai-usage-eyebrow">Telemetria AI</p>
            <h2 id="ai-usage-title" class="ai-usage-title">Utilizzo di token e costi</h2>
            <p class="ai-usage-description">
              Dati raccolti per le analisi degli snapshot. Il costo è mostrato solo quando il provider lo comunica.
            </p>
          </div>
          <v-icon color="primary" icon="mdi-chart-donut-variant" size="28" aria-hidden="true" />
        </div>

        <v-row class="mt-1" dense>
          <v-col
            v-for="item in summaryItems"
            :key="item.label"
            cols="12"
            sm="6"
            md="4"
            lg="auto"
          >
            <div class="ai-usage-metric" :aria-label="`${item.label}: ${item.value}. ${item.detail}`">
              <div class="ai-usage-metric__label">{{ item.label }}</div>
              <div class="ai-usage-metric__value">{{ item.value }}</div>
              <div class="ai-usage-metric__detail">{{ item.detail }}</div>
            </div>
          </v-col>
        </v-row>

        <v-alert
          class="mt-4"
          :color="costNotice.color"
          :icon="costNotice.icon"
          :title="costNotice.title"
          :text="costNotice.text"
          density="compact"
          variant="tonal"
          role="status"
        />

        <v-row class="mt-1" dense>
          <v-col cols="12" lg="6">
            <section class="ai-usage-table" aria-labelledby="ai-usage-provider-title">
              <h3 id="ai-usage-provider-title" class="ai-usage-table__title">Per provider</h3>
              <v-table density="compact" class="ai-usage-table__table">
                <caption class="visually-hidden">Utilizzo AI suddiviso per provider</caption>
                <thead>
                  <tr>
                    <th scope="col">Provider</th>
                    <th scope="col" class="text-right">Richieste</th>
                    <th scope="col" class="text-right">Input</th>
                    <th scope="col" class="text-right">Output</th>
                    <th scope="col" class="text-right">Totale</th>
                    <th scope="col" class="text-right">Costo</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="slice in aiUsage.byProvider" :key="slice.label">
                    <th scope="row">{{ slice.label }}</th>
                    <td class="text-right">{{ formatInteger(slice.requestCount) }}</td>
                    <td class="text-right">{{ formatInteger(slice.inputTokens) }}</td>
                    <td class="text-right">{{ formatInteger(slice.outputTokens) }}</td>
                    <td class="text-right">{{ formatInteger(slice.totalTokens) }}</td>
                    <td class="text-right">{{ formatUsd(slice.actualCostUsd) }}</td>
                  </tr>
                  <tr v-if="aiUsage.byProvider.length === 0">
                    <td colspan="6" class="text-medium-emphasis">Nessun dettaglio per provider disponibile.</td>
                  </tr>
                </tbody>
              </v-table>
            </section>
          </v-col>

          <v-col cols="12" lg="6">
            <section class="ai-usage-table" aria-labelledby="ai-usage-origin-title">
              <h3 id="ai-usage-origin-title" class="ai-usage-table__title">Per provenienza</h3>
              <v-table density="compact" class="ai-usage-table__table">
                <caption class="visually-hidden">Utilizzo AI suddiviso per provenienza della richiesta</caption>
                <thead>
                  <tr>
                    <th scope="col">Provenienza</th>
                    <th scope="col" class="text-right">Richieste</th>
                    <th scope="col" class="text-right">Input</th>
                    <th scope="col" class="text-right">Output</th>
                    <th scope="col" class="text-right">Totale</th>
                    <th scope="col" class="text-right">Costo</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="slice in aiUsage.byOrigin" :key="slice.label">
                    <th scope="row">{{ slice.label }}</th>
                    <td class="text-right">{{ formatInteger(slice.requestCount) }}</td>
                    <td class="text-right">{{ formatInteger(slice.inputTokens) }}</td>
                    <td class="text-right">{{ formatInteger(slice.outputTokens) }}</td>
                    <td class="text-right">{{ formatInteger(slice.totalTokens) }}</td>
                    <td class="text-right">{{ formatUsd(slice.actualCostUsd) }}</td>
                  </tr>
                  <tr v-if="aiUsage.byOrigin.length === 0">
                    <td colspan="6" class="text-medium-emphasis">Nessun dettaglio sulla provenienza disponibile.</td>
                  </tr>
                </tbody>
              </v-table>
            </section>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>
  </section>
</template>

<style scoped>
.ai-usage-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.ai-usage-eyebrow {
  margin: 0 0 4px;
  color: rgb(var(--v-theme-primary));
  font-size: 0.74rem;
  font-weight: 700;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.ai-usage-title {
  margin: 0;
  color: rgb(var(--v-theme-on-surface));
  font-size: 1.1rem;
  font-weight: 700;
}

.ai-usage-description {
  max-width: 52rem;
  margin: 5px 0 0;
  color: rgb(var(--v-theme-on-surface-variant));
  font-size: 0.84rem;
}

.ai-usage-metric {
  display: grid;
  grid-template-rows: auto auto auto;
  gap: 3px;
  min-height: 92px;
  padding: 12px;
  border: 1px solid rgb(var(--v-theme-outline-variant));
  border-radius: 10px;
}

.ai-usage-metric__label {
  color: rgb(var(--v-theme-on-surface-variant));
  font-size: 0.7rem;
  font-weight: 650;
  letter-spacing: 0.035em;
  text-transform: uppercase;
}

.ai-usage-metric__value {
  color: rgb(var(--v-theme-on-surface));
  font-size: 1.05rem;
  font-weight: 680;
  line-height: 1.15;
}

.ai-usage-metric__detail {
  color: rgb(var(--v-theme-on-surface-variant));
  font-size: 0.72rem;
}

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
