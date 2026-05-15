<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { ContractBidResult, ContractFulfillmentResult, GovernmentContractCard } from '@/types'

const route = useRoute()
const { t, locale } = useI18n()
const companyId = computed(() => route.params.companyId as string)

const loading = ref(false)
const submitting = ref<string | null>(null)
const error = ref<string | null>(null)
const contracts = ref<GovernmentContractCard[]>([])
const bids = ref<ContractBidResult[]>([])
const shipmentQuantities = ref<Record<string, number>>({})

const biddingContracts = computed(() => contracts.value.filter((contract) => contract.status === 'OPEN'))
const awardedContracts = computed(() => contracts.value.filter((contract) => contract.status === 'AWARDED' && (contract.fulfillmentPercent ?? 0) <= 0))
const inFulfillmentContracts = computed(() => contracts.value.filter((contract) => contract.status === 'AWARDED' && (contract.fulfillmentPercent ?? 0) > 0))
const completedContracts = computed(() => contracts.value.filter((contract) => contract.status === 'FULFILLED'))

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ companyContracts: GovernmentContractCard[]; myContractBids: ContractBidResult[] }>(
      `query CompanyContracts($companyId: UUID!) {
        companyContracts(companyId: $companyId) {
          id cityId cityName currencyCode title description productTypeId productName quantityRequired minimumQuality budgetCap
          deadlineTick status winnerCompanyId winnerCompanyName createdAtTick bidCount awardedBidPricePerUnit
          fulfilledQuantity fulfillmentPercent
        }
        myContractBids(companyId: $companyId) {
          id contractId companyId companyName bidPricePerUnit estimatedDeliveryTick submittedAtTick contractStatus
        }
      }`,
      { companyId: companyId.value },
    )

    contracts.value = data.companyContracts ?? []
    bids.value = data.myContractBids ?? []
    shipmentQuantities.value = Object.fromEntries(
      contracts.value
        .filter((contract) => contract.status === 'AWARDED')
        .map((contract) => [
          contract.id,
          Math.max(1, Math.ceil((contract.quantityRequired - (contract.fulfilledQuantity ?? 0)) / 2)),
        ]),
    )
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

async function fulfill(contract: GovernmentContractCard) {
  submitting.value = contract.id
  error.value = null
  try {
    await gqlRequest<{ fulfillContractShipment: ContractFulfillmentResult }>(
      `mutation FulfillContractShipment($input: FulfillContractShipmentInput!) {
        fulfillContractShipment(input: $input) {
          contractId status quantityDelivered quantityRequired fulfillmentPercent settledRevenue latePenaltyApplied
        }
      }`,
      {
        input: {
          contractId: contract.id,
          quantity: Number(shipmentQuantities.value[contract.id] ?? 1),
        },
      },
    )
    await loadData()
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    submitting.value = null
  }
}

onMounted(() => {
  void loadData()
})
</script>

