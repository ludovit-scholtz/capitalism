<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'

const { t, locale } = useI18n()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const companyId = computed(() => route.params.companyId as string)

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

interface Company {
  id: string
  name: string
  cash: number
}

const loading = ref(true)
const error = ref<string | null>(null)
const statement = ref<BankStatementResult | null>(null)
const companies = ref<Company[]>([])
const limit = ref(50)

const BANK_STATEMENT_QUERY = `
  query BankStatement($companyId: UUID!, $limit: Int) {
    bankStatement(companyId: $companyId, limit: $limit) {
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

async function loadStatement() {
  if (!companyId.value) return
  loading.value = true
  error.value = null
  try {
    const result = await gqlRequest<{ bankStatement: BankStatementResult }>(
      BANK_STATEMENT_QUERY,
      { companyId: companyId.value, limit: limit.value },
    )
    statement.value = result.bankStatement
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }
  if (!auth.player) {
    await auth.fetchMe()
  }
  companies.value = (auth.player?.companies ?? []) as Company[]
  if (!companyId.value && companies.value.length > 0) {
    const firstCompany = companies.value[0]
    if (firstCompany) router.replace(`/bank-statement/${firstCompany.id}`)
    return
  }
  await loadStatement()
})

watch(companyId, (id) => {
  if (id) loadStatement()
})

watch(limit, () => loadStatement())

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
  if (['PURCHASING_COST', 'LABOR_COST', 'ENERGY_COST', 'PROPERTY_PURCHASE', 'CONSTRUCTION_COST', 'UNIT_UPGRADE', 'MARKETING', 'SHIPPING_COST', 'TAX', 'DIVIDEND', 'LOAN_ORIGINATION', 'LOAN_INTEREST_EXPENSE', 'LOAN_PENALTY', 'DEPOSIT_MADE', 'MEDIA_HOUSE_CONTENT', 'STOCK_PURCHASE'].includes(cat)) return '−'
  return ''
}

const totalShown = computed(() => statement.value?.rows.length ?? 0)
const hasMore = computed(() => (statement.value?.totalEntries ?? 0) > totalShown.value)
</script>

<template>
  <main class="container py-8 pb-16 min-h-[calc(100vh-64px)]">
    <!-- Hero -->
    <div class="mb-6">
      <h1 class="text-3xl font-bold text-body mb-1">🏦 {{ t('bankStatement.title') }}</h1>
      <p class="text-muted text-base">{{ t('bankStatement.subtitle') }}</p>
    </div>

    <!-- Company selector -->
    <div v-if="companies.length > 1" class="flex items-center gap-3 mb-4">
      <label for="company-select" class="text-sm font-semibold text-muted whitespace-nowrap">
        {{ t('bankStatement.selectCompany') }}
      </label>
      <select
        id="company-select"
        :value="companyId"
        class="selector-select bg-card border border-divider rounded-lg px-3 py-2 text-body text-sm cursor-pointer focus:outline-none focus:border-brand"
        @change="(e) => router.push(`/bank-statement/${(e.target as HTMLSelectElement).value}`)"
      >
        <option v-for="c in companies" :key="c.id" :value="c.id">{{ c.name }}</option>
      </select>
    </div>

    <!-- Limit selector -->
    <div class="flex items-center gap-3 mb-6">
      <label for="limit-select" class="text-sm font-semibold text-muted whitespace-nowrap">
        {{ t('bankStatement.showEntries') }}
      </label>
      <select
        id="limit-select"
        v-model.number="limit"
        class="selector-select bg-card border border-divider rounded-lg px-3 py-2 text-body text-sm cursor-pointer focus:outline-none focus:border-brand"
      >
        <option :value="20">20</option>
        <option :value="50">50</option>
        <option :value="100">100</option>
        <option :value="200">200</option>
      </select>
    </div>

    <!-- Loading / error -->
    <div v-if="loading" class="state-message text-center py-12 text-muted" role="status">
      {{ t('common.loading') }}
    </div>
    <div v-else-if="error" class="state-message text-center py-12 text-bad" role="alert">
      {{ error }}
    </div>

    <template v-else-if="statement">
      <!-- Account summary card -->
      <div
        class="flex flex-wrap items-center gap-6 bg-card border border-divider rounded-xl px-6 py-4 mb-6"
        aria-label="Account summary"
      >
        <div class="flex items-center gap-2">
          <span class="text-2xl">🏢</span>
          <span class="text-lg font-bold text-body">{{ statement.companyName }}</span>
        </div>
        <div class="flex flex-col gap-0 ml-auto sm:ml-auto">
          <span class="text-xs font-semibold text-muted uppercase tracking-wide">
            {{ t('bankStatement.currentBalance') }}
          </span>
          <span
            class="balance-amount text-2xl font-extrabold"
            :class="statement.currentBalance >= 0 ? 'text-good' : 'text-bad'"
          >
            {{ formatBalance(statement.currentBalance, statement.currencyCode) }}
          </span>
        </div>
        <div class="flex gap-4 text-xs text-muted">
          <span>{{ t('bankStatement.totalEntries') }}: {{ statement.totalEntries }}</span>
          <span>{{ t('bankStatement.currency') }}: {{ statement.currencyCode }}</span>
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
            <tr
              v-for="row in statement.rows"
              :key="row.id"
              class="statement-row"
              :class="row.amount >= 0 ? 'row-credit' : 'row-debit'"
            >
              <td class="px-3 py-2.5 text-muted text-xs whitespace-nowrap align-middle hidden sm:table-cell">
                {{ formatDate(row.recordedAtUtc) }}
              </td>
              <td class="px-3 py-2.5 text-muted text-xs tabular-nums align-middle hidden sm:table-cell">
                {{ row.recordedAtTick }}
              </td>
              <td class="px-3 py-2.5 align-middle max-w-[280px]">
                <div class="text-body font-medium">{{ row.description || '—' }}</div>
                <div v-if="row.buildingName" class="description-sub text-xs text-muted mt-0.5">
                  🏭 {{ row.buildingName }}
                </div>
              </td>
              <td class="px-3 py-2.5 align-middle">
                <span class="inline-block text-xs font-semibold text-muted bg-card-raised border border-divider rounded px-1.5 py-0.5 whitespace-nowrap">
                  {{ categoryLabel(row.category) }}
                </span>
              </td>
              <td class="debit-cell px-3 py-2.5 text-right text-bad font-semibold align-middle">
                <span v-if="row.amount < 0">
                  {{ statement.currencySymbol }}{{ formatAmount(row.amount) }}
                </span>
                <span v-else class="empty-cell-dash text-muted">—</span>
              </td>
              <td class="credit-cell px-3 py-2.5 text-right text-good font-semibold align-middle">
                <span v-if="row.amount >= 0">
                  {{ statement.currencySymbol }}{{ formatAmount(row.amount) }}
                </span>
                <span v-else class="empty-cell-dash text-muted">—</span>
              </td>
              <td class="px-3 py-2.5 text-right whitespace-nowrap font-bold tabular-nums align-middle">
                <span :class="row.runningBalance >= 0 ? 'text-good' : 'text-bad'">
                  {{ statement.currencySymbol }}{{ formatAmount(row.runningBalance) }}
                </span>
                <span class="text-xs text-muted ml-0.5">{{ categoryIcon(row.category) }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="hasMore" class="flex items-center gap-2 mt-4 text-sm text-muted">
        {{ t('bankStatement.showingFirst', { count: totalShown, total: statement.totalEntries }) }}
        <button
          class="bg-transparent border-0 text-brand text-sm cursor-pointer underline font-semibold hover:opacity-80"
          @click="limit = 200"
        >
          {{ t('bankStatement.showAll') }}
        </button>
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
</style>
