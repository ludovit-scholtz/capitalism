<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import BankStatementSummaryCard from '@/components/banking/BankStatementSummaryCard.vue'
import BankStatementTable from '@/components/banking/BankStatementTable.vue'
import type { BankStatementResult, PlayerBankAccountSummary } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const routeAccountOrCompanyId = computed(() => route.params.companyId as string)

const loading = ref(true)
const error = ref<string | null>(null)
const statement = ref<BankStatementResult | null>(null)
const accounts = ref<PlayerBankAccountSummary[]>([])
const pageSize = ref(50)
const page = ref(1)
const fromTick = ref<number | null>(null)
const toTick = ref<number | null>(null)
const isPersonalContext = computed(() => auth.player?.activeAccountType === 'PERSON')

const contextAccounts = computed<PlayerBankAccountSummary[]>(() => {
  let filtered: PlayerBankAccountSummary[] = []

  if (isPersonalContext.value) {
    filtered = accounts.value.filter((account) => account.ownerType === 'PERSON')
  } else {
    const activeCompanyId = auth.player?.activeCompanyId
    if (!activeCompanyId) {
      return []
    }
    filtered = accounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === activeCompanyId)
  }

  return filtered
})

const selectedAccount = computed<PlayerBankAccountSummary | null>(() => contextAccounts.value.find((account) => account.id === routeAccountOrCompanyId.value) ?? null)

const BANK_STATEMENT_QUERY = `
  query BankStatement($companyId: UUID!, $accountId: UUID, $limit: Int, $offset: Int, $fromTick: Long, $toTick: Long) {
    bankStatement(companyId: $companyId, accountId: $accountId, limit: $limit, offset: $offset, fromTick: $fromTick, toTick: $toTick) {
      companyId
      companyName
      currencyCode
      currencySymbol
      currentBalance
      totalEntries
      rows {
        id
        recordedAtTick
        recordedAtUtc
        description
        category
        amount
        runningBalance
        buildingId
        buildingName
      }
    }
  }
`

const MY_BANK_ACCOUNTS_QUERY = `
  {
    myBankAccounts {
      id
      accountNumber
      currencyCode
      currencySymbol
      balance
      companyId
      companyName
      ownerType
      ownerDisplayName
      bankBuildingId
      cityId
    }
  }
`

async function loadStatement() {
  if (!selectedAccount.value) return
  if (!selectedAccount.value.companyId) {
    statement.value = null
    loading.value = false
    return
  }

  loading.value = true
  error.value = null
  try {
    const result = await gqlRequest<{ bankStatement: BankStatementResult }>(BANK_STATEMENT_QUERY, {
      companyId: selectedAccount.value.companyId,
      accountId: selectedAccount.value.id,
      limit: pageSize.value,
      offset: (page.value - 1) * pageSize.value,
      fromTick: fromTick.value ?? undefined,
      toTick: toTick.value ?? undefined,
    })
    statement.value = result.bankStatement
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function loadAccounts() {
  const result = await gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY)
  accounts.value = result.myBankAccounts ?? []
}

async function syncRouteToAccount() {
  if (contextAccounts.value.length === 0) {
    statement.value = null
    loading.value = false
    return false
  }

  const routeId = routeAccountOrCompanyId.value
  const matchingAccount = contextAccounts.value.find((account) => account.id === routeId)
  if (matchingAccount) {
    return true
  }

  const firstAccountForCompany = contextAccounts.value.find((account) => account.companyId === routeId)
  const fallbackAccount = firstAccountForCompany ?? contextAccounts.value[0] ?? null
  if (!fallbackAccount) {
    statement.value = null
    loading.value = false
    return false
  }

  await router.replace(`/bank-statement/${fallbackAccount.id}`)
  return false
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }
  if (!auth.player) {
    await auth.fetchMe()
  }

  await loadAccounts()
  if (!(await syncRouteToAccount())) {
    return
  }

  await loadStatement()
})

watch(routeAccountOrCompanyId, async (id, previousId) => {
  if (!id || id === previousId) return
  page.value = 1
  if (selectedAccount.value) {
    await loadStatement()
  }
})

watch(
  () => [auth.player?.activeAccountType, auth.player?.activeCompanyId],
  async () => {
    page.value = 1
    if (!(await syncRouteToAccount())) {
      return
    }
    await loadStatement()
  },
)

watch(pageSize, async (value, previousValue) => {
  if (value === previousValue) return
  page.value = 1
  await loadStatement()
})

watch(page, async (value, previousValue) => {
  if (value === previousValue) return
  await loadStatement()
})

watch([fromTick, toTick], async () => {
  page.value = 1
  await loadStatement()
})

function formatAmount(val: number, currencyCode?: string): string {
  const code = currencyCode ?? statement.value?.currencyCode ?? 'EUR'
  return formatMoney(Math.abs(val), code, locale.value)
}

const totalShown = computed(() => statement.value?.rows.length ?? 0)
const totalPages = computed(() => Math.max(1, Math.ceil((statement.value?.totalEntries ?? 0) / pageSize.value)))
const hasPreviousPage = computed(() => page.value > 1)
const hasNextPage = computed(() => page.value < totalPages.value)
const selectedAccountBalance = computed(() => selectedAccount.value?.balance ?? 0)

