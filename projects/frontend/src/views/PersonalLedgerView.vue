<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { useTickRefresh } from '@/composables/useTickRefresh'
import CurrencyAmount from '@/components/numbers/CurrencyAmount.vue'
import type { EndgameStatus, PersonAccount } from '@/types/index'

const PERSONAL_STOCK_SALE_TAX_RATE = 0.15

const { t, locale } = useI18n()
const auth = useAuthStore()

const personAccount = ref<PersonAccount | null>(null)
const endgameStatus = ref<EndgameStatus | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

const PERSON_ACCOUNT_QUERY = `
  query PersonAccountLedger {
    personAccount {
      playerId
      displayName
      personalCash
      taxReserve
      availableCash
      totalNetWealth
      activeAccountType
      activeCompanyId
      shareholdings {
        companyId
        companyName
        shareCount
        ownershipRatio
        sharePrice
        marketValue
      }
      dividendPayments {
        id
        companyId
        companyName
        shareCount
        amountPerShare
        totalAmount
        gameYear
        recordedAtTick
        recordedAtUtc
        description
      }
      stockTrades {
        id
        companyId
        companyName
        direction
        shareCount
        pricePerShare
        totalValue
        recordedAtTick
        recordedAtUtc
      }
    }
  }
`

const ENDGAME_STATUS_QUERY = `
  {
    endgameStatus {
      gameEnded
      winnerPlayerId
      winnerDisplayName
      winnerCompanyName
      gameEndedAtUtc
      winningThresholdUsd
      topRealWorldRichest {
        name
        wealthUsd
      }
    }
  }
`

const portfolioValue = computed(() =>
  (personAccount.value?.shareholdings ?? []).reduce((sum, h) => sum + h.marketValue, 0),
)

const sellTrades = computed(() =>
  (personAccount.value?.stockTrades ?? []).filter((t) => t.direction === 'SELL'),
)

const raceProgressPercent = computed(() => {
  const threshold = endgameStatus.value?.winningThresholdUsd ?? 0
  if (threshold <= 0 || !personAccount.value) return 0
  return Math.min(100, Math.round((personAccount.value.totalNetWealth / threshold) * 100))
})

async function loadData(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null

  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }
    const data = await gqlRequest<{ personAccount: PersonAccount | null }>(PERSON_ACCOUNT_QUERY)
    personAccount.value = data.personAccount
    const endgameData = await gqlRequest<{ endgameStatus: EndgameStatus }>(ENDGAME_STATUS_QUERY)
    endgameStatus.value = endgameData.endgameStatus
  } catch (reason: unknown) {
    if (!isRefresh) {
      error.value = reason instanceof Error ? reason.message : t('personalLedger.loadFailed')
    }
  } finally {
    loading.value = false
  }
}

