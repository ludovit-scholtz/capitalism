<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { City } from '@/types'

interface AdditionalCompanyPrerequisites {
  allRequirementsMet: boolean
  personalBalanceUsd: number
}

const props = defineProps<{
  open: boolean
  cities: City[]
  prerequisites: AdditionalCompanyPrerequisites | null
}>()

const emit = defineEmits<{
  close: []
  launched: [companyId: string, companyName: string]
}>()

const { t, locale } = useI18n()

// ── Form state ────────────────────────────────────────────────────────────────
const step = ref<1 | 2>(1)
const companyName = ref('')
const selectedCityId = ref('')
const selectedIpoRaiseTarget = ref<number>(200_000)
const launching = ref(false)
const launchError = ref<string | null>(null)

// ── IPO tiers ─────────────────────────────────────────────────────────────────
const founderContribution = 200_000
const ipoTiers = [
  { raiseTarget: 200_000, founderPct: 50, labelKey: 'newCompanyIpoTier1Label', descKey: 'newCompanyIpoTier1Desc', isDefault: true },
  { raiseTarget: 400_000, founderPct: 33, labelKey: 'newCompanyIpoTier2Label', descKey: 'newCompanyIpoTier2Desc', isDefault: false },
  { raiseTarget: 600_000, founderPct: 25, labelKey: 'newCompanyIpoTier3Label', descKey: 'newCompanyIpoTier3Desc', isDefault: false },
]

// ── Computed ──────────────────────────────────────────────────────────────────
const nameError = computed(() => {
  const trimmed = companyName.value.trim()
  if (!trimmed) return null
  if (trimmed.length < 3) return t('dashboard.newCompanyNamePlaceholder')
  return null
})

const canProceedStep1 = computed(() => {
  const trimmed = companyName.value.trim()
  return trimmed.length >= 3 && selectedCityId.value !== ''
})

const balanceAfterIpo = computed(() => {
  return (props.prerequisites?.personalBalanceUsd ?? 0) - founderContribution
})

const lowBalanceWarning = computed(() => balanceAfterIpo.value < 50_000)

const totalFunding = computed(() => {
  const fxRate = 1 // All amounts shown in USD; backend converts to local currency.
  return (founderContribution + selectedIpoRaiseTarget.value) * fxRate
})

// ── Formatters ────────────────────────────────────────────────────────────────
function formatUsd(amount: number): string {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(amount)
}

// ── Actions ───────────────────────────────────────────────────────────────────
function goNext() {
  if (step.value === 1 && canProceedStep1.value) step.value = 2
}

function goBack() {
  if (step.value === 2) step.value = 1
  else emit('close')
}

function close() {
  step.value = 1
  companyName.value = ''
  selectedCityId.value = ''
  selectedIpoRaiseTarget.value = 200_000
  launchError.value = null
  emit('close')
}

async function launch() {
  if (!canProceedStep1.value) return
  launching.value = true
  launchError.value = null
  try {
    const result = await gqlRequest<{ startAdditionalCompany: { id: string; name: string } }>(
      `mutation StartAdditionalCompany($input: StartAdditionalCompanyInput!) {
        startAdditionalCompany(input: $input) {
          id
          name
        }
      }`,
      {
        input: {
          companyName: companyName.value.trim(),
          cityId: selectedCityId.value,
          ipoRaiseTarget: selectedIpoRaiseTarget.value,
        },
      },
    )
    const { id, name } = result.startAdditionalCompany
    emit('launched', id, name)
    close()
  } catch (e: unknown) {
    launchError.value = e instanceof Error ? e.message : t('dashboard.newCompanyError')
  } finally {
    launching.value = false
  }
}
</script>

