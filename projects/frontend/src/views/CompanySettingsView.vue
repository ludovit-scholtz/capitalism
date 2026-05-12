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
const activeTab = ref<'general' | 'salaries' | 'administration' | 'dividends'>('general')
const dividendProposalPercent = ref(20)
const dividendBusy = ref(false)

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
      pendingDividendProposal {
        id
        dividendPercent
        votingCloseTick
        ticksRemaining
        forVotes
        againstVotes
        myVoteChoice
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

const PROPOSE_DIVIDEND_MUTATION = `
  mutation ProposeDividend($input: ProposeDividendInput!) {
    proposeDividend(input: $input) {
      id
      status
      ticksRemaining
    }
  }
`

const VOTE_DIVIDEND_MUTATION = `
  mutation VoteDividend($input: VoteDividendInput!) {
    voteDividend(input: $input) {
      id
      status
      forVotes
      againstVotes
      myVoteChoice
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
    dividendProposalPercent.value = data.companySettings.dividendPayoutRatio * 100
    salaryMultipliers.value = Object.fromEntries(data.companySettings.citySalarySettings.map((entry) => [entry.cityId, entry.salaryMultiplier]))
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('companySettings.loadFailed')
  } finally {
    loading.value = false
  }
}

async function proposeDividendPolicy() {
  if (!settings.value || dividendBusy.value) {
    return
  }

  dividendBusy.value = true
  error.value = null
  success.value = null
  try {
    await gqlRequest(PROPOSE_DIVIDEND_MUTATION, {
      input: {
        companyId: settings.value.companyId,
        dividendPercent: Number(dividendProposalPercent.value),
      },
    })
    await loadSettings()
    success.value = t('companySettings.dividendProposalSubmitted')
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('companySettings.saveFailed')
  } finally {
    dividendBusy.value = false
  }
}

async function voteDividend(vote: 'APPROVE' | 'REJECT') {
  if (!settings.value || dividendBusy.value) {
    return
  }

  dividendBusy.value = true
  error.value = null
  success.value = null
  try {
    await gqlRequest(VOTE_DIVIDEND_MUTATION, {
      input: {
        companyId: settings.value.companyId,
        vote,
      },
    })
    await loadSettings()
    success.value = t('companySettings.dividendVoteSubmitted')
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('companySettings.saveFailed')
  } finally {
    dividendBusy.value = false
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
const pendingDividendTotalVotes = computed(() => {
  const proposal = settings.value?.pendingDividendProposal
  if (!proposal) {
    return 0
  }

  return proposal.forVotes + proposal.againstVotes
})
const pendingDividendApprovePct = computed(() => {
  const proposal = settings.value?.pendingDividendProposal
  if (!proposal) {
    return 0
  }
  const total = pendingDividendTotalVotes.value
  if (total <= 0) {
    return 0
  }

  return Math.round((proposal.forVotes / total) * 100)
})
const pendingDividendRejectPct = computed(() => {
  const proposal = settings.value?.pendingDividendProposal
  if (!proposal) {
    return 0
  }
  const total = pendingDividendTotalVotes.value
  if (total <= 0) {
    return 0
  }

  return Math.max(0, 100 - pendingDividendApprovePct.value)
})

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
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-4">{{ t('companySettings.overviewTitle') }}</h2>
          <div class="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.assetValue') }}</span>
              <strong><CurrencyAmount :amount="settings.assetValue" :currency="settings.currencyCode" /></strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.cash') }}</span>
              <strong><CurrencyAmount :amount="settings.cash" :currency="settings.currencyCode" /></strong>
            </div>
            <div>
              <span class="block text-xs text-muted mb-1">{{ t('companySettings.totalSharesIssued') }}</span>
              <strong>{{ formatShareCount(settings.totalSharesIssued) }}</strong>
            </div>
          </div>
        </section>

        <section class="rounded-2xl border border-divider bg-card p-4 shadow-sm">
          <div class="flex flex-wrap gap-2">
            <button class="btn btn-ghost" :class="{ 'btn-primary': activeTab === 'general' }" @click="activeTab = 'general'">
              {{ t('companySettings.tabs.general') }}
            </button>
            <button class="btn btn-ghost" :class="{ 'btn-primary': activeTab === 'salaries' }" @click="activeTab = 'salaries'">
              {{ t('companySettings.tabs.salaries') }}
            </button>
            <button class="btn btn-ghost" :class="{ 'btn-primary': activeTab === 'administration' }" @click="activeTab = 'administration'">
              {{ t('companySettings.tabs.administration') }}
            </button>
            <button class="btn btn-ghost" :class="{ 'btn-primary': activeTab === 'dividends' }" @click="activeTab = 'dividends'">
              {{ t('companySettings.tabs.dividends') }}
            </button>
          </div>
        </section>

        <section v-show="activeTab === 'general'" class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
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

          <div class="flex justify-end mt-4">
            <button class="btn btn-primary" :disabled="saving" @click="saveSettings">
              {{ saving ? t('common.loading') : t('common.save') }}
            </button>
          </div>
        </section>

        <section v-show="activeTab === 'salaries'" class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-5">{{ t('companySettings.tabs.salaries') }}</h2>
          <div class="overflow-x-auto">
            <table class="salary-table w-full border-collapse">
              <thead>
                <tr>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">{{ t('companySettings.city') }}</th>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">{{ t('companySettings.baseSalary') }}</th>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">{{ t('companySettings.salaryMultiplier') }}</th>
                  <th class="text-left px-3 py-3 border-b border-divider text-sm text-muted font-medium">{{ t('companySettings.effectiveSalary') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="entry in settings.citySalarySettings" :key="entry.cityId">
                  <td class="px-3 py-3 border-b border-divider">
                    {{ entry.cityName }}
                    <span class="city-currency-badge inline-block text-[0.7rem] font-semibold px-1.5 py-[0.1rem] rounded-full border border-divider bg-card text-muted ml-1.5 align-middle tracking-[0.03em]">{{ entry.currencyCode }}</span>
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
                    <CurrencyAmount :amount="entry.baseSalaryPerManhour * (salaryMultipliers[entry.cityId] ?? entry.salaryMultiplier)" :currency="entry.currencyCode" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p class="salary-impact-hint text-[0.82rem] text-muted mt-3 mb-1">{{ t('companySettings.salaryImpactHint') }}</p>
          <p class="salary-local-currency-note text-[0.82rem] text-muted mb-1">{{ t('companySettings.salaryLocalCurrencyNote') }}</p>
          <div class="flex justify-end mt-4">
            <button class="btn btn-primary" :disabled="saving" @click="saveSettings">
              {{ saving ? t('common.loading') : t('common.save') }}
            </button>
          </div>
        </section>

        <section v-show="activeTab === 'administration'" class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-4">{{ t('companySettings.tabs.administration') }}</h2>
          <p class="text-sm text-muted mb-3">{{ t('companySettings.overheadHelp') }}</p>
          <strong :class="['overhead-value', `overhead-${overheadStatus}`, 'flex items-center gap-1.5 flex-wrap']">
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
          <div class="flex flex-wrap gap-2 mt-3 mb-2">
            <span class="driver-chip text-[0.78rem] px-3 py-0.5 rounded-full border border-divider bg-card text-muted">
              {{ t('companySettings.overheadDriverAge') }}: {{ formatPercent(settings.ageFactor) }}
            </span>
            <span class="driver-chip text-[0.78rem] px-3 py-0.5 rounded-full border border-divider bg-card text-muted">
              {{ t('companySettings.overheadDriverScale') }}: {{ formatPercent(settings.assetFactor) }}
            </span>
          </div>
          <p class="overhead-tip text-[0.82rem] text-muted mt-1">{{ t('companySettings.overheadReduceTip') }}</p>
        </section>

        <section v-show="activeTab === 'dividends'" class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-4">{{ t('companySettings.tabs.dividends') }}</h2>
          <p class="text-sm text-muted mb-3">
            {{ t('companySettings.currentDividendRate') }}:
            <strong>{{ formatPercent(settings.dividendPayoutRatio) }}</strong>
          </p>
          <label class="settings-field grid gap-1.5 mb-3">
            <span class="text-sm text-muted">{{ t('companySettings.proposeDividendLabel') }}</span>
            <input
              v-model.number="dividendProposalPercent"
              type="number"
              min="0"
              max="100"
              step="1"
              class="w-full rounded-[10px] border border-divider bg-card px-3 py-2.5 text-body"
            />
          </label>
          <div class="mb-4">
            <button class="btn btn-secondary" :disabled="dividendBusy || !!settings.pendingDividendProposal" @click="proposeDividendPolicy">
              {{ t('companySettings.proposeDividendAction') }}
            </button>
          </div>

          <div v-if="settings.pendingDividendProposal" class="rounded-xl border border-divider p-4 bg-card/40">
            <p class="text-sm mb-2">
              {{ t('companySettings.pendingProposal') }}:
              <strong>{{ settings.pendingDividendProposal.dividendPercent.toFixed(1) }}%</strong>
            </p>
            <p class="text-xs text-muted mb-2">
              {{ t('companySettings.votingClosesAtTick', { tick: settings.pendingDividendProposal.votingCloseTick }) }}
            </p>
            <div class="w-full h-2 rounded bg-divider overflow-hidden mb-2">
              <div class="h-full bg-green-600" :style="{ width: `${pendingDividendApprovePct}%` }" />
            </div>
            <p class="text-xs text-muted mb-3">
              {{ t('companySettings.voteSummary', { approve: pendingDividendApprovePct, reject: pendingDividendRejectPct }) }}
            </p>
            <div class="flex flex-wrap gap-2">
              <button class="btn btn-secondary" :disabled="dividendBusy" @click="voteDividend('APPROVE')">
                {{ t('companySettings.voteApprove') }}
              </button>
              <button class="btn btn-secondary" :disabled="dividendBusy" @click="voteDividend('REJECT')">
                {{ t('companySettings.voteReject') }}
              </button>
            </div>
          </div>
        </section>

        <p v-if="success" class="text-good mt-1" role="status">{{ success }}</p>
        <p v-if="error" class="text-red-600 mt-1" role="alert">{{ error }}</p>
      </div>
    </div>
  </div>
</template>
