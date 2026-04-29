<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { getOverheadStatus } from '@/lib/companyOverhead'
import CurrencyAmount from '@/components/numbers/CurrencyAmount.vue'
import type { CompanySettings } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const companyId = computed(() => route.params.companyId as string)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const settings = ref<CompanySettings | null>(null)
const companyName = ref('')
const dividendPayoutPercent = ref(20)
const salaryMultipliers = ref<Record<string, number>>({})

const SETTINGS_QUERY = `
  query GetCompanySettings($companyId: UUID!) {
    companySettings(companyId: $companyId) {
      companyId
      companyName
      cash
      totalSharesIssued
      dividendPayoutRatio
      foundedAtTick
      administrationOverheadRate
      ageFactor
      assetFactor
      assetValue
      currencyCode
      citySalarySettings {
        cityId
        cityName
        currencyCode
        baseSalaryPerManhour
        salaryMultiplier
        effectiveSalaryPerManhour
      }
    }
  }
`

const UPDATE_MUTATION = `
  mutation UpdateCompanySettings($input: UpdateCompanySettingsInput!) {
    updateCompanySettings(input: $input) {
      id
      name
      dividendPayoutRatio
    }
  }
`

async function loadSettings() {
  loading.value = true
  error.value = null
  success.value = null

  try {
    const data = await gqlRequest<{ companySettings: CompanySettings | null }>(SETTINGS_QUERY, {
      companyId: companyId.value,
    })

    if (!data.companySettings) {
      error.value = t('companySettings.notFound')
      return
    }

    settings.value = data.companySettings
    companyName.value = data.companySettings.companyName
    dividendPayoutPercent.value = data.companySettings.dividendPayoutRatio * 100
    salaryMultipliers.value = Object.fromEntries(data.companySettings.citySalarySettings.map((entry) => [entry.cityId, entry.salaryMultiplier]))
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('companySettings.loadFailed')
  } finally {
    loading.value = false
  }
}

