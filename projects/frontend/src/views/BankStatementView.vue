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
  <main class="bank-statement-page container">
    <div class="statement-hero">
      <h1 class="statement-title">🏦 {{ t('bankStatement.title') }}</h1>
      <p class="statement-subtitle">{{ t('bankStatement.subtitle') }}</p>
    </div>

    <!-- Company selector -->
    <div v-if="companies.length > 1" class="company-selector-row">
      <label for="company-select" class="company-label">{{ t('bankStatement.selectCompany') }}</label>
      <select
        id="company-select"
        :value="companyId"
        class="company-select"
        @change="(e) => router.push(`/bank-statement/${(e.target as HTMLSelectElement).value}`)"
      >
        <option v-for="c in companies" :key="c.id" :value="c.id">{{ c.name }}</option>
      </select>
    </div>

    <!-- Limit selector -->
    <div class="limit-row">
      <label for="limit-select" class="limit-label">{{ t('bankStatement.showEntries') }}</label>
      <select id="limit-select" v-model.number="limit" class="limit-select">
        <option :value="20">20</option>
        <option :value="50">50</option>
        <option :value="100">100</option>
        <option :value="200">200</option>
      </select>
    </div>

    <!-- Loading / error -->
    <div v-if="loading" class="state-message" role="status">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="state-message state-error" role="alert">{{ error }}</div>

    <template v-else-if="statement">
      <!-- Summary header -->
      <div class="account-summary-card" aria-label="Account summary">
        <div class="summary-company">
          <span class="company-icon">🏢</span>
          <span class="company-name">{{ statement.companyName }}</span>
        </div>
        <div class="summary-balance-block">
          <span class="balance-label">{{ t('bankStatement.currentBalance') }}</span>
          <span
            class="balance-amount"
            :class="statement.currentBalance >= 0 ? 'balance-positive' : 'balance-negative'"
          >
            {{ formatBalance(statement.currentBalance, statement.currencyCode) }}
          </span>
        </div>
        <div class="summary-meta">
          <span class="meta-item">{{ t('bankStatement.totalEntries') }}: {{ statement.totalEntries }}</span>
          <span class="meta-item">{{ t('bankStatement.currency') }}: {{ statement.currencyCode }}</span>
        </div>
      </div>

      <!-- Transaction table -->
      <div class="statement-table-wrap">
        <table class="statement-table" aria-label="Bank statement transactions">
          <thead>
            <tr>
              <th class="col-date">{{ t('bankStatement.columns.date') }}</th>
              <th class="col-tick">{{ t('bankStatement.columns.tick') }}</th>
              <th class="col-description">{{ t('bankStatement.columns.description') }}</th>
              <th class="col-category">{{ t('bankStatement.columns.category') }}</th>
              <th class="col-debit">{{ t('bankStatement.columns.debit') }}</th>
              <th class="col-credit">{{ t('bankStatement.columns.credit') }}</th>
              <th class="col-balance">{{ t('bankStatement.columns.balance') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-if="statement.rows.length === 0"
              class="empty-row"
            >
              <td colspan="7" class="empty-cell">{{ t('bankStatement.noTransactions') }}</td>
            </tr>
            <tr
              v-for="row in statement.rows"
              :key="row.id"
              class="statement-row"
              :class="row.amount >= 0 ? 'row-credit' : 'row-debit'"
            >
              <td class="col-date">{{ formatDate(row.recordedAtUtc) }}</td>
              <td class="col-tick tick-badge">{{ row.recordedAtTick }}</td>
              <td class="col-description">
                <div class="description-main">{{ row.description || '—' }}</div>
                <div v-if="row.buildingName" class="description-sub">🏭 {{ row.buildingName }}</div>
              </td>
              <td class="col-category">
                <span class="category-badge">{{ categoryLabel(row.category) }}</span>
              </td>
              <td class="col-debit debit-cell">
                <span v-if="row.amount < 0">
                  {{ statement.currencySymbol }}{{ formatAmount(row.amount) }}
                </span>
                <span v-else class="empty-cell-dash">—</span>
              </td>
              <td class="col-credit credit-cell">
                <span v-if="row.amount >= 0">
                  {{ statement.currencySymbol }}{{ formatAmount(row.amount) }}
                </span>
                <span v-else class="empty-cell-dash">—</span>
              </td>
              <td class="col-balance balance-cell">
                <span :class="row.runningBalance >= 0 ? 'balance-positive' : 'balance-negative'">
                  {{ statement.currencySymbol }}{{ formatAmount(row.runningBalance) }}
                </span>
                <span class="direction-icon">{{ categoryIcon(row.category) }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="hasMore" class="load-more-hint">
        {{ t('bankStatement.showingFirst', { count: totalShown, total: statement.totalEntries }) }}
        <button class="btn-link" @click="limit = 200">{{ t('bankStatement.showAll') }}</button>
      </div>
    </template>
  </main>
</template>

<style scoped>
.bank-statement-page {
  padding: 2rem 0 4rem;
  min-height: calc(100vh - 64px);
}

.statement-hero {
  margin-bottom: 1.5rem;
}

.statement-title {
  font-size: 2rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 0.4rem;
}

.statement-subtitle {
  color: var(--color-text-muted);
  font-size: 1.05rem;
}

.company-selector-row,
.limit-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.company-label,
.limit-label {
  font-size: 0.88rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.company-select,
.limit-select {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  color: var(--color-text-primary);
  font-size: 0.9rem;
  cursor: pointer;
}

.company-select:focus,
.limit-select:focus {
  outline: 2px solid var(--color-accent, #4f8ef7);
}

.state-message {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-muted);
}

.state-error {
  color: var(--color-error, #ea5455);
}

/* Account summary */
.account-summary-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg, 12px);
  padding: 1.25rem 1.5rem;
  margin-bottom: 1.5rem;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1.5rem;
}

.summary-company {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.company-icon {
  font-size: 1.4rem;
}

.company-name {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.summary-balance-block {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  margin-left: auto;
}

.balance-label {
  font-size: 0.78rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-muted);
}

.balance-amount {
  font-size: 1.5rem;
  font-weight: 800;
}

.balance-positive {
  color: var(--color-success, #28c76f);
}

.balance-negative {
  color: var(--color-error, #ea5455);
}

.summary-meta {
  display: flex;
  gap: 1rem;
  font-size: 0.82rem;
  color: var(--color-text-muted);
}

/* Table */
.statement-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg, 12px);
}

.statement-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.statement-table th {
  text-align: left;
  padding: 0.65rem 0.85rem;
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-muted);
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
  white-space: nowrap;
}

.statement-row td {
  padding: 0.7rem 0.85rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  vertical-align: middle;
}

.statement-row:last-child td {
  border-bottom: none;
}

.row-credit {
  background: rgba(40, 199, 111, 0.02);
}

.row-debit {
  background: rgba(234, 84, 85, 0.02);
}

.statement-row:hover td {
  background: var(--color-surface-alt, #1e2a3a);
}

.col-date {
  white-space: nowrap;
  color: var(--color-text-muted);
  font-size: 0.83rem;
}

.tick-badge {
  font-size: 0.8rem;
  color: var(--color-text-muted);
  font-variant-numeric: tabular-nums;
}

.col-description {
  max-width: 280px;
}

.description-main {
  color: var(--color-text-primary);
  font-weight: 500;
}

.description-sub {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  margin-top: 0.15rem;
}

.category-badge {
  display: inline-block;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  background: var(--color-surface-alt, #1e2a3a);
  border: 1px solid var(--color-border);
  border-radius: 4px;
  padding: 0.1rem 0.4rem;
  white-space: nowrap;
}

.debit-cell {
  color: var(--color-error, #ea5455);
  font-weight: 600;
  text-align: right;
}

.credit-cell {
  color: var(--color-success, #28c76f);
  font-weight: 600;
  text-align: right;
}

.balance-cell {
  text-align: right;
  white-space: nowrap;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.empty-cell-dash {
  color: var(--color-text-muted);
}

.empty-cell {
  text-align: center;
  color: var(--color-text-muted);
  font-style: italic;
  padding: 2rem;
}

.direction-icon {
  font-size: 0.7rem;
  margin-left: 0.2rem;
  color: var(--color-text-muted);
}

/* Load more */
.load-more-hint {
  margin-top: 1rem;
  font-size: 0.85rem;
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn-link {
  background: none;
  border: none;
  color: var(--color-accent, #4f8ef7);
  font-size: 0.85rem;
  cursor: pointer;
  padding: 0;
  text-decoration: underline;
  font-weight: 600;
}

.btn-link:hover {
  opacity: 0.8;
}

@media (max-width: 640px) {
  .col-tick,
  .col-date {
    display: none;
  }

  .account-summary-card {
    flex-direction: column;
    gap: 0.75rem;
  }

  .summary-balance-block {
    margin-left: 0;
  }
}
</style>
