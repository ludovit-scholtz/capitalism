<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { City, Company, GovernmentContractCard, GovernmentContractDetailResult } from '@/types'

const props = defineProps<{
  city: City
  cityId: string
  companies: Company[]
}>()

const { t, locale } = useI18n()
const auth = useAuthStore()

const loading = ref(false)
const bidding = ref(false)
const error = ref<string | null>(null)
const currentTick = ref(0)
const contracts = ref<GovernmentContractCard[]>([])
const selectedContract = ref<GovernmentContractCard | null>(null)
const selectedCompanyId = ref<string>('')
const bidPricePerUnit = ref<number>(0)
const estimatedDeliveryTick = ref<number>(0)
const eligibility = ref<GovernmentContractDetailResult['eligibility']>(null)

const eligibleCompanies = computed(() =>
  props.companies.filter((company) => company.buildings.some((building) => building.cityId === props.cityId)),
)

const canOpenBid = computed(() => auth.isAuthenticated && eligibleCompanies.value.length > 0)

async function loadContracts() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{
      gameState: { currentTick: number }
      cityGovernmentContracts: GovernmentContractCard[]
    }>(
      `query CityGovernmentContracts($cityId: UUID!) {
        gameState { currentTick }
        cityGovernmentContracts(cityId: $cityId, status: "OPEN") {
          id cityId cityName currencyCode title description productTypeId productName quantityRequired minimumQuality budgetCap
          deadlineTick status winnerCompanyId winnerCompanyName createdAtTick bidCount awardedBidPricePerUnit
          fulfilledQuantity fulfillmentPercent
        }
      }`,
      { cityId: props.cityId },
    )

    currentTick.value = data.gameState?.currentTick ?? 0
    contracts.value = data.cityGovernmentContracts ?? []
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

function openBidModal(contract: GovernmentContractCard) {
  selectedContract.value = contract
  selectedCompanyId.value = eligibleCompanies.value[0]?.id ?? ''
  bidPricePerUnit.value = Number(contract.budgetCap.toFixed(2))
  estimatedDeliveryTick.value = Math.max(currentTick.value + 1, currentTick.value + 24)
  void loadEligibility()
}

function closeBidModal() {
  selectedContract.value = null
  eligibility.value = null
}

async function loadEligibility() {
  if (!selectedContract.value || !selectedCompanyId.value) {
    eligibility.value = null
    return
  }

  try {
    const data = await gqlRequest<{ contractDetail: GovernmentContractDetailResult | null }>(
      `query ContractDetail($contractId: UUID!, $companyId: UUID) {
        contractDetail(contractId: $contractId, companyId: $companyId) {
          competingBidCount
          eligibility {
            isEligible
            reasonCode
            reasonMessage
            currentQualityLevel
          }
          contract {
            id
          }
        }
      }`,
      { contractId: selectedContract.value.id, companyId: selectedCompanyId.value },
    )

    eligibility.value = data.contractDetail?.eligibility ?? null
  } catch {
    eligibility.value = null
  }
}

async function submitBid() {
  if (!selectedContract.value || !selectedCompanyId.value) {
    return
  }

  bidding.value = true
  error.value = null
  try {
    await gqlRequest(
      `mutation SubmitContractBid($input: SubmitContractBidInput!) {
        submitContractBid(input: $input) {
          id
        }
      }`,
      {
        input: {
          contractId: selectedContract.value.id,
          companyId: selectedCompanyId.value,
          bidPricePerUnit: Number(bidPricePerUnit.value),
          estimatedDeliveryTick: Number(estimatedDeliveryTick.value),
        },
      },
    )
    closeBidModal()
    await loadContracts()
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    bidding.value = false
  }
}

function ticksRemaining(deadlineTick: number): number {
  return Math.max(0, deadlineTick - currentTick.value)
}

function deadlineClass(deadlineTick: number): string {
  const remaining = ticksRemaining(deadlineTick)
  if (remaining <= 12) return 'deadline-critical'
  if (remaining <= 48) return 'deadline-warning'
  return 'deadline-safe'
}

watch(
  () => props.cityId,
  () => {
    closeBidModal()
    void loadContracts()
  },
)

watch(selectedCompanyId, () => {
  void loadEligibility()
})

onMounted(() => {
  void loadContracts()
})
</script>