function goToAccount(accountId: string) {
  router.push(`/bank-statement/${accountId}`)
}

function onAccountChange(event: Event) {
  const accountId = (event.target as HTMLSelectElement | null)?.value
  if (!accountId) return
  goToAccount(accountId)
}

function goToPreviousPage() {
  if (hasPreviousPage.value) {
    page.value -= 1
  }
}

function goToNextPage() {
  if (hasNextPage.value) {
    page.value += 1
  }
}
</script>

<template>
  <main class="container py-8 pb-16 min-h-[calc(100vh-64px)]">
    <!-- Hero -->
    <div class="mb-6">
      <h1 class="text-3xl font-bold text-body mb-1">🏦 {{ t('bankStatement.title') }}</h1>
      <p class="text-muted text-base">{{ t('bankStatement.subtitle') }}</p>
    </div>

    <!-- Account selector -->
    <div v-if="contextAccounts.length > 0" class="flex flex-col gap-3 mb-4 lg:flex-row lg:items-center">
      <label for="account-select" class="text-sm font-semibold text-muted whitespace-nowrap">
        {{ t('bankStatement.selectAccount') }}
      </label>
      <div class="relative">
        <select
          id="account-select"
          :value="selectedAccount?.id ?? ''"
          class="bg-card border border-divider rounded-lg px-3 py-2 pr-10 text-body text-sm cursor-pointer appearance-none focus:outline-none focus:border-brand"
          @change="onAccountChange"
        >
          <option v-for="account in contextAccounts" :key="account.id" :value="account.id">
            {{ account.ownerDisplayName }} · {{ account.accountNumber }} · {{ account.currencyCode }} · {{ formatAmount(account.balance, account.currencyCode) }}
          </option>
        </select>
        <span class="pointer-events-none absolute inset-y-0 right-3 flex items-center text-muted" aria-hidden="true">
          <svg class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" />
          </svg>
        </span>
      </div>
    </div>

    <!-- Limit selector + tick range filter -->
    <div class="flex flex-wrap items-center gap-4 mb-6">
      <div class="flex items-center gap-2">
        <label for="limit-select" class="text-sm font-semibold text-muted whitespace-nowrap">
          {{ t('bankStatement.showEntries') }}
        </label>
        <div class="relative">
          <select
            id="limit-select"
            v-model.number="pageSize"
            class="bg-card border border-divider rounded-lg px-3 py-2 pr-10 text-body text-sm cursor-pointer appearance-none focus:outline-none focus:border-brand"
          >
            <option :value="20">20</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
            <option :value="200">200</option>
          </select>
          <span class="pointer-events-none absolute inset-y-0 right-3 flex items-center text-muted" aria-hidden="true">
            <svg class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
              <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" />
            </svg>
          </span>
        </div>
      </div>
      <div class="flex items-center gap-2">
        <label for="from-tick" class="text-sm font-semibold text-muted whitespace-nowrap">{{ t('bankStatement.fromTick') }}</label>
        <input
          id="from-tick"
          v-model.number="fromTick"
          type="number"
          min="0"
          step="1"
          :placeholder="t('bankStatement.tickPlaceholder')"
          class="w-28 px-3 py-2 border border-divider rounded-lg bg-card text-body text-sm focus:outline-none focus:border-brand"
        />
      </div>
      <div class="flex items-center gap-2">
        <label for="to-tick" class="text-sm font-semibold text-muted whitespace-nowrap">{{ t('bankStatement.toTick') }}</label>
        <input
          id="to-tick"
          v-model.number="toTick"
          type="number"
          min="0"
          step="1"
          :placeholder="t('bankStatement.tickPlaceholder')"
          class="w-28 px-3 py-2 border border-divider rounded-lg bg-card text-body text-sm focus:outline-none focus:border-brand"
        />
      </div>
      <button v-if="fromTick !== null || toTick !== null" class="text-xs text-muted hover:text-bad transition-colors">
        {{ t('common.clearFilter') }}
      </button>
    </div>

    <!-- Loading / error -->
    <div v-if="loading" class="state-message text-center py-12 text-muted" role="status">
      {{ t('common.loading') }}
    </div>
    <div v-else-if="error" class="state-message text-center py-12 text-bad" role="alert">
      {{ error }}
    </div>
    <div v-else-if="contextAccounts.length === 0" class="state-message text-center py-12 text-muted" role="status">
      {{ t('bankStatement.noOwnedAccounts') }}
    </div>

    <template v-else-if="statement">
      <BankStatementSummaryCard
        :owner-display-name="selectedAccount?.ownerDisplayName ?? statement.companyName"
        :account-number="selectedAccount?.accountNumber ?? null"
        :balance="selectedAccountBalance"
        :currency-code="selectedAccount?.currencyCode ?? statement.currencyCode"
        :total-entries="statement.totalEntries"
      />

      <BankStatementTable
        :rows="statement.rows"
        :currency-code="statement.currencyCode"
        :total-entries="statement.totalEntries"
        :total-shown="totalShown"
        :page="page"
        :total-pages="totalPages"
        :has-previous-page="hasPreviousPage"
        :has-next-page="hasNextPage"
        @previous-page="goToPreviousPage"
        @next-page="goToNextPage"
      />
    </template>
  </main>
</template>