<template>
  <div class="company-contracts-view pt-6 pb-16 lg:pt-8 lg:pb-20">
    <div class="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
      <header class="contracts-header">
        <h1>🏛️ {{ t('companyContracts.title') }}</h1>
        <p>{{ t('companyContracts.subtitle') }}</p>
        <RouterLink :to="`/ledger/${companyId}`" class="back-link">← {{ t('nav.ledger') }}</RouterLink>
      </header>

      <div v-if="loading" class="state-box">{{ t('common.loading') }}</div>
      <div v-else-if="error" class="state-box error">{{ error }}</div>
      <div v-else class="kanban-grid">
        <section class="kanban-column">
          <h2>{{ t('companyContracts.columnBidding') }}</h2>
          <article v-for="contract in biddingContracts" :key="contract.id" class="contract-item">
            <h3>{{ contract.title }}</h3>
            <p>{{ contract.productName }} • {{ contract.quantityRequired.toLocaleString() }}</p>
            <p>{{ t('companyContracts.bidCount') }}: {{ contract.bidCount }}</p>
          </article>
          <p v-if="biddingContracts.length === 0" class="empty">{{ t('companyContracts.emptyColumn') }}</p>
        </section>

        <section class="kanban-column">
          <h2>{{ t('companyContracts.columnAwarded') }}</h2>
          <article v-for="contract in awardedContracts" :key="contract.id" class="contract-item">
            <h3>{{ contract.title }}</h3>
            <p>{{ contract.productName }} • {{ contract.quantityRequired.toLocaleString() }}</p>
            <p>{{ t('companyContracts.awardedPrice') }}: {{ formatMoney(contract.awardedBidPricePerUnit ?? contract.budgetCap, contract.currencyCode, locale) }}</p>
            <div class="shipment-form">
              <input v-model.number="shipmentQuantities[contract.id]" type="number" min="1" step="1" />
              <button class="btn btn-primary btn-sm" :disabled="submitting === contract.id" @click="fulfill(contract)">
                {{ t('companyContracts.ship') }}
              </button>
            </div>
          </article>
          <p v-if="awardedContracts.length === 0" class="empty">{{ t('companyContracts.emptyColumn') }}</p>
        </section>

        <section class="kanban-column">
          <h2>{{ t('companyContracts.columnInFulfillment') }}</h2>
          <article v-for="contract in inFulfillmentContracts" :key="contract.id" class="contract-item">
            <h3>{{ contract.title }}</h3>
            <div class="progress-track">
              <span class="progress-fill" :style="{ width: `${contract.fulfillmentPercent ?? 0}%` }"></span>
            </div>
            <p>{{ (contract.fulfillmentPercent ?? 0).toFixed(1) }}%</p>
            <div class="shipment-form">
              <input v-model.number="shipmentQuantities[contract.id]" type="number" min="1" step="1" />
              <button class="btn btn-primary btn-sm" :disabled="submitting === contract.id" @click="fulfill(contract)">
                {{ t('companyContracts.ship') }}
              </button>
            </div>
          </article>
          <p v-if="inFulfillmentContracts.length === 0" class="empty">{{ t('companyContracts.emptyColumn') }}</p>
        </section>

        <section class="kanban-column">
          <h2>{{ t('companyContracts.columnCompleted') }}</h2>
          <article v-for="contract in completedContracts" :key="contract.id" class="contract-item">
            <h3>{{ contract.title }}</h3>
            <p>{{ t('companyContracts.completedRevenue') }}: {{ formatMoney((contract.awardedBidPricePerUnit ?? contract.budgetCap) * contract.quantityRequired, contract.currencyCode, locale) }}</p>
          </article>
          <p v-if="completedContracts.length === 0" class="empty">{{ t('companyContracts.emptyColumn') }}</p>
        </section>
      </div>

      <section class="bids-section">
        <h2>{{ t('companyContracts.bidHistory') }}</h2>
        <ul v-if="bids.length > 0" class="bid-list">
          <li v-for="bid in bids" :key="bid.id">
            {{ bid.companyName }} · {{ bid.bidPricePerUnit.toFixed(2) }} · {{ t('companyContracts.submittedTick', { tick: bid.submittedAtTick }) }}
          </li>
        </ul>
        <p v-else class="empty">{{ t('companyContracts.noBids') }}</p>
      </section>
    </div>
  </div>
</template>

<style scoped>
.contracts-header h1 {
  margin: 0;
}

.contracts-header p {
  margin: 0.25rem 0;
  color: var(--color-text-secondary);
}

.back-link {
  color: var(--color-primary);
  text-decoration: none;
}

.state-box {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 1rem;
}

.state-box.error {
  color: var(--color-danger);
  background: rgba(239, 68, 68, 0.1);
}

.kanban-grid {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.9rem;
}

.kanban-column {
  background: var(--color-card);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 0.8rem;
  min-height: 16rem;
}

.kanban-column h2 {
  margin: 0 0 0.6rem;
  font-size: 0.95rem;
}

.contract-item {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.6rem;
  margin-bottom: 0.6rem;
  background: var(--color-surface);
}

.contract-item h3 {
  margin: 0 0 0.25rem;
  font-size: 0.9rem;
}

.contract-item p {
  margin: 0.2rem 0;
  font-size: 0.82rem;
}

.empty {
  margin: 0;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.shipment-form {
  margin-top: 0.45rem;
  display: flex;
  gap: 0.4rem;
}

.shipment-form input {
  width: 5.5rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: 0.3rem 0.4rem;
  background: var(--color-card);
  color: var(--color-text);
}

.progress-track {
  height: 0.5rem;
  border-radius: 999px;
  background: var(--color-border);
  overflow: hidden;
}

.progress-fill {
  display: block;
  height: 100%;
  background: var(--color-primary);
}

.bids-section {
  margin-top: 1rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 0.8rem;
}

.bids-section h2 {
  margin: 0 0 0.5rem;
  font-size: 0.95rem;
}

.bid-list {
  margin: 0;
  padding-left: 1.1rem;
  display: grid;
  gap: 0.25rem;
}

@media (max-width: 1200px) {
  .kanban-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 720px) {
  .kanban-grid {
    grid-template-columns: 1fr;
  }
}
</style>