<template>
  <teleport to="body">
    <div
      v-if="open"
      class="new-company-modal-overlay fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      @click.self="close"
    >
      <div
        class="new-company-modal bg-card border border-divider rounded-2xl shadow-2xl w-full max-w-lg mx-4 overflow-hidden"
        role="dialog"
        aria-modal="true"
        :aria-label="t('dashboard.newCompanyWizardTitle')"
      >
        <!-- Modal header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-divider">
          <h2 class="text-lg font-bold">{{ t('dashboard.newCompanyWizardTitle') }}</h2>
          <div class="flex items-center gap-3">
            <span class="text-xs text-muted">
              {{ t('common.step', { step, total: 2 }) }}
            </span>
            <button
              class="btn-icon text-muted hover:text-primary transition-colors"
              :aria-label="t('common.close')"
              @click="close"
            >
              ✕
            </button>
          </div>
        </div>

        <!-- Step 1: Company Profile -->
        <div v-if="step === 1" class="new-company-step1 px-6 py-5 flex flex-col gap-4">
          <h3 class="text-base font-semibold">{{ t('dashboard.newCompanyStep1Title') }}</h3>

          <!-- Company name -->
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium" for="nc-name">
              {{ t('dashboard.newCompanyNameLabel') }}
            </label>
            <input
              id="nc-name"
              v-model="companyName"
              class="input border border-divider rounded-lg px-3 py-2 bg-transparent text-primary w-full"
              type="text"
              maxlength="200"
              :placeholder="t('dashboard.newCompanyNamePlaceholder')"
            />
            <p v-if="nameError" class="text-bad text-xs">{{ nameError }}</p>
          </div>

          <!-- City selector -->
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium" for="nc-city">
              {{ t('dashboard.newCompanyCityLabel') }}
            </label>
            <select
              id="nc-city"
              v-model="selectedCityId"
              class="input border border-divider rounded-lg px-3 py-2 bg-card text-primary w-full"
            >
              <option value="" disabled>{{ t('dashboard.newCompanyCityPlaceholder') }}</option>
              <option v-for="city in cities" :key="city.id" :value="city.id">
                {{ city.name }} ({{ city.currencyCode }})
              </option>
            </select>
          </div>

          <div class="flex gap-3 mt-2 justify-end">
            <button class="btn btn-secondary" @click="goBack">{{ t('common.cancel') }}</button>
            <button
              class="btn btn-primary"
              :disabled="!canProceedStep1"
              @click="goNext"
            >
              {{ t('common.next') }}
            </button>
          </div>
        </div>

        <!-- Step 2: IPO Configuration -->
        <div v-else-if="step === 2" class="new-company-step2 px-6 py-5 flex flex-col gap-4">
          <h3 class="text-base font-semibold">{{ t('dashboard.newCompanyStep2Title') }}</h3>
          <p class="text-sm text-muted">{{ t('dashboard.newCompanyIpoTitle') }}</p>

          <!-- IPO tier cards -->
          <div class="nc-ipo-tiers flex flex-col gap-3">
            <button
              v-for="tier in ipoTiers"
              :key="tier.raiseTarget"
              class="nc-ipo-tier-card text-left border rounded-xl px-4 py-3 transition-colors cursor-pointer"
              :class="{
                'border-brand bg-brand/10': selectedIpoRaiseTarget === tier.raiseTarget,
                'border-divider hover:border-brand/50': selectedIpoRaiseTarget !== tier.raiseTarget,
              }"
              @click="selectedIpoRaiseTarget = tier.raiseTarget"
            >
              <div class="flex items-center gap-2">
                <strong class="text-sm">{{ t(`dashboard.${tier.labelKey}`) }}</strong>
                <span
                  v-if="tier.isDefault"
                  class="nc-ipo-default-badge text-xs text-brand border border-brand rounded px-1.5 py-0.5"
                >
                  {{ t('dashboard.newCompanyIpoDefaultBadge') }}
                </span>
              </div>
              <p class="text-xs text-muted mt-0.5">{{ t(`dashboard.${tier.descKey}`) }}</p>
            </button>
          </div>

          <!-- Balance info -->
          <div class="nc-balance-info bg-white/[0.04] border border-divider rounded-lg p-4 flex flex-col gap-2">
            <div class="flex justify-between text-sm">
              <span class="text-muted">{{ t('dashboard.newCompanyFounderContrib') }}</span>
              <span class="text-primary font-semibold">{{ formatUsd(founderContribution) }}</span>
            </div>
            <div class="flex justify-between text-sm">
              <span class="text-muted">{{ t('dashboard.newCompanyTotalFunding') }}</span>
              <span class="text-good font-bold">{{ formatUsd(totalFunding) }}</span>
            </div>
            <div class="flex justify-between text-sm border-t border-divider pt-2 mt-1">
              <span class="text-muted">{{ t('dashboard.newCompanyIpoCurrentBalance') }}</span>
              <span class="text-primary">{{ formatUsd(prerequisites?.personalBalanceUsd ?? 0) }}</span>
            </div>
            <div class="flex justify-between text-sm">
              <span class="text-muted">{{ t('dashboard.newCompanyIpoBalanceAfter') }}</span>
              <span :class="balanceAfterIpo < 0 ? 'text-bad' : 'text-primary'">
                {{ formatUsd(balanceAfterIpo) }}
              </span>
            </div>
          </div>

          <p v-if="lowBalanceWarning" class="text-sm text-warning border border-warning/30 bg-warning/10 rounded-lg px-3 py-2">
            {{ t('dashboard.newCompanyIpoLowBalanceWarning') }}
          </p>

          <p v-if="launchError" class="text-sm text-bad border border-bad/30 bg-bad/10 rounded-lg px-3 py-2 nc-launch-error">
            {{ launchError }}
          </p>

          <div class="flex gap-3 mt-2 justify-end">
            <button class="btn btn-secondary" :disabled="launching" @click="goBack">
              {{ t('common.back') }}
            </button>
            <button
              class="btn btn-primary"
              :disabled="launching"
              @click="launch"
            >
              {{ launching ? t('dashboard.newCompanyLaunching') : t('dashboard.newCompanyLaunchBtn') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </teleport>
</template>

<style scoped>
.btn-icon {
  background: none;
  border: none;
  padding: 0.25rem 0.5rem;
  cursor: pointer;
  font-size: 1rem;
  line-height: 1;
  border-radius: 0.375rem;
}
.btn-icon:hover {
  background: var(--color-bg-hover, rgba(255, 255, 255, 0.08));
}
</style>
