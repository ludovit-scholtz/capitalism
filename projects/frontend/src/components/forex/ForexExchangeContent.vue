<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useForexData } from '@/composables/useForexData'
import ForexSwapSection from '@/components/forex/ForexSwapSection.vue'
import GoldAmmSection from '@/components/forex/GoldAmmSection.vue'
import BankAccountTransferPanel from '@/components/banking/BankAccountTransferPanel.vue'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const { auth, loading, error, rates, balances, history, contextScopedBankAccounts, hasBankAccounts, selectedCity, baseCurrencyCode, baseCurrencySymbol, cityRateBoard, rateUpdateDate, availableCurrencies, toBalances, loadData, reloadBankAccountsSilent, reloadAfterSwap, formatAmount, formatRate, formatTick } = useForexData()

type ForexTab = 'swap' | 'transfer' | 'rates' | 'history' | 'gold'

function parseForexTab(value: unknown): ForexTab {
  return value === 'transfer' || value === 'rates' || value === 'history' || value === 'gold' ? value : 'swap'
}

const activeTab = ref<ForexTab>(parseForexTab(route.query.tab))

const initialToCurrency = computed(() => {
  const q = route.query.toCurrency
  return typeof q === 'string' ? q : undefined
})

onMounted(async () => {
  if (!auth.isAuthenticated) { router.push('/login'); return }
  await loadData()
  activeTab.value = parseForexTab(route.query.tab)
})

watch(activeTab, async (tab) => {
  const nextQuery = { ...route.query }
  if (tab === 'swap') delete nextQuery.tab
  else nextQuery.tab = tab
  await router.replace({ query: nextQuery })
})
</script>