async function saveSettings() {
  if (!settings.value) {
    return
  }

  saving.value = true
  error.value = null
  success.value = null

  try {
    await gqlRequest(UPDATE_MUTATION, {
      input: {
        companyId: settings.value.companyId,
        name: companyName.value,
        dividendPayoutRatio: Number((dividendPayoutPercent.value / 100).toFixed(4)),
        citySalarySettings: settings.value.citySalarySettings.map((entry) => ({
          cityId: entry.cityId,
          salaryMultiplier: Number(salaryMultipliers.value[entry.cityId] ?? entry.salaryMultiplier),
        })),
      },
    })

    await loadSettings()
    success.value = t('companySettings.saved')
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('companySettings.saveFailed')
  } finally {
    saving.value = false
  }
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

function formatShareCount(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

const overheadStatus = computed(() =>
  settings.value ? getOverheadStatus(settings.value.administrationOverheadRate) : 'low',
)

onMounted(loadSettings)
</script>

<template>
  <div class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
    <div class="flex flex-col gap-6">
      <!-- Header -->
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:gap-4">
        <button class="btn btn-ghost self-start" @click="router.push('/dashboard')">
          ← {{ t('common.back') }}
        </button>
        <div>
          <p class="text-sm text-muted">{{ t('companySettings.eyebrow') }}</p>
          <h1>{{ settings?.companyName ?? t('companySettings.title') }}</h1>
        </div>
      </div>

      <!-- Loading state -->
      <div v-if="loading" class="rounded-2xl border border-divider bg-card p-6 shadow-sm text-center">
        <p class="text-muted">{{ t('common.loading') }}</p>
      </div>

      <!-- Load-error state -->
      <div
        v-else-if="error"
        class="rounded-2xl border border-divider bg-card p-6 shadow-sm flex flex-col gap-4"
        role="alert"
      >
        <p class="text-red-600">{{ error }}</p>
        <div>
          <button class="btn btn-secondary" @click="loadSettings">{{ t('common.tryAgain') }}</button>
        </div>
      </div>

      <!-- Main content -->
      <div v-else-if="settings" class="flex flex-col gap-6">
        <!-- Overview card -->
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-4">{{ t('companySettings.overviewTitle') }}</h2>

          <div class="grid grid-cols-2 gap-4 my-4 sm:grid-cols-3">
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.assetValue') }}</span>
              <strong><CurrencyAmount :amount="settings.assetValue" :currency="settings.currencyCode" /></strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.cash') }}</span>
              <strong><CurrencyAmount :amount="settings.cash" :currency="settings.currencyCode" /></strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.foundedTick') }}</span>
              <strong>{{ settings.foundedAtTick }}</strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.totalSharesIssued') }}</span>
              <strong>{{ formatShareCount(settings.totalSharesIssued) }}</strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.dividendPayoutRatio') }}</span>
              <strong>{{ formatPercent(settings.dividendPayoutRatio) }}</strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.administrationOverhead') }}</span>
              <strong
                :class="[
                  'overhead-value',
                  `overhead-${overheadStatus}`,
                  'flex items-center gap-1.5 flex-wrap',
                ]"
              >
                {{ formatPercent(settings.administrationOverheadRate) }}
                <span
                  class="overhead-badge text-[0.7rem] font-semibold px-[0.45rem] py-[0.1rem] rounded-full uppercase tracking-[0.04em]"
                  :class="{
                    'bg-green-500/15 text-green-700': overheadStatus === 'low',
                    'bg-amber-500/15 text-amber-700': overheadStatus === 'medium',
                    'bg-red-500/15 text-red-600': overheadStatus === 'high',
                  }"
                >
                  {{ t(`companySettings.overheadStatus.${overheadStatus}`) }}
                </span>
              </strong>
            </div>
          </div>

          <!-- Driver chips -->
          <div class="flex flex-wrap gap-2 mb-3">
            <span
              class="driver-chip text-[0.78rem] px-3 py-0.5 rounded-full border border-divider bg-card text-muted"
            >
              {{ t('companySettings.overheadDriverAge') }}: {{ formatPercent(settings.ageFactor) }}
            </span>
            <span
              class="driver-chip text-[0.78rem] px-3 py-0.5 rounded-full border border-divider bg-card text-muted"
            >
              {{ t('companySettings.overheadDriverScale') }}: {{ formatPercent(settings.assetFactor) }}
            </span>
          </div>

          <p class="text-sm text-muted">{{ t('companySettings.overheadHelp') }}</p>
          <p class="text-[0.82rem] text-muted mt-1">{{ t('companySettings.overheadReduceTip') }}</p>
        </section>

        <!-- Form card -->
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-5">{{ t('companySettings.profileTitle') }}</h2>

          <label class="settings-field grid gap-1.5 mb-4">
            <span class="text-sm text-muted">{{ t('companySettings.companyName') }}</span>
            <input
              v-model="companyName"
              type="text"
              maxlength="200"
              class="w-full rounded-[10px] border border-divider bg-card px-3 py-2.5 text-body"
            />
          </label>

          <label class="settings-field grid gap-1.5 mb-4">
            <span class="text-sm text-muted">{{ t('companySettings.dividendPayoutRatio') }}</span>
            <input
              v-model.number="dividendPayoutPercent"
              type="number"
              min="0"
              max="100"
              step="1"
              class="w-full rounded-[10px] border border-divider bg-card px-3 py-2.5 text-body"
            />
            <small class="text-muted text-[0.82rem]">{{ t('companySettings.dividendHelp') }}</small>
          </label>

          <!-- Salary table -->
          <div class="overflow-x-auto">
            <table class="salary-table w-full border-collapse">
              <thead>
                <tr>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">
                    {{ t('companySettings.city') }}
                  </th>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">
                    {{ t('companySettings.baseSalary') }}
                  </th>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">
                    {{ t('companySettings.salaryMultiplier') }}
                  </th>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">
                    {{ t('companySettings.effectiveSalary') }}
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="entry in settings.citySalarySettings" :key="entry.cityId">
                  <td class="px-3 py-3 border-b border-divider">
                    {{ entry.cityName }}
                    <span
                      class="city-currency-badge inline-block text-[0.7rem] font-semibold px-1.5 py-[0.1rem] rounded-full border border-divider bg-card text-muted ml-1.5 align-middle tracking-[0.03em]"
                    >{{ entry.currencyCode }}</span>
                  </td>
                  <td class="px-3 py-3 border-b border-divider">
                    <CurrencyAmount :amount="entry.baseSalaryPerManhour" :currency="entry.currencyCode" />
                  </td>
                  <td class="px-3 py-3 border-b border-divider">
                    <input
                      v-model.number="salaryMultipliers[entry.cityId]"
                      type="number"
                      min="0.5"
                      max="2"
                      step="0.05"
                      class="salary-input w-full max-w-[7rem] rounded-[10px] border border-divider bg-card px-3 py-2 text-body"
                      :aria-label="`${t('companySettings.salaryMultiplier')} ${entry.cityName}`"
                    />
                  </td>
                  <td class="px-3 py-3 border-b border-divider">
                    <CurrencyAmount
                      :amount="entry.baseSalaryPerManhour * (salaryMultipliers[entry.cityId] ?? entry.salaryMultiplier)"
                      :currency="entry.currencyCode"
                    />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <p class="salary-impact-hint text-[0.82rem] text-muted mt-3 mb-1">{{ t('companySettings.salaryImpactHint') }}</p>
          <p class="salary-local-currency-note text-[0.82rem] text-muted mb-1">{{ t('companySettings.salaryLocalCurrencyNote') }}</p>

          <p v-if="success" class="text-good mt-3" role="status">{{ success }}</p>
          <p v-if="error" class="text-red-600 mt-3" role="alert">{{ error }}</p>

          <div class="flex justify-end mt-4">
            <button class="btn btn-primary" :disabled="saving" @click="saveSettings">
              {{ saving ? t('common.loading') : t('common.save') }}
            </button>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>