<template>
  <section class="contracts-tab">
    <header class="contracts-header">
      <h2>{{ t('cityMap.tabs.contractsHeading') }}</h2>
      <p>{{ t('cityMap.tabs.contractsDescription') }}</p>
    </header>

    <div v-if="loading" class="contracts-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="contracts-error">{{ error }}</div>
    <div v-else-if="contracts.length === 0" class="contracts-empty">{{ t('cityContracts.empty') }}</div>

    <div v-else class="contracts-grid">
      <article v-for="contract in contracts" :key="contract.id" class="contract-card">
        <header class="contract-card-header">
          <span class="contract-seal">🏛️</span>
          <div>
            <h3>{{ contract.title }}</h3>
            <p>{{ contract.productName }}</p>
          </div>
        </header>

        <p class="contract-description">{{ contract.description }}</p>

        <dl class="contract-metrics">
          <div>
            <dt>{{ t('cityContracts.quantity') }}</dt>
            <dd>{{ contract.quantityRequired.toLocaleString() }}</dd>
          </div>
          <div>
            <dt>{{ t('cityContracts.budgetCap') }}</dt>
            <dd>{{ formatMoney(contract.budgetCap, contract.currencyCode, locale) }}</dd>
          </div>
          <div>
            <dt>{{ t('cityContracts.minimumQuality') }}</dt>
            <dd>{{ contract.minimumQuality.toFixed(1) }}/10</dd>
          </div>
          <div>
            <dt>{{ t('cityContracts.bidCount') }}</dt>
            <dd>{{ contract.bidCount }}</dd>
          </div>
        </dl>

        <div class="deadline-chip" :class="deadlineClass(contract.deadlineTick)">
          {{ t('cityContracts.deadlineTicks', { ticks: ticksRemaining(contract.deadlineTick) }) }}
        </div>

        <button class="btn btn-primary place-bid-btn" :disabled="!canOpenBid" @click="openBidModal(contract)">
          {{ t('cityContracts.placeBid') }}
        </button>
      </article>
    </div>

    <div v-if="selectedContract" class="bid-modal-backdrop" @click.self="closeBidModal">
      <div class="bid-modal">
        <h3>{{ t('cityContracts.bidModalTitle') }}</h3>
        <p class="bid-modal-subtitle">{{ selectedContract.title }}</p>

        <label class="field-label" for="contract-company">{{ t('cityContracts.company') }}</label>
        <select id="contract-company" v-model="selectedCompanyId" class="field-input">
          <option v-for="company in eligibleCompanies" :key="company.id" :value="company.id">{{ company.name }}</option>
        </select>

        <div class="eligibility-chip" :class="eligibility?.isEligible ? 'eligible' : 'ineligible'">
          <span v-if="eligibility?.isEligible">✅ {{ t('cityContracts.qualityEligible', { quality: eligibility.currentQualityLevel.toFixed(1) }) }}</span>
          <span v-else>
            ❌
            {{ eligibility?.reasonMessage ?? t('cityContracts.qualityUnknown') }}
          </span>
        </div>

        <label class="field-label" for="contract-bid-price">{{ t('cityContracts.bidPricePerUnit') }}</label>
        <input id="contract-bid-price" v-model.number="bidPricePerUnit" type="number" min="0.01" step="0.01" class="field-input" />

        <label class="field-label" for="contract-delivery-tick">{{ t('cityContracts.estimatedDeliveryTick') }}</label>
        <input
          id="contract-delivery-tick"
          v-model.number="estimatedDeliveryTick"
          type="number"
          :min="currentTick + 1"
          :max="selectedContract.deadlineTick + 240"
          class="field-input"
        />

        <div class="modal-actions">
          <button class="btn btn-secondary" @click="closeBidModal">{{ t('common.cancel') }}</button>
          <button class="btn btn-primary" :disabled="bidding || !eligibility?.isEligible" @click="submitBid">
            {{ bidding ? t('cityContracts.submittingBid') : t('cityContracts.submitBid') }}
          </button>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.contracts-tab {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.contracts-header h2 {
  margin: 0;
}

.contracts-header p {
  margin: 0.25rem 0 0;
  color: var(--color-text-secondary);
}

.contracts-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 0.9rem;
}

.contract-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: linear-gradient(160deg, rgba(10, 24, 44, 0.96), rgba(20, 32, 56, 0.9));
  color: #e2e8f0;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
}

.contract-card-header {
  display: flex;
  gap: 0.6rem;
  align-items: flex-start;
}

.contract-card-header h3 {
  margin: 0;
  font-size: 1rem;
}

.contract-card-header p {
  margin: 0.15rem 0 0;
  color: #93c5fd;
  font-size: 0.84rem;
}

.contract-seal {
  font-size: 1.25rem;
}

.contract-description {
  margin: 0;
  color: #cbd5e1;
  font-size: 0.86rem;
}

.contract-metrics {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.6rem;
  margin: 0;
}

.contract-metrics dt {
  font-size: 0.68rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #94a3b8;
}

.contract-metrics dd {
  margin: 0.1rem 0 0;
  font-weight: 600;
}

.deadline-chip {
  display: inline-flex;
  align-self: flex-start;
  border-radius: 999px;
  padding: 0.2rem 0.7rem;
  font-size: 0.76rem;
}

.deadline-safe {
  background: rgba(34, 197, 94, 0.18);
  color: #86efac;
}

.deadline-warning {
  background: rgba(245, 158, 11, 0.2);
  color: #fcd34d;
}

.deadline-critical {
  background: rgba(239, 68, 68, 0.2);
  color: #fca5a5;
}

.place-bid-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.contracts-loading,
.contracts-error,
.contracts-empty {
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  padding: 1rem;
}

.bid-modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.65);
  display: grid;
  place-items: center;
  z-index: 70;
  padding: 1rem;
}

.bid-modal {
  width: min(32rem, 100%);
  background: var(--color-card);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.1rem;
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
}

.bid-modal h3 {
  margin: 0;
}

.bid-modal-subtitle {
  margin: 0;
  color: var(--color-text-secondary);
}

.field-label {
  font-size: 0.82rem;
  font-weight: 600;
}

.field-input {
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  padding: 0.5rem 0.65rem;
}

.eligibility-chip {
  border-radius: var(--radius-md);
  padding: 0.45rem 0.6rem;
  font-size: 0.82rem;
}

.eligibility-chip.eligible {
  background: rgba(34, 197, 94, 0.12);
  color: #16a34a;
}

.eligibility-chip.ineligible {
  background: rgba(239, 68, 68, 0.12);
  color: #dc2626;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.6rem;
  margin-top: 0.2rem;
}
</style>
