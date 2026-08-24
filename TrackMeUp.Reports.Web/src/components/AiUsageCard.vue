<script setup lang="ts">
import { computed } from 'vue'
import { formatDateTime, formatInteger, type AiUsageOrigin, type AiUsageSummary } from '../reporting'
import { reportLocale, tr } from '../localization'
import { reportIcons } from '../icons'
import AiUsageBreakdownTable from './AiUsageBreakdownTable.vue'

const props = defineProps<{
  aiUsage: AiUsageSummary
}>()

const formatUsd = (value: number | null): string =>
  value === null ? tr('Unavailable') : new Intl.NumberFormat(reportLocale.value, {
    style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 6,
  }).format(value)

const costDetail = (count: number, timestamp: string | null): string =>
  timestamp === null
    ? tr('{count} estimated requests', { count: formatInteger(count) })
    : tr('{count} estimated requests · Prices: {timestamp}', {
        count: formatInteger(count),
        timestamp: formatDateTime(timestamp),
      })

const formatOriginLabel = (origin: AiUsageOrigin): string => {
  switch (origin) {
    case 'winui.operations': return tr('WinUI operations')
    case 'cli.ai': return tr('CLI AI')
    case 'snapshot.manual': return tr('Manual snapshot')
    case 'snapshot.scheduled': return tr('Scheduled snapshot')
    case 'snapshot.reprocess': return tr('Reprocessed snapshot')
    case 'runtime.ai': return tr('Runtime AI')
    case 'ocr.refinement': return tr('OCR refinement')
    case 'manual': return tr('Manual')
    case 'unknown': return tr('Unknown')
  }
}

const originRows = computed(() => props.aiUsage.byOrigin.map((slice) => ({
  ...slice,
  displayLabel: formatOriginLabel(slice.label),
})))

const costNotice = computed(() => {
  const usage = props.aiUsage
  if (usage.requestCount === 0) {
    return {
      color: 'primary',
      icon: reportIcons.information,
      title: tr('No AI requests in the period'),
      text: tr('There are no AI requests to account for in the selected period.'),
    }
  }

  if (usage.estimatedCostRequestCount > 0 && usage.actualCostRequestCount === 0) {
    return {
      color: usage.estimatedCostRequestCount === usage.requestCount ? 'success' : 'warning',
      icon: usage.estimatedCostRequestCount === usage.requestCount
        ? reportIcons.aiCostEstimated
        : reportIcons.aiCostPartial,
      title: tr('Estimated cost'),
      text: tr('No AI provider returned a per-request cost. The local pricing table estimates {estimated} of {total} requests.', {
        estimated: formatInteger(usage.estimatedCostRequestCount),
        total: formatInteger(usage.requestCount),
      }),
    }
  }

  if (usage.actualCostRequestCount === 0 || usage.actualCostUsd === null) {
    return {
      color: 'warning',
      icon: reportIcons.aiCostUnavailable,
      title: tr('Cost unavailable'),
      text: tr('No AI provider returned a per-request cost. Token counts remain available for usage analysis.'),
    }
  }

  if (usage.estimatedCostRequestCount > 0 && usage.actualCostRequestCount < usage.requestCount) {
    return {
      color: 'warning',
      icon: reportIcons.aiCostEstimated,
      title: tr('Reported and estimated cost'),
      text: tr('The AI provider reported {reported} costs, and the local pricing table estimated {estimated} requests.', {
        reported: formatInteger(usage.actualCostRequestCount),
        estimated: formatInteger(usage.estimatedCostRequestCount),
      }),
    }
  }

  if (usage.actualCostRequestCount < usage.requestCount) {
    return {
      color: 'warning',
      icon: reportIcons.aiCostPartial,
      title: tr('Partial cost'),
      text: tr('The AI provider reported a cost for {reported} of {total} requests. The displayed total does not cover the other requests.', {
        reported: formatInteger(usage.actualCostRequestCount),
        total: formatInteger(usage.requestCount),
      }),
    }
  }

  return {
    color: 'success',
    icon: reportIcons.aiCostAvailable,
    title: tr('AI-provider-reported cost'),
    text: tr('The AI provider returned a cost for every AI request in the period.'),
  }
})

const summaryItems = computed(() => [
  {
    label: tr('Requests'),
    value: formatInteger(props.aiUsage.requestCount),
    detail: tr('{succeeded} succeeded, {failed} failed', {
      succeeded: formatInteger(props.aiUsage.successfulRequestCount),
      failed: formatInteger(props.aiUsage.failedRequestCount),
    }),
  },
  {
    label: tr('Input tokens'),
    value: formatInteger(props.aiUsage.inputTokens),
    detail: tr('Cached input: {count}', { count: formatInteger(props.aiUsage.cachedInputTokens) }),
  },
  {
    label: tr('Output tokens'),
    value: formatInteger(props.aiUsage.outputTokens),
    detail: tr('Reasoning: {reasoning} · Thinking: {thinking}', {
      reasoning: formatInteger(props.aiUsage.reasoningTokens),
      thinking: formatInteger(props.aiUsage.thinkingTokens),
    }),
  },
  {
    label: tr('Total tokens'),
    value: formatInteger(props.aiUsage.totalTokens),
    detail: tr('Input and output reported by the AI provider'),
  },
  {
    label: tr('Reported cost'),
    value: formatUsd(props.aiUsage.actualCostUsd),
    detail: tr('{count} requests with cost', { count: formatInteger(props.aiUsage.actualCostRequestCount) }),
  },
  {
    label: tr('Estimated cost'),
    value: formatUsd(props.aiUsage.estimatedCostUsd),
    detail: costDetail(props.aiUsage.estimatedCostRequestCount, props.aiUsage.estimatedCostPricingUpdatedAt),
  },
])
</script>

<template>
  <section class="ai-usage-section mt-4" aria-labelledby="ai-usage-title">
    <v-card class="report-card" rounded="xl" variant="outlined">
      <v-card-text>
        <div class="ai-usage-header">
          <div>
            <p class="ai-usage-eyebrow">{{ tr('AI telemetry') }}</p>
            <h2 id="ai-usage-title" class="ai-usage-title">{{ tr('Token usage and costs') }}</h2>
            <p class="ai-usage-description">
              {{ tr('Data collected for snapshot analysis. Costs use AI-provider-reported values when available and local pricing estimates otherwise.') }}
            </p>
          </div>
          <v-icon color="primary" :icon="reportIcons.aiUsage" size="28" aria-hidden="true" />
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
            <AiUsageBreakdownTable
              title-id="ai-usage-provider-title"
              :title="tr('By AI provider')"
              :caption="tr('AI usage broken down by provider')"
              :first-column-label="tr('AI provider')"
              :rows="aiUsage.byProvider"
              :empty-text="tr('No AI-provider details available.')"
              :format-cost="formatUsd"
            />
          </v-col>

          <v-col cols="12" lg="6">
            <AiUsageBreakdownTable
              title-id="ai-usage-origin-title"
              :title="tr('By origin')"
              :caption="tr('AI usage broken down by request origin')"
              :first-column-label="tr('Origin')"
              :rows="originRows"
              :empty-text="tr('No origin details available.')"
              :format-cost="formatUsd"
            />
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

</style>