<template>
  <main class="container min-h-[calc(100vh-64px)] pb-16 pt-6 lg:pb-20 lg:pt-8">
    <div class="flex flex-col gap-10 lg:gap-12">
      <div class="forex-hero rounded-2xl border border-divider bg-card px-6 py-6 shadow-sm sm:px-8 sm:py-7">
        <h1 class="text-3xl font-bold text-body">{{ t('forex.title') }}</h1>
        <p class="text-base text-muted">{{ t('forex.subtitle') }}</p>
      </div>

      <UiStateLoading v-if="loading" :label="t('common.loading')" />
      <UiStateError v-else-if="error" :message="error" :retry-label="t('common.retry')" @retry="loadData()" />

      <template v-else>
        <div class="flex flex-col gap-8">
          <div class="flex flex-wrap gap-4" role="tablist" :aria-label="t('forex.tabsLabel')">
            <button v-for="tab in (['swap', 'transfer', 'rates', 'history', 'gold'] as const)" :key="tab" role="tab" :aria-selected="activeTab === tab"
              class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
              :class="activeTab === tab ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
              @click="activeTab = tab">
              {{ t(tab === 'swap' ? 'forex.tabSwap' : tab === 'transfer' ? 'bankTransfer.tabLabel' : tab === 'rates' ? 'forex.tabRateList' : tab === 'history' ? 'forex.tabHistory' : 'forex.tabGold') }}
            </button>
          </div>

          <ForexSwapSection
            v-if="activeTab === 'swap'"
            :rates="rates"
            :balances="balances"
            :context-scoped-bank-accounts="contextScopedBankAccounts"
            :has-bank-accounts="hasBankAccounts"
            :selected-city="selectedCity"
            :base-currency-symbol="baseCurrencySymbol"
            :base-currency-code="baseCurrencyCode"
            :to-balances="toBalances"
            :initial-to-currency="initialToCurrency"
            :available-currencies="availableCurrencies"
            @refresh="reloadAfterSwap()"
          />

          <BankAccountTransferPanel v-else-if="activeTab === 'transfer'" :accounts="contextScopedBankAccounts" @transferred="reloadBankAccountsSilent()" />

          <section v-else-if="activeTab === 'rates'" class="space-y-6 rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8" aria-label="Rate List">
            <h2 class="border-b border-divider pb-3 text-lg font-semibold text-body">{{ t('forex.rateListTitle') }}</h2>
            <div v-if="rates.length === 0" class="text-sm italic text-muted">{{ t('forex.rateListEmpty') }}</div>
            <template v-else>
              <div class="city-rate-context flex flex-wrap items-center gap-3 rounded-xl border border-brand/30 bg-brand/5 px-4 py-3">
                <div class="flex items-center gap-2">
                  <span class="text-2xl font-bold text-brand">{{ baseCurrencySymbol }}</span>
                  <div class="flex flex-col">
                    <span class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('forex.rateBaseCurrency') }}</span>
                    <span class="font-bold text-body">{{ baseCurrencyCode }}<span v-if="selectedCity" class="ml-1 font-normal text-muted">({{ selectedCity.name }})</span></span>
                  </div>
                </div>
                <div class="ml-auto text-right text-xs text-muted">
                  <div v-if="rateUpdateDate">{{ t('forex.rateUpdated') }}: {{ rateUpdateDate }}</div>
                  <div class="text-subtle">{{ t('forex.rateSourceNote') }}</div>
                </div>
              </div>
              <div>
                <p class="mb-3 text-sm text-muted">{{ t('forex.rateTableIntro', { base: `1 ${baseCurrencySymbol} ${baseCurrencyCode}` }) }}</p>
                <div class="overflow-x-auto">
                  <table class="rates-table w-full border-collapse text-sm">
                    <thead>
                      <tr>
                        <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.rateTableCurrency') }}</th>
                        <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.rateTableMidRate') }}</th>
                        <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.rateTableAfterFee') }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="row in cityRateBoard" :key="row.targetCode" class="history-row border-b border-divider/40 last:border-0">
                        <td class="px-3 py-3 align-middle">
                          <div class="flex items-center gap-2">
                            <span class="min-w-[2rem] text-base font-bold text-brand">{{ row.targetSymbol }}</span>
                            <span class="font-semibold text-body">{{ row.targetCode }}</span>
                          </div>
                        </td>
                        <td class="px-3 py-3 text-right font-mono font-semibold text-body align-middle">{{ formatRate(row.rate) }}</td>
                        <td class="px-3 py-3 text-right font-mono text-muted align-middle"><span class="text-sm">{{ formatRate(row.afterFeeRate) }}</span><span class="ml-1 text-xs text-subtle">-1%</span></td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <p class="mt-3 text-xs text-subtle">{{ t('forex.rateAfterFeeNote') }}</p>
              </div>
            </template>
          </section>

          <section v-else-if="activeTab === 'history'" class="space-y-4 rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8" aria-label="Trade History">
            <h2 class="border-b border-divider pb-3 text-lg font-semibold text-body">{{ t('forex.historyTitle') }}</h2>
            <div v-if="history.length === 0" class="text-sm italic text-muted">{{ t('forex.historyEmpty') }}</div>
            <div v-else class="overflow-x-auto">
              <table class="w-full border-collapse text-sm">
                <thead>
                  <tr>
                    <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.from') }}</th>
                    <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.to') }}</th>
                    <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.rate') }}</th>
                    <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.fee') }}</th>
                    <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">{{ t('forex.executedAt') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="entry in history" :key="entry.id" class="history-row">
                    <td class="px-3 py-2.5 align-middle"><span class="mr-0.5 font-bold text-brand">{{ entry.fromCurrencySymbol }}</span>{{ formatAmount(entry.fromAmount) }}<span class="ml-1 text-xs text-muted">{{ entry.fromCurrencyCode }}</span></td>
                    <td class="px-3 py-2.5 align-middle"><span class="mr-0.5 font-bold text-brand">{{ entry.toCurrencySymbol }}</span>{{ formatAmount(entry.toAmount) }}<span class="ml-1 text-xs text-muted">{{ entry.toCurrencyCode }}</span></td>
                    <td class="px-3 py-2.5 text-muted align-middle">{{ formatAmount(entry.rate) }}</td>
                    <td class="px-3 py-2.5 text-caution align-middle">{{ entry.fromCurrencySymbol }}{{ formatAmount(entry.feeAmount) }}</td>
                    <td class="px-3 py-2.5 text-subtle text-xs align-middle">{{ formatTick(entry.executedAtTick) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <GoldAmmSection v-if="activeTab === 'gold'" :available-currencies="availableCurrencies" :balances="balances" @refresh="loadData()" />
        </div>
      </template>
    </div>
  </main>
</template>

<style scoped>
.history-row td {
  border-bottom: 1px solid var(--color-border-light, rgba(48, 54, 61, 0.5));
}
.history-row:last-child td {
  border-bottom: none;
}
.history-row:hover td {
  background: var(--color-surface-raised);
}
</style>
