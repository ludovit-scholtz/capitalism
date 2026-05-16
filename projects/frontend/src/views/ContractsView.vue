<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { SupplyContractActionResult, SupplyContractCard } from '@/types'

type CompanyOption = { id: string; name: string }
type CompanyRankingOption = { companyId: string; companyName: string }

const { t, locale } = useI18n()

const loading = ref(false)
const submitting = ref(false)
const actionLoadingId = ref<string | null>(null)
const error = ref<string | null>(null)
const contracts = ref<SupplyContractCard[]>([])
const myCompanies = ref<CompanyOption[]>([])
const allCompanies = ref<CompanyOption[]>([])

const form = ref({
  sellerCompanyId: '',
  buyerCompanyId: '',
  sellerBuildingUnitId: '',
  resourceTypeId: '',
  productTypeId: '',
  quantityPerTick: 100,
  pricePerUnit: 10,
  durationTicks: 100,
  penaltyRatePercent: 10,
})

const pendingContracts = computed(() => contracts.value.filter((contract) => contract.status === 'PENDING'))
const activeContracts = computed(() => contracts.value.filter((contract) => contract.status === 'ACTIVE'))
const historyContracts = computed(() => contracts.value.filter((contract) => contract.status !== 'PENDING' && contract.status !== 'ACTIVE'))

const canSubmit = computed(() => {
  const hasItem = (!!form.value.resourceTypeId && !form.value.productTypeId) || (!form.value.resourceTypeId && !!form.value.productTypeId)
  return !!form.value.sellerCompanyId && !!form.value.buyerCompanyId && !!form.value.sellerBuildingUnitId && hasItem
})

function deliveryBadge(contract: SupplyContractCard): 'ok' | 'warning' | 'error' {
  if (contract.penaltyCount > 0) return 'error'
  if (contract.totalUndeliveredQuantity > 0) return 'warning'
  return 'ok'
}

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ myContracts: SupplyContractCard[] }>(
      `query MyContracts {
        myContracts(take: 200, skip: 0) {
          id sellerCompanyId sellerCompanyName buyerCompanyId buyerCompanyName sellerBuildingUnitId
          resourceTypeId resourceTypeName productTypeId productTypeName quantityPerTick pricePerUnit
          durationTicks remainingTicks startTick penaltyRatePercent currencyCode status createdAtTick
          totalDeliveredQuantity totalUndeliveredQuantity totalPenaltyAmount penaltyCount
        }
      }`,
    )
    contracts.value = data.myContracts ?? []

    const meData = await gqlRequest<{ me: { companies: CompanyOption[] } }>(
      `query MeCompanies { me { companies { id name } } }`,
    )
    myCompanies.value = meData.me?.companies ?? []
    if (!form.value.sellerCompanyId && myCompanies.value.length > 0) {
      form.value.sellerCompanyId = myCompanies.value[0]!.id
    }

    const rankingData = await gqlRequest<{ companyRankings: CompanyRankingOption[] }>(
      `query CompanyRankings { companyRankings { companyId companyName } }`,
    )
    allCompanies.value = (rankingData.companyRankings ?? []).map((company) => ({ id: company.companyId, name: company.companyName }))
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

async function proposeContract() {
  if (!canSubmit.value) return
  submitting.value = true
  error.value = null
  try {
    await gqlRequest<{ proposeSupplyContract: SupplyContractActionResult }>(
      `mutation ProposeSupplyContract($input: ProposeSupplyContractInput!) {
        proposeSupplyContract(input: $input) { success message contract { id } }
      }`,
      {
        input: {
          sellerCompanyId: form.value.sellerCompanyId,
          buyerCompanyId: form.value.buyerCompanyId,
          sellerBuildingUnitId: form.value.sellerBuildingUnitId,
          resourceTypeId: form.value.resourceTypeId || null,
          productTypeId: form.value.productTypeId || null,
          quantityPerTick: Number(form.value.quantityPerTick),
          pricePerUnit: Number(form.value.pricePerUnit),
          durationTicks: Number(form.value.durationTicks),
          penaltyRatePercent: Number(form.value.penaltyRatePercent),
        },
      },
    )
    await loadData()
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    submitting.value = false
  }
}

async function runAction(action: 'accept' | 'reject' | 'cancel', id: string) {
  actionLoadingId.value = id
  error.value = null
  try {
    if (action === 'accept') {
      await gqlRequest(`mutation AcceptSupplyContract($id: UUID!) { acceptSupplyContract(id: $id) { success } }`, { id })
    } else if (action === 'reject') {
      await gqlRequest(`mutation RejectSupplyContract($id: UUID!) { rejectSupplyContract(id: $id) { success } }`, { id })
    } else {
      await gqlRequest(`mutation CancelSupplyContract($id: UUID!) { cancelSupplyContract(id: $id) { success } }`, { id })
    }
    await loadData()
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    actionLoadingId.value = null
  }
}

onMounted(() => {
  void loadData()
})
</script>

