<template>
  <tr class="listing-row" :class="{ 'listing-row--expanded': expanded }">
    <td class="company-cell">
      <span class="company-name">{{ listing.companyName }}</span>
      <span v-if="listing.canClaimControl && !isControlledCompany" class="listing-chip listing-chip--control">{{ t('stockExchange.controlReady') }}</span>
      <span v-if="listing.canMerge" class="listing-chip listing-chip--merge">{{ t('stockExchange.mergeReady') }}</span>
      <span v-else-if="listing.playerOwnedShares + listing.controlledCompanyOwnedShares > 0" class="listing-chip listing-chip--owned">{{ t('stockExchange.ownedBadge') }}</span>
    </td>
    <td class="price-cell">
      <div class="price-stack">
        <span class="price-main">{{ formatCurrency(listing.sharePrice) }}</span>
        <span class="price-meta">{{ t('stockExchange.bidAskHint', { bid: formatCurrency(listing.bidPrice), ask: formatCurrency(listing.askPrice) }) }}</span>
      </div>
    </td>
    <td>{{ formatCurrency(listing.marketValue) }}</td>
    <td>{{ formatShares(listing.publicFloatShares) }}</td>
    <td>
      <div class="ownership-cell">
        <span>{{ formatPercent(listing.combinedControlledOwnershipRatio) }}</span>
        <span v-if="listing.playerOwnedShares > 0" class="owned-shares-hint">{{ t('stockExchange.personalShares', { shares: formatShares(listing.playerOwnedShares) }) }}</span>
        <span v-if="listing.controlledCompanyOwnedShares > 0" class="owned-shares-hint">
          {{ t('stockExchange.controlledCompanyShares', { shares: formatShares(listing.controlledCompanyOwnedShares) }) }}
        </span>
      </div>
    </td>
    <td class="dividend-cell">
      <span class="dividend-badge">{{ formatPercent(listing.dividendPayoutRatio) }}</span>
    </td>
    <td v-if="showActions" class="actions-cell">
      <button class="btn btn-primary btn-sm" :class="{ 'btn-active': expanded }" @click="emit('toggle-trade-panel')">
        {{ expanded ? t('stockExchange.closeTrade') : t('stockExchange.openTrade') }}
      </button>
      <button v-if="listing.canClaimControl && !isControlledCompany" class="btn btn-ghost btn-sm" :disabled="actionLoadingKey === `switch-${listing.companyId}`" @click="emit('switch-to-company')">
        {{ t('stockExchange.claimControl') }}
      </button>
      <button v-if="listing.canMerge" class="btn btn-warning btn-sm" @click="emit('open-merge')">{{ t('stockExchange.mergeCompany') }}</button>
    </td>
  </tr>

  <tr v-if="expanded" class="trade-panel-row">
    <td :colspan="showActions ? 7 : 6">
      <div class="trade-panel">
        <div class="company-snapshot">
          <dl class="snapshot-grid">
            <div class="snapshot-item">
              <dt>{{ t('stockExchange.totalSharesLabel') }}</dt>
              <dd>{{ formatShares(listing.totalSharesIssued) }}</dd>
            </div>
            <div class="snapshot-item">
              <dt>{{ t('stockExchange.publicFloatLabel') }}</dt>
              <dd>
                {{ formatShares(listing.publicFloatShares) }}
                <span class="snapshot-pct">({{ formatPercent(listing.publicFloatShares / listing.totalSharesIssued) }})</span>
              </dd>
            </div>
            <div class="snapshot-item">
              <dt>{{ t('stockExchange.dividendPayoutLabel') }}</dt>
              <dd>{{ formatPercent(listing.dividendPayoutRatio) }}</dd>
            </div>
          </dl>
        </div>

        <div class="trade-price-context">
          <div class="trade-price-item">
            <span class="trade-price-label">{{ t('stockExchange.askPriceLabel') }}</span>
            <strong class="trade-price-value trade-price-ask">{{ formatCurrency(listing.askPrice) }}</strong>
            <span class="trade-price-hint">{{ t('stockExchange.askPriceHint') }}</span>
          </div>
          <div class="trade-price-item">
            <span class="trade-price-label">{{ t('stockExchange.bidPriceLabel') }}</span>
            <strong class="trade-price-value trade-price-bid">{{ formatCurrency(listing.bidPrice) }}</strong>
            <span class="trade-price-hint">{{ t('stockExchange.bidPriceHint') }}</span>
          </div>
        </div>

        <div class="trade-form">
          <div class="trade-order-panel">
            <div class="trade-order-header">
              <div class="trade-order-context">
                <span class="trade-order-caption">{{ t('stockExchange.tradeAccountLabel') }}</span>
                <strong class="trade-order-name">{{ activeTradeAccountName }}</strong>
                <p class="trade-order-hint">
                  {{ activeTradeAccountType === 'COMPANY' ? t('stockExchange.tradeAccountHintCompany') : t('stockExchange.tradeAccountHintPerson') }}
                </p>
                <label class="trade-order-caption mt-2 block">{{ t('stockExchange.settlementAccountLabel') }}</label>
                <select
                  :value="selectedSettlementBankAccountId"
                  class="trade-input mt-1"
                  :aria-label="t('stockExchange.settlementAccountLabel')"
                  @change="emit('update:settlement-bank-account-id', ($event.target as HTMLSelectElement).value)"
                >
                  <option value="">{{ t('stockExchange.selectSettlementAccount') }}</option>
                  <option v-for="account in activeSettlementAccounts" :key="account.id" :value="account.id">{{ account.accountNumber }} · {{ formatCurrency(account.balance) }}</option>
                </select>
                <p v-if="activeSettlementAccounts.length === 0" class="trade-order-hint mt-1">{{ t('stockExchange.noUsdSettlementAccount') }}</p>
              </div>
              <span v-if="activeTradeAccountCash !== null" class="trade-account-cash">{{ formatCurrency(activeTradeAccountCash) }}</span>
            </div>

            <div class="trade-order-controls">
              <div class="trade-controls-labels" aria-hidden="true">
                <span class="trade-field-label">{{ t('stockExchange.quantity') }}</span>
                <span class="trade-field-label">{{ t('stockExchange.buyLabel') }}</span>
                <span class="trade-field-label">{{ t('stockExchange.sellLabel') }}</span>
              </div>
              <div class="trade-controls-inputs">
                <input
                  :value="quantity"
                  type="number"
                  min="1"
                  step="1"
                  class="trade-input"
                  :aria-label="`${t('stockExchange.quantity')} ${listing.companyName}`"
                  @input="emit('update-quantity', Number(($event.target as HTMLInputElement).value))"
                />
                <button class="btn btn-primary trade-action-btn" :disabled="actionLoadingKey === `buy-${listing.companyId}`" @click="emit('buy')">
                  {{ t('stockExchange.buyAt', { price: formatCurrency(listing.askPrice) }) }}
                </button>
                <button class="btn btn-secondary trade-action-btn" :disabled="actionLoadingKey === `sell-${listing.companyId}`" @click="emit('sell')">
                  {{ t('stockExchange.sellAt', { price: formatCurrency(listing.bidPrice) }) }}
                </button>
              </div>
              <div class="trade-controls-estimates">
                <span aria-hidden="true"></span>
                <span class="trade-est" aria-live="polite">{{ t('stockExchange.estimatedCost', { total: formatCurrency(estimatedBuyCost) }) }}</span>
                <span class="trade-est" aria-live="polite">{{ t('stockExchange.estimatedProceeds', { total: formatCurrency(estimatedSellProceeds) }) }}</span>
              </div>
            </div>
          </div>
        </div>

        <p v-if="successMessage" class="trade-feedback trade-feedback--success" role="status">{{ successMessage }}</p>
        <p v-if="errorMessage" class="trade-feedback trade-feedback--error" role="alert">{{ errorMessage }}</p>

        <div class="history-panel">
          <div class="history-panel__header">
            <h3>{{ t('stockExchange.priceHistoryTitle') }}</h3>
            <span class="history-panel__hint">{{ t('stockExchange.priceHistoryHint') }}</span>
          </div>
          <p v-if="priceHistoryLoading" class="history-panel__state">{{ t('common.loading') }}</p>
          <p v-else-if="priceHistoryError" class="history-panel__state history-panel__state--error">{{ priceHistoryError }}</p>
          <p v-else-if="!priceHistory.length" class="history-panel__state">{{ t('stockExchange.priceHistoryEmpty') }}</p>
          <div v-else class="history-table-wrapper">
            <table class="history-table" :aria-label="`${listing.companyName} ${t('stockExchange.priceHistoryTitle')}`">
              <thead>
                <tr>
                  <th>{{ t('stockExchange.tick') }}</th>
                  <th>{{ t('stockExchange.sharePrice') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="point in priceHistory" :key="`${listing.companyId}-${point.tick}-${point.recordedAtUtc}`">
                  <td>{{ point.tick }}</td>
                  <td>{{ formatCurrency(point.price) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="shareholders-panel">
          <div class="history-panel__header">
            <h3>{{ t('stockExchange.shareholdersTitle') }}</h3>
            <span class="history-panel__hint">{{ t('stockExchange.shareholdersDesc') }}</span>
          </div>
          <p v-if="shareholdersLoading" class="history-panel__state">{{ t('stockExchange.shareholdersLoading') }}</p>
          <p v-else-if="shareholdersError" class="history-panel__state history-panel__state--error">{{ shareholdersError }}</p>
          <template v-else-if="shareholders">
            <div class="shareholders-summary">
              <span class="shareholders-summary__item">
                {{ t('stockExchange.shareholdersTotalLabel') }}: <strong>{{ formatShares(shareholders.totalSharesIssued) }}</strong>
              </span>
              <span class="shareholders-summary__item">{{ t('stockExchange.shareholdersCountLabel', { count: shareholders.shareholderCount }) }}</span>
              <span v-if="shareholders.shareholders.length > 0" class="shareholders-summary__item">
                {{ t('stockExchange.shareholdersLargestHolder') }}: <strong>{{ shareholders.shareholders[0]?.holderName }}</strong> ({{
                  formatPercent(shareholders.shareholders[0]?.ownershipRatio ?? 0)
                }})
              </span>
            </div>
            <p v-if="shareholders.shareholders.length === 0" class="history-panel__state">{{ t('stockExchange.shareholdersEmpty') }}</p>
            <p v-else-if="shareholders.shareholders.length === 1 && shareholders.publicFloatShares === 0" class="history-panel__state shareholders-single-owner">
              {{ t('stockExchange.shareholdersSingleOwner') }}
            </p>
            <div v-if="shareholders.shareholders.length > 0" class="shareholders-layout">
              <div class="ownership-chart">
                <svg viewBox="0 0 160 160" width="160" height="160" :aria-label="t('stockExchange.shareholdersPieChartLabel')" role="img" class="ownership-donut">
                  <template v-if="pieSlices.length > 0">
                    <path v-for="(seg, idx) in donutPaths" :key="idx" :d="seg.d" :fill="seg.color" class="donut-segment" />
                  </template>
                  <circle v-else cx="80" cy="80" r="72" fill="#e0e0e0" />
                </svg>
                <ul class="ownership-legend" :aria-label="t('stockExchange.shareholdersPieChartLabel')">
                  <li v-for="(seg, idx) in pieSlices" :key="idx" class="ownership-legend__item">
                    <span class="ownership-legend__swatch" :style="{ background: seg.color }" />
                    <span class="ownership-legend__label">{{ seg.label }}</span>
                    <span class="ownership-legend__pct">{{ formatPercent(seg.ratio) }}</span>
                  </li>
                </ul>
              </div>
              <div class="shareholders-table-wrapper">
                <table class="shareholders-table" :aria-label="`${listing.companyName} ${t('stockExchange.shareholdersTitle')}`">
                  <thead>
                    <tr>
                      <th>{{ t('stockExchange.shareholdersHolder') }}</th>
                      <th>{{ t('stockExchange.shareholdersShares') }}</th>
                      <th>{{ t('stockExchange.shareholdersOwnership') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="holder in shareholders.shareholders" :key="`${holder.holderPlayerId ?? holder.holderCompanyId}`" class="shareholder-row">
                      <td>
                        <span class="holder-name">{{ holder.holderName }}</span>
                        <span class="holder-type-badge" :class="holder.holderType === 'PERSON' ? 'holder-type-badge--person' : 'holder-type-badge--company'">
                          {{ holder.holderType === 'PERSON' ? t('stockExchange.shareholdersTypePerson') : t('stockExchange.shareholdersTypeCompany') }}
                        </span>
                      </td>
                      <td>{{ formatShares(holder.shareCount) }}</td>
                      <td>
                        <div class="ownership-bar-cell">
                          <div class="ownership-bar">
                            <div class="ownership-bar__fill" :style="{ width: `${Math.min(holder.ownershipRatio * 100, 100)}%` }" />
                          </div>
                          <span class="ownership-bar__pct">{{ formatPercent(holder.ownershipRatio) }}</span>
                        </div>
                      </td>
                    </tr>
                    <tr v-if="shareholders.publicFloatShares > 0" class="shareholder-row shareholder-row--float">
                      <td>
                        <span class="holder-name">{{ t('stockExchange.shareholdersPublicFloat') }}</span>
                        <span class="holder-type-badge holder-type-badge--float">Float</span>
                      </td>
                      <td>{{ formatShares(shareholders.publicFloatShares) }}</td>
                      <td>
                        <div class="ownership-bar-cell">
                          <div class="ownership-bar ownership-bar--float">
                            <div class="ownership-bar__fill" :style="{ width: `${Math.min((shareholders.publicFloatShares / shareholders.totalSharesIssued) * 100, 100)}%` }" />
                          </div>
                          <span class="ownership-bar__pct">{{ formatPercent(shareholders.publicFloatShares / shareholders.totalSharesIssued) }}</span>
                        </div>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </template>
        </div>
      </div>
    </td>
  </tr>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CompanyOwnership, PlayerBankAccountSummary, StockExchangeListing, StockExchangePriceHistoryPoint } from '@/types'

type PieSlice = {
  label: string
  ratio: number
  color: string
  isPublicFloat?: boolean
  isOther?: boolean
}

const PIE_COLORS = ['#4e79a7', '#f28e2b', '#e15759', '#76b7b2', '#59a14f', '#edc948', '#b07aa1', '#ff9da7', '#9c755f', '#bab0ac']
const PUBLIC_FLOAT_COLOR = '#c8c8c8'
const PIE_OTHER_THRESHOLD = 0.02
const PIE_MAX_NAMED_SLICES = 8

const props = defineProps<{
  listing: StockExchangeListing
  locale: string
  showActions: boolean
  expanded: boolean
  isControlledCompany: boolean
  actionLoadingKey: string | null
  activeTradeAccountName: string
  activeTradeAccountType: string
  activeTradeAccountCash: number | null
  activeSettlementAccounts: PlayerBankAccountSummary[]
  selectedSettlementBankAccountId: string
  quantity: number
  estimatedBuyCost: number
  estimatedSellProceeds: number
  successMessage: string | null
  errorMessage: string | null
  priceHistory: StockExchangePriceHistoryPoint[]
  priceHistoryLoading: boolean
  priceHistoryError: string | null
  shareholders: CompanyOwnership | null
  shareholdersLoading: boolean
  shareholdersError: string | null
}>()

const emit = defineEmits<{
  (e: 'toggle-trade-panel'): void
  (e: 'switch-to-company'): void
  (e: 'open-merge'): void
  (e: 'update:settlement-bank-account-id', value: string): void
  (e: 'update-quantity', value: number): void
  (e: 'buy'): void
  (e: 'sell'): void
}>()

const { t } = useI18n()

const pieSlices = computed(() => (props.shareholders ? buildPieSlices(props.shareholders) : []))
const donutPaths = computed(() => buildDonuts(pieSlices.value, 80, 80, 72, 44))

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(props.locale, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value)
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

function formatShares(value: number): string {
  return new Intl.NumberFormat(props.locale, {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

function buildPieSlices(ownership: CompanyOwnership): PieSlice[] {
  if (ownership.totalSharesIssued <= 0) return []

  const slices: PieSlice[] = []
  const holders = ownership.shareholders
  let namedSliceCount = 0

  const prominentHolders = holders.filter((holder) => holder.ownershipRatio >= PIE_OTHER_THRESHOLD)
  const minorHolders = holders.filter((holder) => holder.ownershipRatio < PIE_OTHER_THRESHOLD)
  const displayHolders = prominentHolders.length > PIE_MAX_NAMED_SLICES ? prominentHolders.slice(0, PIE_MAX_NAMED_SLICES) : prominentHolders
  const overflowHolders = prominentHolders.length > PIE_MAX_NAMED_SLICES ? [...prominentHolders.slice(PIE_MAX_NAMED_SLICES), ...minorHolders] : minorHolders

  for (const holder of displayHolders) {
    slices.push({
      label: holder.holderName,
      ratio: holder.ownershipRatio,
      color: PIE_COLORS[namedSliceCount % PIE_COLORS.length] ?? '#808080',
    })
    namedSliceCount++
  }

  const otherNamedRatio = overflowHolders.reduce((sum, holder) => sum + holder.ownershipRatio, 0)
  if (otherNamedRatio > 0.0001) {
    slices.push({
      label: t('stockExchange.shareholdersOther'),
      ratio: otherNamedRatio,
      color: PIE_COLORS[namedSliceCount % PIE_COLORS.length] ?? '#808080',
      isOther: true,
    })
  }

  const floatRatio = ownership.totalSharesIssued > 0 ? ownership.publicFloatShares / ownership.totalSharesIssued : 0
  if (floatRatio > 0.0001) {
    slices.push({
      label: t('stockExchange.shareholdersPublicFloatLabel'),
      ratio: floatRatio,
      color: PUBLIC_FLOAT_COLOR,
      isPublicFloat: true,
    })
  }

  return slices
}

function buildDonuts(slices: PieSlice[], cx: number, cy: number, r: number, innerR: number) {
  const paths: { d: string; color: string; label: string; ratio: number; isPublicFloat?: boolean; isOther?: boolean }[] = []
  if (slices.length === 0) return paths

  let startAngle = -Math.PI / 2

  for (const slice of slices) {
    const sweep = slice.ratio * 2 * Math.PI
    const endAngle = startAngle + sweep
    const x1 = cx + r * Math.cos(startAngle)
    const y1 = cy + r * Math.sin(startAngle)
    const x2 = cx + r * Math.cos(endAngle)
    const y2 = cy + r * Math.sin(endAngle)
    const ix1 = cx + innerR * Math.cos(endAngle)
    const iy1 = cy + innerR * Math.sin(endAngle)
    const ix2 = cx + innerR * Math.cos(startAngle)
    const iy2 = cy + innerR * Math.sin(startAngle)
    const largeArc = sweep > Math.PI ? 1 : 0
    const d = `M ${x1} ${y1}` + ` A ${r} ${r} 0 ${largeArc} 1 ${x2} ${y2}` + ` L ${ix1} ${iy1}` + ` A ${innerR} ${innerR} 0 ${largeArc} 0 ${ix2} ${iy2}` + ` Z`

    paths.push({ d, color: slice.color, label: slice.label, ratio: slice.ratio, isPublicFloat: slice.isPublicFloat, isOther: slice.isOther })
    startAngle = endAngle
  }

  return paths
}
</script>