function formatShares(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

onMounted(() => void loadData())

useTickRefresh(() => loadData(true))
</script>

<template>
  <div
    class="min-h-screen"
    style="background: radial-gradient(circle at top, color-mix(in srgb, var(--color-primary) 10%, transparent), transparent 35%), var(--color-background);"
  >
    <!-- Hero -->
    <section class="pb-8 pt-14">
      <div class="container">
        <p class="mb-2 text-[0.8rem] font-bold uppercase tracking-[0.14em] text-brand">
          {{ t('personalLedger.eyebrow') }}
        </p>
        <h1>{{ t('personalLedger.title') }}</h1>
        <p class="mb-2 mt-3 max-w-4xl text-muted">{{ t('personalLedger.subtitle') }}</p>
        <RouterLink
          to="/stocks"
          class="mt-2 inline-block text-sm text-muted no-underline hover:text-brand hover:underline"
        >
          {{ t('personalLedger.backToExchange') }}
        </RouterLink>
      </div>
    </section>

    <!-- Body -->
    <div class="container flex flex-col gap-6 pb-12">
      <!-- Loading -->
      <div
        v-if="loading"
        class="flex flex-col items-center gap-4 rounded-2xl border border-divider bg-card px-6 py-10 text-center shadow-sm"
      >
        <p>{{ t('common.loading') }}</p>
      </div>

      <!-- Error -->
      <div
        v-else-if="error"
        class="flex flex-col items-center gap-4 rounded-2xl border border-bad bg-card px-6 py-10 text-center shadow-sm"
        role="alert"
      >
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="() => void loadData()">
          {{ t('common.tryAgain') }}
        </button>
      </div>

      <!-- Unauthenticated -->
      <div
        v-else-if="!personAccount"
        class="flex flex-col items-center gap-4 rounded-2xl border border-divider bg-card px-6 py-10 text-center shadow-sm"
      >
        <p>{{ t('personalLedger.loadFailed') }}</p>
        <RouterLink to="/login" class="btn btn-primary">{{ t('common.login') }}</RouterLink>
      </div>

      <template v-else>
        <!-- Wealth summary panel -->
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <h2 class="mb-5 text-lg font-semibold">{{ t('personalLedger.wealthBreakdownTitle') }}</h2>

          <div class="wealth-grid grid gap-4 [grid-template-columns:repeat(auto-fit,minmax(200px,1fr))]">
            <!-- Net wealth — brand-tinted primary card -->
            <article
              class="flex flex-col gap-1.5 rounded-[14px] border p-4"
              style="border-color: color-mix(in srgb, var(--color-primary) 40%, var(--color-border)); background: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));"
            >
              <span class="wealth-label text-xs font-bold uppercase tracking-[0.06em] text-muted">
                {{ t('personalLedger.netWealth') }}
              </span>
              <strong class="text-[1.8rem] font-bold tabular-nums text-brand">
                <CurrencyAmount :amount="personAccount.totalNetWealth" currency="EUR" />
              </strong>
              <span class="text-[0.78rem] leading-snug text-muted">
                {{ t('personalLedger.netWealthDesc') }}
              </span>
            </article>

            <!-- Available cash -->
            <article
              class="flex flex-col gap-1.5 rounded-[14px] border border-divider p-4"
              style="background: color-mix(in srgb, var(--color-surface) 60%, var(--color-background));"
            >
              <span class="wealth-label text-xs font-bold uppercase tracking-[0.06em] text-muted">
                {{ t('personalLedger.availableCash') }}
              </span>
              <strong class="text-[1.35rem] font-bold tabular-nums text-body">
                <CurrencyAmount :amount="personAccount.availableCash" currency="EUR" />
              </strong>
              <span class="text-[0.78rem] leading-snug text-muted">
                {{ t('personalLedger.availableCashDesc') }}
              </span>
            </article>

            <!-- Tax reserve — amber-tinted when non-zero -->
            <article
              class="flex flex-col gap-1.5 rounded-[14px] border p-4"
              :class="
                personAccount.taxReserve > 0
                  ? 'border-amber-500/40 bg-amber-500/[0.06]'
                  : 'border-divider bg-card/60'
              "
            >
              <span class="wealth-label text-xs font-bold uppercase tracking-[0.06em] text-muted">
                {{ t('personalLedger.taxReserve') }}
              </span>
              <strong
                class="wealth-amount--blocked text-[1.35rem] font-bold tabular-nums"
                :class="personAccount.taxReserve > 0 ? 'text-amber-700' : 'text-body'"
              >
                <CurrencyAmount :amount="personAccount.taxReserve" currency="EUR" />
              </strong>
              <span class="text-[0.78rem] leading-snug text-muted">
                {{ t('personalLedger.taxReserveDesc') }}
              </span>
            </article>

            <!-- Portfolio value -->
            <article
              class="flex flex-col gap-1.5 rounded-[14px] border border-divider p-4"
              style="background: color-mix(in srgb, var(--color-surface) 60%, var(--color-background));"
            >
              <span class="wealth-label text-xs font-bold uppercase tracking-[0.06em] text-muted">
                {{ t('personalLedger.portfolioValue') }}
              </span>
              <strong class="text-[1.35rem] font-bold tabular-nums text-body">
                <CurrencyAmount :amount="portfolioValue" currency="EUR" />
              </strong>
              <span class="text-[0.78rem] leading-snug text-muted">
                {{ t('personalLedger.portfolioValueDesc') }}
              </span>
            </article>
          </div>

          <!-- Tax note -->
          <p
            v-if="personAccount.taxReserve > 0"
            class="mt-4 rounded-[10px] border border-amber-500/35 bg-amber-500/[0.12] px-4 py-3 text-sm text-muted"
            role="note"
          >
            {{ t('personalLedger.taxNote') }}
          </p>
        </section>

        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <div class="flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold">{{ t('endgame.raceTitle') }}</h2>
              <p class="mt-1 text-sm text-muted">{{ t('endgame.raceSubtitle') }}</p>
            </div>
            <div class="text-right">
              <p class="text-xs font-bold uppercase tracking-[0.06em] text-muted">{{ t('endgame.yourProgress') }}</p>
              <p class="text-lg font-bold text-brand">{{ raceProgressPercent }}%</p>
            </div>
          </div>
          <div class="mt-4 h-2.5 overflow-hidden rounded-full bg-card-raised">
            <div class="h-full bg-brand transition-all" :style="{ width: `${raceProgressPercent}%` }" />
          </div>
          <div class="mt-4 overflow-x-auto">
            <table class="w-full border-collapse text-sm" :aria-label="t('endgame.raceTitle')">
              <thead>
                <tr>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('endgame.rank') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('endgame.person') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('endgame.wealthUsd') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(entry, index) in endgameStatus?.topRealWorldRichest ?? []" :key="entry.name">
                  <td class="border-b border-divider px-3 py-[0.55rem] font-semibold">#{{ index + 1 }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem]">{{ entry.name }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums">
                    <CurrencyAmount :amount="entry.wealthUsd" currency="USD" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p class="mt-3 text-xs text-muted">
            {{ t('endgame.thresholdHint', { target: endgameStatus?.winningThresholdUsd ?? 0 }) }}
          </p>
        </section>

        <!-- Tax reserve history (sell trades) -->
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <div class="mb-4 flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold">{{ t('personalLedger.taxHistoryTitle') }}</h2>
              <p class="mt-1 text-sm text-muted">{{ t('personalLedger.taxHistoryDesc') }}</p>
            </div>
            <span
              v-if="personAccount.taxReserve > 0"
              class="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full border border-amber-500/40 bg-amber-500/15 px-3 py-1 text-[0.82rem] text-amber-800"
            >
              {{ t('personalLedger.totalReserved') }}:
              <strong><CurrencyAmount :amount="personAccount.taxReserve" currency="EUR" /></strong>
            </span>
          </div>

          <p v-if="sellTrades.length === 0" class="py-6 text-center text-sm text-muted">
            {{ t('personalLedger.taxHistoryEmpty') }}
          </p>
          <div v-else class="overflow-x-auto">
            <table class="w-full border-collapse text-sm" :aria-label="t('personalLedger.taxHistoryTitle')">
              <thead>
                <tr>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.company') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.shares') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.price') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.total') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.taxReservedCol') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.recordedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="trade in sellTrades" :key="trade.id">
                  <td class="tax-cell border-b border-divider px-3 py-[0.55rem]">{{ trade.companyName }}</td>
                  <td class="tax-cell border-b border-divider px-3 py-[0.55rem] text-right tabular-nums">{{ formatShares(trade.shareCount) }}</td>
                  <td class="tax-cell border-b border-divider px-3 py-[0.55rem] text-right tabular-nums"><CurrencyAmount :amount="trade.pricePerShare" currency="EUR" /></td>
                  <td class="tax-cell border-b border-divider px-3 py-[0.55rem] text-right tabular-nums"><CurrencyAmount :amount="trade.totalValue" currency="EUR" /></td>
                  <td class="tax-cell border-b border-divider px-3 py-[0.55rem] text-right tabular-nums font-semibold text-amber-700"><CurrencyAmount :amount="trade.totalValue * PERSONAL_STOCK_SALE_TAX_RATE" currency="EUR" /></td>
                  <td class="tax-cell border-b border-divider px-3 py-[0.55rem]">{{ formatDateTime(trade.recordedAtUtc) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Full trade history -->
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <div class="mb-4 flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold">{{ t('personalLedger.tradeHistoryTitle') }}</h2>
              <p class="mt-1 text-sm text-muted">{{ t('personalLedger.tradeHistoryDesc') }}</p>
            </div>
          </div>

          <p v-if="personAccount.stockTrades.length === 0" class="py-6 text-center text-sm text-muted">
            {{ t('personalLedger.tradeHistoryEmpty') }}
          </p>
          <div v-else class="overflow-x-auto">
            <table class="w-full border-collapse text-sm" :aria-label="t('personalLedger.tradeHistoryTitle')">
              <thead>
                <tr>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.company') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.direction') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.shares') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.price') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.total') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.recordedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="trade in personAccount.stockTrades" :key="trade.id">
                  <td class="border-b border-divider px-3 py-[0.55rem]">{{ trade.companyName }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem]">
                    <span
                      class="inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-[0.72rem] font-bold"
                      :class="
                        trade.direction === 'BUY'
                          ? 'direction-badge--buy bg-good/15 text-good'
                          : 'direction-badge--sell bg-bad/15 text-bad'
                      "
                    >
                      {{ trade.direction === 'BUY' ? t('personalLedger.buy') : t('personalLedger.sell') }}
                    </span>
                  </td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums">{{ formatShares(trade.shareCount) }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums"><CurrencyAmount :amount="trade.pricePerShare" currency="EUR" /></td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums"><CurrencyAmount :amount="trade.totalValue" currency="EUR" /></td>
                  <td class="border-b border-divider px-3 py-[0.55rem]">{{ formatDateTime(trade.recordedAtUtc) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Dividend history -->
        <section class="rounded-2xl border border-divider bg-card p-6 shadow-sm">
          <div class="mb-4 flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold">{{ t('personalLedger.dividendHistoryTitle') }}</h2>
              <p class="mt-1 text-sm text-muted">{{ t('personalLedger.dividendHistoryDesc') }}</p>
            </div>
          </div>

          <p v-if="personAccount.dividendPayments.length === 0" class="py-6 text-center text-sm text-muted">
            {{ t('personalLedger.dividendHistoryEmpty') }}
          </p>
          <div v-else class="overflow-x-auto">
            <table class="w-full border-collapse text-sm" :aria-label="t('personalLedger.dividendHistoryTitle')">
              <thead>
                <tr>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.company') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.gameYear') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.shares') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.perShare') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-right text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.amount') }}</th>
                  <th class="border-b border-divider px-3 py-2 text-left text-[0.78rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('personalLedger.recordedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="div in personAccount.dividendPayments" :key="div.id">
                  <td class="border-b border-divider px-3 py-[0.55rem]">{{ div.companyName }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem]">{{ div.gameYear }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums">{{ formatShares(div.shareCount) }}</td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums"><CurrencyAmount :amount="div.amountPerShare" currency="EUR" /></td>
                  <td class="border-b border-divider px-3 py-[0.55rem] text-right tabular-nums"><CurrencyAmount :amount="div.totalAmount" currency="EUR" /></td>
                  <td class="border-b border-divider px-3 py-[0.55rem]">{{ formatDateTime(div.recordedAtUtc) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
    </div>
  </div>
</template>