<template>
  <div class="contracts-page pt-6 pb-16 lg:pt-8 lg:pb-20">
    <div class="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
      <header class="mb-6">
        <h1 class="text-2xl font-bold">{{ t('contractsPage.title') }}</h1>
        <p class="text-sm text-muted">{{ t('contractsPage.subtitle') }}</p>
      </header>

      <section class="mb-8 rounded-xl border border-divider bg-card p-4">
        <h2 class="mb-3 text-lg font-semibold">{{ t('contractsPage.createTitle') }}</h2>
        <div class="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.sellerCompany') }}</span>
            <select v-model="form.sellerCompanyId" class="rounded border border-divider bg-card px-3 py-2">
              <option v-for="company in myCompanies" :key="company.id" :value="company.id">{{ company.name }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.buyerCompany') }}</span>
            <select v-model="form.buyerCompanyId" class="rounded border border-divider bg-card px-3 py-2">
              <option value="" disabled>{{ t('contractsPage.selectBuyer') }}</option>
              <option v-for="company in allCompanies" :key="company.id" :value="company.id">{{ company.name }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.sellerUnit') }}</span>
            <input v-model.trim="form.sellerBuildingUnitId" class="rounded border border-divider bg-card px-3 py-2" placeholder="UUID" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.resourceTypeId') }}</span>
            <input v-model.trim="form.resourceTypeId" class="rounded border border-divider bg-card px-3 py-2" placeholder="UUID" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.productTypeId') }}</span>
            <input v-model.trim="form.productTypeId" class="rounded border border-divider bg-card px-3 py-2" placeholder="UUID" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.quantityPerTick') }}</span>
            <input v-model.number="form.quantityPerTick" type="number" min="0.0001" step="0.0001" class="rounded border border-divider bg-card px-3 py-2" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.pricePerUnit') }}</span>
            <input v-model.number="form.pricePerUnit" type="number" min="0.0001" step="0.0001" class="rounded border border-divider bg-card px-3 py-2" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.durationTicks') }}</span>
            <select v-model.number="form.durationTicks" class="rounded border border-divider bg-card px-3 py-2">
              <option :value="25">25</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
              <option :value="200">200</option>
              <option :value="500">500</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span>{{ t('contractsPage.penaltyRatePercent') }}</span>
            <input v-model.number="form.penaltyRatePercent" type="number" min="0" max="100" step="0.1" class="rounded border border-divider bg-card px-3 py-2" />
          </label>
        </div>
        <button class="btn btn-primary mt-4" :disabled="submitting || !canSubmit" @click="proposeContract">
          {{ submitting ? t('contractsPage.submitting') : t('contractsPage.create') }}
        </button>
      </section>

      <p v-if="loading" class="text-sm text-muted">{{ t('common.loading') }}</p>
      <p v-else-if="error" class="text-sm text-danger">{{ error }}</p>

      <div v-else class="grid gap-6 lg:grid-cols-3">
        <section>
          <h2 class="mb-3 text-lg font-semibold">{{ t('contractsPage.pending') }}</h2>
          <article v-for="contract in pendingContracts" :key="contract.id" class="contract-card mb-3 rounded-xl border border-divider bg-card p-4">
            <h3 class="font-semibold">{{ contract.resourceTypeName ?? contract.productTypeName }}</h3>
            <p class="text-sm text-muted">{{ contract.sellerCompanyName }} → {{ contract.buyerCompanyName }}</p>
            <p class="text-sm">{{ formatMoney(contract.pricePerUnit, contract.currencyCode, locale) }} / {{ contract.quantityPerTick }}</p>
            <div class="mt-2 flex gap-2">
              <button class="btn btn-primary btn-sm" :disabled="actionLoadingId === contract.id" @click="runAction('accept', contract.id)">{{ t('contractsPage.accept') }}</button>
              <button class="btn btn-danger btn-sm" :disabled="actionLoadingId === contract.id" @click="runAction('reject', contract.id)">{{ t('contractsPage.reject') }}</button>
            </div>
          </article>
          <p v-if="pendingContracts.length === 0" class="text-sm text-muted">{{ t('contractsPage.emptyPending') }}</p>
        </section>

        <section>
          <h2 class="mb-3 text-lg font-semibold">{{ t('contractsPage.active') }}</h2>
          <article v-for="contract in activeContracts" :key="contract.id" class="contract-card mb-3 rounded-xl border border-divider bg-card p-4">
            <h3 class="font-semibold">{{ contract.resourceTypeName ?? contract.productTypeName }}</h3>
            <p class="text-sm text-muted">{{ contract.sellerCompanyName }} → {{ contract.buyerCompanyName }}</p>
            <p class="text-sm">{{ t('contractsPage.remainingTicks', { ticks: contract.remainingTicks }) }}</p>
            <p class="text-sm">{{ t('contractsPage.delivered', { quantity: contract.totalDeliveredQuantity }) }}</p>
            <span class="health-badge mt-2 inline-block rounded px-2 py-1 text-xs" :class="`health-${deliveryBadge(contract)}`">
              {{ t(`contractsPage.health.${deliveryBadge(contract)}`) }}
            </span>
            <div class="mt-2">
              <button class="btn btn-danger btn-sm" :disabled="actionLoadingId === contract.id" @click="runAction('cancel', contract.id)">{{ t('contractsPage.cancel') }}</button>
            </div>
          </article>
          <p v-if="activeContracts.length === 0" class="text-sm text-muted">{{ t('contractsPage.emptyActive') }}</p>
        </section>

        <section>
          <h2 class="mb-3 text-lg font-semibold">{{ t('contractsPage.history') }}</h2>
          <article v-for="contract in historyContracts" :key="contract.id" class="contract-card mb-3 rounded-xl border border-divider bg-card p-4">
            <h3 class="font-semibold">{{ contract.resourceTypeName ?? contract.productTypeName }}</h3>
            <p class="text-sm text-muted">{{ contract.status }}</p>
            <p class="text-sm">{{ t('contractsPage.delivered', { quantity: contract.totalDeliveredQuantity }) }}</p>
            <p class="text-sm">{{ t('contractsPage.penaltyTotal', { amount: formatMoney(contract.totalPenaltyAmount, contract.currencyCode, locale) }) }}</p>
          </article>
          <p v-if="historyContracts.length === 0" class="text-sm text-muted">{{ t('contractsPage.emptyHistory') }}</p>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
.health-ok {
  background: var(--color-success-soft);
  color: var(--color-success);
}

.health-warning {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.health-error {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
</style>
