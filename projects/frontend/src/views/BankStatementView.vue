<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { PlayerBankAccountSummary } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const routeAccountOrCompanyId = computed(() => route.params.companyId as string)

interface BankStatementRow {
  id: string
  recordedAtTick: number
  recordedAtUtc: string
  description: string
  category: string
  amount: number
  runningBalance: number
  buildingId: string | null
  buildingName: string | null
}

interface BankStatementResult {
  companyId: string
  companyName: string
  currencyCode: string
  currencySymbol: string
  currentBalance: number
  totalEntries: number
  rows: BankStatementRow[]
}

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
  if (isPersonalContext.value) {
    return accounts.value.filter((account) => account.ownerType === 'PERSON')
  }

  const activeCompanyId = auth.player?.activeCompanyId
  if (!activeCompanyId) {
    return []
  }

  return accounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === activeCompanyId)
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

function formatAmount(val: number): string {
  return new Intl.NumberFormat(locale.value, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Math.abs(val))
}

function formatBalance(val: number, currencyCode: string): string {
  return formatMoney(val, currencyCode, locale.value)
}

function formatDate(utc: string): string {
  return new Date(utc).toLocaleDateString(locale.value, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function categoryLabel(cat: string): string {
  const map: Record<string, string> = {
    REVENUE: t('bankStatement.category.revenue'),
    PURCHASING_COST: t('bankStatement.category.purchasingCost'),
    LABOR_COST: t('bankStatement.category.laborCost'),
    ENERGY_COST: t('bankStatement.category.energyCost'),
    PROPERTY_PURCHASE: t('bankStatement.category.propertyPurchase'),
    CONSTRUCTION_COST: t('bankStatement.category.constructionCost'),
    UNIT_UPGRADE: t('bankStatement.category.unitUpgrade'),
    MARKETING: t('bankStatement.category.marketing'),
    SHIPPING_COST: t('bankStatement.category.shippingCost'),
    MEDIA_HOUSE_INCOME: t('bankStatement.category.mediaHouseIncome'),
    MEDIA_HOUSE_CONTENT: t('bankStatement.category.mediaHouseContent'),
    TAX: t('bankStatement.category.tax'),
    DIVIDEND: t('bankStatement.category.dividend'),
    RENT_INCOME: t('bankStatement.category.rentIncome'),
    OTHER: t('bankStatement.category.other'),
    LOAN_ORIGINATION: t('bankStatement.category.loanOrigination'),
    LOAN_REPAYMENT_PRINCIPAL: t('bankStatement.category.loanRepayment'),
    LOAN_INTEREST_EXPENSE: t('bankStatement.category.loanInterestExpense'),
    LOAN_INTEREST_INCOME: t('bankStatement.category.loanInterestIncome'),
    LOAN_PENALTY: t('bankStatement.category.loanPenalty'),
    DEPOSIT_MADE: t('bankStatement.category.depositMade'),
    DEPOSIT_WITHDRAWN: t('bankStatement.category.depositWithdrawn'),
    DEPOSIT_INTEREST_PAID: t('bankStatement.category.depositInterestPaid'),
    DEPOSIT_INTEREST_RECEIVED: t('bankStatement.category.depositInterestReceived'),
    CENTRAL_BANK_BORROW: t('bankStatement.category.centralBankBorrow'),
    CENTRAL_BANK_REPAY: t('bankStatement.category.centralBankRepay'),
    STOCK_PURCHASE: t('bankStatement.category.stockPurchase'),
    STOCK_SALE: t('bankStatement.category.stockSale'),
  }
  return map[cat] ?? cat
}

function categoryIcon(cat: string): string {
  if (['REVENUE', 'MEDIA_HOUSE_INCOME', 'RENT_INCOME', 'DEPOSIT_INTEREST_RECEIVED', 'LOAN_INTEREST_INCOME', 'STOCK_SALE'].includes(cat)) return '+'
  if (
    [
      'PURCHASING_COST',
      'LABOR_COST',
      'ENERGY_COST',
      'PROPERTY_PURCHASE',
      'CONSTRUCTION_COST',
      'UNIT_UPGRADE',
      'MARKETING',
      'SHIPPING_COST',
      'TAX',
      'DIVIDEND',
      'LOAN_ORIGINATION',
      'LOAN_INTEREST_EXPENSE',
      'LOAN_PENALTY',
      'DEPOSIT_MADE',
      'MEDIA_HOUSE_CONTENT',
      'STOCK_PURCHASE',
    ].includes(cat)
  )
    return '−'
  return ''
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
      <select
        id="account-select"
        :value="selectedAccount?.id ?? ''"
        class="selector-select bg-card border border-divider rounded-lg px-3 py-2 text-body text-sm cursor-pointer focus:outline-none focus:border-brand"
        @change="onAccountChange"
      >
        <option v-for="account in contextAccounts" :key="account.id" :value="account.id">
          {{ account.ownerDisplayName }} · {{ account.accountNumber }} · {{ account.currencyCode }} · {{ account.currencySymbol }}{{ formatAmount(account.balance) }}
        </option>
      </select>
    </div>

    <!-- Limit selector + tick range filter -->
    <div class="flex flex-wrap items-center gap-4 mb-6">
      <div class="flex items-center gap-2">
        <label for="limit-select" class="text-sm font-semibold text-muted whitespace-nowrap">
          {{ t('bankStatement.showEntries') }}
        </label>
        <select
          id="limit-select"
          v-model.number="pageSize"
          class="selector-select bg-card border border-divider rounded-lg px-3 py-2 text-body text-sm cursor-pointer focus:outline-none focus:border-brand"
        >
          <option :value="20">20</option>
          <option :value="50">50</option>
          <option :value="100">100</option>
          <option :value="200">200</option>
        </select>
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
      <!-- Account summary card -->
      <div class="flex flex-wrap items-center gap-6 bg-card border border-divider rounded-xl px-6 py-4 mb-6" aria-label="Account summary">
        <div class="flex items-center gap-2">
          <span class="text-2xl">🏢</span>
          <div class="flex flex-col gap-0.5">
            <span class="text-lg font-bold text-body">{{ selectedAccount?.ownerDisplayName ?? statement.companyName }}</span>
            <span v-if="selectedAccount" class="text-xs text-muted"> {{ t('bankStatement.accountNumber') }}: {{ selectedAccount.accountNumber }} </span>
          </div>
        </div>
        <div class="flex flex-col gap-0 ml-auto sm:ml-auto">
          <span class="text-xs font-semibold text-muted uppercase tracking-wide">
            {{ t('bankStatement.currentBalance') }}
          </span>
          <span class="balance-amount text-2xl font-extrabold" :class="selectedAccountBalance >= 0 ? 'text-good' : 'text-bad'">
            {{ formatBalance(selectedAccountBalance, selectedAccount?.currencyCode ?? statement.currencyCode) }}
          </span>
        </div>
        <div class="flex gap-4 text-xs text-muted">
          <span>{{ t('bankStatement.totalEntries') }}: {{ statement.totalEntries }}</span>
          <span>{{ t('bankStatement.currency') }}: {{ selectedAccount?.currencyCode ?? statement.currencyCode }}</span>
        </div>
      </div>

      <!-- Transaction table -->
      <div class="overflow-x-auto border border-divider rounded-xl">
        <table class="statement-table w-full border-collapse text-sm" aria-label="Bank statement transactions">
          <thead>
            <tr>
              <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card whitespace-nowrap hidden sm:table-cell">
                {{ t('bankStatement.columns.date') }}
              </th>
              <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card whitespace-nowrap hidden sm:table-cell">
                {{ t('bankStatement.columns.tick') }}
              </th>
              <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
                {{ t('bankStatement.columns.description') }}
              </th>
              <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
                {{ t('bankStatement.columns.category') }}
              </th>
              <th class="text-right px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
                {{ t('bankStatement.columns.debit') }}
              </th>
              <th class="text-right px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
                {{ t('bankStatement.columns.credit') }}
              </th>
              <th class="text-right px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card whitespace-nowrap">
                {{ t('bankStatement.columns.balance') }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="statement.rows.length === 0">
              <td colspan="7" class="text-center text-muted italic py-8 px-4">
                {{ t('bankStatement.noTransactions') }}
              </td>
            </tr>
            <tr v-for="row in statement.rows" :key="row.id" class="statement-row" :class="row.amount >= 0 ? 'row-credit' : 'row-debit'">
              <td class="px-3 py-2.5 text-muted text-xs whitespace-nowrap align-middle hidden sm:table-cell">
                {{ formatDate(row.recordedAtUtc) }}
              </td>
              <td class="px-3 py-2.5 text-muted text-xs tabular-nums align-middle hidden sm:table-cell">
                {{ row.recordedAtTick }}
              </td>
              <td class="px-3 py-2.5 align-middle max-w-[280px]">
                <div class="text-body font-medium">{{ row.description || '—' }}</div>
                <div v-if="row.buildingName" class="description-sub text-xs text-muted mt-0.5">🏭 {{ row.buildingName }}</div>
              </td>
              <td class="px-3 py-2.5 align-middle">
                <span class="inline-block text-xs font-semibold text-muted bg-card-raised border border-divider rounded px-1.5 py-0.5 whitespace-nowrap">
                  {{ categoryLabel(row.category) }}
                </span>
              </td>
              <td class="debit-cell px-3 py-2.5 text-right text-bad font-semibold align-middle">
                <span v-if="row.amount < 0"> {{ statement.currencySymbol }}{{ formatAmount(row.amount) }} </span>
                <span v-else class="empty-cell-dash text-muted">—</span>
              </td>
              <td class="credit-cell px-3 py-2.5 text-right text-good font-semibold align-middle">
                <span v-if="row.amount >= 0"> {{ statement.currencySymbol }}{{ formatAmount(row.amount) }} </span>
                <span v-else class="empty-cell-dash text-muted">—</span>
              </td>
              <td class="px-3 py-2.5 text-right whitespace-nowrap font-bold tabular-nums align-middle">
                <span :class="row.runningBalance >= 0 ? 'text-good' : 'text-bad'"> {{ statement.currencySymbol }}{{ formatAmount(row.runningBalance) }} </span>
                <span class="text-xs text-muted ml-0.5">{{ categoryIcon(row.category) }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="flex flex-col gap-3 mt-4 sm:flex-row sm:items-center sm:justify-between text-sm text-muted">
        <span>{{ t('bankStatement.showingFirst', { count: totalShown, total: statement.totalEntries }) }}</span>
        <div class="flex items-center gap-3">
          <span>{{ t('bankStatement.pageSummary', { page, total: totalPages }) }}</span>
          <button class="pagination-btn" :disabled="!hasPreviousPage" @click="goToPreviousPage">
            {{ t('bankStatement.previousPage') }}
          </button>
          <button class="pagination-btn" :disabled="!hasNextPage" @click="goToNextPage">
            {{ t('bankStatement.nextPage') }}
          </button>
        </div>
      </div>
    </template>
  </main>
</template>

<style scoped>
/* Custom dropdown arrow for selectors */
.selector-select {
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20' fill='%236b7280'%3E%3Cpath fill-rule='evenodd' d='M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z' clip-rule='evenodd'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 0.6rem center;
  background-size: 1.1rem;
  padding-right: 2rem;
  appearance: none;
}

/* Table row hover and subtle row tinting */
.statement-row td {
  border-bottom: 1px solid var(--color-border-light, rgba(48, 54, 61, 0.5));
}
.statement-row:last-child td {
  border-bottom: none;
}
.row-credit {
  background: rgba(34, 197, 94, 0.02);
}
.row-debit {
  background: rgba(248, 113, 113, 0.02);
}
.statement-row:hover td {
  background: var(--color-surface-raised);
}

.pagination-btn {
  background: var(--color-surface-raised);
  color: var(--color-text);
  border: 1px solid var(--color-border-light, rgba(48, 54, 61, 0.5));
  border-radius: 0.5rem;
  padding: 0.45rem 0.8rem;
  cursor: pointer;
  font-weight: 600;
}

.pagination-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
