<template>
  <tr class="listing-row" :class="{ 'listing-row--expanded': expanded }">
    <td class="company-cell">
      <span class="company-name">{{ listing.companyName }}</span>
      <span class="company-meta">{{ formatCompanyMeta(listing.primaryCityName, listing.primaryIndustry) }}</span>
      <span v-if="listing.canClaimControl && !isControlledCompany" class="listing-chip listing-chip--control">{{ t('stockExchange.controlReady') }}</span>
      <span v-if="listing.canMerge" class="listing-chip listing-chip--merge">{{ t('stockExchange.mergeReady') }}</span>
      <span v-else-if="listing.playerOwnedShares + listing.controlledCompanyOwnedShares > 0" class="listing-chip listing-chip--owned">{{ t('stockExchange.ownedBadge') }}</span>
    </td>
    <td class="price-cell">
      <div class="price-stack">
        <span class="price-main">{{ formatCurrency(listing.sharePrice) }}</span>
        <span class="price-change" :class="listing.dailyChangePercent >= 0 ? 'price-change--up' : 'price-change--down'">
          {{ listing.dailyChangePercent >= 0 ? '+' : '' }}{{ listing.dailyChangePercent.toFixed(2) }}%
        </span>
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
      <button v-if="listing.canClaimControl && !isControlledCompany" class="btn btn-ghost btn-sm" @click="emit('open-takeover')">
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

        <div class="dividend-governance-panel">
          <div class="history-panel__header">
            <h3>{{ t('stockExchange.dividendGovernanceTitle') }}</h3>
            <span class="history-panel__hint">{{ t('stockExchange.dividendGovernanceDesc') }}</span>
          </div>
          <p v-if="dividendProposalsLoading" class="history-panel__state">{{ t('common.loading') }}</p>
          <p v-else-if="dividendProposalsError" class="history-panel__state history-panel__state--error">{{ dividendProposalsError }}</p>
          <div v-else class="dividend-governance-content">
            <div v-if="listing.canProposeDividend" class="dividend-propose-form">
              <h4>{{ t('stockExchange.proposeDividendTitle') }}</h4>
              <div class="dividend-propose-fields">
                <label class="trade-field">
                  <span class="trade-field-label">{{ t('stockExchange.dividendPerShare') }}</span>
                  <input
                    :value="dividendPerShareDraft"
                    type="number"
                    min="0"
                    step="0.0001"
                    class="trade-input"
                    :aria-label="t('stockExchange.dividendPerShare')"
                    @input="emit('update-dividend-per-share', Number(($event.target as HTMLInputElement).value))"
                  />
                </label>
                <p class="trade-order-hint">
                  {{ t('stockExchange.dividendTotalPreview', { total: formatCurrency(dividendPerShareDraft * listing.totalSharesIssued) }) }}
                </p>
                <p class="trade-order-hint">
                  {{ t('stockExchange.dividendPersonalPreview', { total: formatCurrency(dividendPerShareDraft * listing.playerOwnedShares) }) }}
                </p>
                <button
                  class="btn btn-primary btn-sm"
                  :disabled="actionLoadingKey === `propose-dividend-${listing.companyId}`"
                  @click="emit('propose-dividend')"
                >
                  <font-awesome-icon :icon="['fas', 'coins']" />
                  <span>{{ t('stockExchange.proposeDividend') }}</span>
                </button>
              </div>
            </div>
            <p v-else class="history-panel__state">{{ t('stockExchange.dividendProposeNotEligible') }}</p>

            <div class="dividend-open-list">
              <h4>{{ t('stockExchange.openDividendProposalsTitle') }}</h4>
              <p v-if="openDividendProposals.length === 0" class="history-panel__state">{{ t('stockExchange.openDividendProposalsEmpty') }}</p>
              <article v-for="proposal in openDividendProposals" :key="proposal.id" class="dividend-proposal-card">
                <header class="dividend-proposal-header">
                  <strong>{{ t('stockExchange.dividendPerShareValue', { value: formatCurrency(proposal.dividendPerShare) }) }}</strong>
                  <span class="trade-order-hint">{{ t('stockExchange.dividendTicksRemaining', { ticks: proposal.ticksRemaining }) }}</span>
                </header>
                <p class="trade-order-hint">{{ t('stockExchange.dividendTotalPreview', { total: formatCurrency(proposal.totalPayout) }) }}</p>
                <div class="dividend-progress">
                  <div class="dividend-progress-bar dividend-progress-bar--for" :style="{ width: `${proposalSupportPercent(proposal)}%` }"></div>
                  <div class="dividend-progress-bar dividend-progress-bar--against" :style="{ width: `${proposalAgainstPercent(proposal)}%` }"></div>
                </div>
                <div class="dividend-vote-summary">
                  <span>{{ t('stockExchange.voteFor') }}: {{ formatShares(proposal.forVotes) }}</span>
                  <span>{{ t('stockExchange.voteAgainst') }}: {{ formatShares(proposal.againstVotes) }}</span>
                </div>
                <div class="dividend-vote-actions">
                  <button
                    class="btn btn-primary btn-sm"
                    :disabled="!!proposal.myVoteChoice || actionLoadingKey === `vote-dividend-${proposal.id}-FOR`"
                    @click="emit('vote-dividend', { proposalId: proposal.id, choice: 'FOR' })"
                  >
                    {{ t('stockExchange.voteFor') }}
                  </button>
                  <button
                    class="btn btn-secondary btn-sm"
                    :disabled="!!proposal.myVoteChoice || actionLoadingKey === `vote-dividend-${proposal.id}-AGAINST`"
                    @click="emit('vote-dividend', { proposalId: proposal.id, choice: 'AGAINST' })"
                  >
                    {{ t('stockExchange.voteAgainst') }}
                  </button>
                  <span v-if="proposal.myVoteChoice" class="trade-order-hint">
                    {{ t('stockExchange.myVoteLabel', { choice: proposal.myVoteChoice }) }}
                  </span>
                </div>
              </article>
            </div>

            <details class="dividend-history-collapsible">
              <summary>{{ t('stockExchange.dividendProposalHistoryTitle') }}</summary>
              <p v-if="closedDividendProposals.length === 0" class="history-panel__state">{{ t('stockExchange.dividendProposalHistoryEmpty') }}</p>
              <ul v-else class="dividend-history-list">
                <li v-for="proposal in closedDividendProposals" :key="proposal.id" class="dividend-history-item">
                  <span>{{ t('stockExchange.dividendPerShareValue', { value: formatCurrency(proposal.dividendPerShare) }) }}</span>
                  <span>{{ proposalOutcomeLabel(proposal) }}</span>
                  <span>{{ t('stockExchange.dividendSettledAtTick', { tick: proposal.settledAtTick ?? proposal.votingCloseTick }) }}</span>
                </li>
              </ul>
            </details>
          </div>
        </div>

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
import type { CompanyOwnership, DividendProposal, PlayerBankAccountSummary, StockExchangeListing, StockExchangePriceHistoryPoint } from '@/types'

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
  dividendProposals: DividendProposal[]
  dividendProposalsLoading: boolean
  dividendProposalsError: string | null
  dividendPerShareDraft: number
  shareholders: CompanyOwnership | null
  shareholdersLoading: boolean
  shareholdersError: string | null
}>()

const emit = defineEmits<{
  (e: 'toggle-trade-panel'): void
  (e: 'open-takeover'): void
  (e: 'open-merge'): void
  (e: 'update:settlement-bank-account-id', value: string): void
  (e: 'update-quantity', value: number): void
  (e: 'update-dividend-per-share', value: number): void
  (e: 'buy'): void
  (e: 'sell'): void
  (e: 'propose-dividend'): void
  (e: 'vote-dividend', payload: { proposalId: string; choice: 'FOR' | 'AGAINST' }): void
}>()

const { t } = useI18n()

const pieSlices = computed(() => (props.shareholders ? buildPieSlices(props.shareholders) : []))
const donutPaths = computed(() => buildDonuts(pieSlices.value, 80, 80, 72, 44))
const openDividendProposals = computed(() =>
  props.dividendProposals.filter((proposal) => proposal.status === 'VOTING' && proposal.ticksRemaining > 0))
const closedDividendProposals = computed(() =>
  props.dividendProposals.filter((proposal) => proposal.status !== 'VOTING' || proposal.ticksRemaining <= 0))

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

function formatCompanyMeta(city: string, industry: string): string {
  const cityLabel = city === 'UNKNOWN' ? t('stockExchange.unknownCity') : city
  const industryLabel = formatIndustryLabel(industry)
  return t('stockExchange.companyMeta', { city: cityLabel, industry: industryLabel })
}

function formatIndustryLabel(industry: string): string {
  if (industry === 'DIVERSIFIED') {
    return t('stockExchange.diversifiedIndustry')
  }

  return industry
    .split('_')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ')
}

function proposalSupportPercent(proposal: DividendProposal): number {
  const total = proposal.forVotes + proposal.againstVotes
  if (total <= 0) {
    return 0
  }

  return (proposal.forVotes / total) * 100
}

function proposalAgainstPercent(proposal: DividendProposal): number {
  const total = proposal.forVotes + proposal.againstVotes
  if (total <= 0) {
    return 0
  }

  return (proposal.againstVotes / total) * 100
}

function proposalOutcomeLabel(proposal: DividendProposal): string {
  if (proposal.outcome === 'APPROVED') {
    return t('stockExchange.dividendOutcomeApproved')
  }

  return t('stockExchange.dividendOutcomeRejected')
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

<style scoped>
/* ── Listing row ─────────────────────────────────────── */
.listing-row {
  transition: background-color 0.15s ease;
}

.listing-row:hover {
  background: color-mix(in srgb, var(--color-primary) 4%, transparent);
}

.listing-row--expanded {
  background: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));
}

.listing-row--expanded td {
  border-bottom-color: transparent;
}

.company-cell {
  display: flex !important;
  flex-direction: column;
  gap: 0.3rem;
  white-space: normal !important;
}

.company-name {
  font-weight: 600;
}

.company-meta {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.price-stack {
  display: grid;
  gap: 0.15rem;
}

.price-main {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.price-change {
  font-size: 0.75rem;
  font-weight: 700;
}

.price-change--up {
  color: #10b981;
}

.price-change--down {
  color: #ef4444;
}

.price-meta {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.ownership-cell {
  display: grid;
  gap: 0.15rem;
}

.owned-shares-hint {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  white-space: normal;
}

.listing-chip {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  white-space: nowrap;
}

.listing-chip--control {
  background: color-mix(in srgb, #f59e0b 18%, transparent);
  color: #b45309;
}

.listing-chip--owned {
  background: color-mix(in srgb, var(--color-primary) 16%, transparent);
  color: var(--color-primary);
}

.listing-chip--merge {
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 18%, var(--color-surface));
  color: var(--color-warning, #f59e0b);
  border: 1px solid color-mix(in srgb, var(--color-warning, #f59e0b) 40%, transparent);
}

.actions-cell {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  white-space: nowrap;
}

.btn-sm {
  padding: 0.4rem 0.85rem;
  font-size: 0.82rem;
}

.btn-active {
  background: color-mix(in srgb, var(--color-primary) 20%, var(--color-surface));
}

.dividend-cell {
  white-space: nowrap;
}

.dividend-badge {
  display: inline-block;
  background: color-mix(in srgb, var(--color-success, #22c55e) 14%, transparent);
  color: var(--color-success, #22c55e);
  padding: 0.2rem 0.55rem;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

/* ── Trade panel ─────────────────────────────────────── */
.trade-panel-row td {
  padding: 0 !important;
  border-bottom: 1px solid var(--color-border);
}

.trade-panel {
  padding: 1.1rem 1rem;
  background: color-mix(in srgb, var(--color-primary) 4%, var(--color-surface));
  display: grid;
  gap: 1rem;
}

.trade-price-context {
  display: flex;
  gap: 2rem;
  flex-wrap: wrap;
}

.trade-price-item {
  display: grid;
  gap: 0.15rem;
}

.trade-price-label,
.trade-price-hint {
  color: var(--color-text-secondary);
}

.trade-price-label {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.trade-price-value {
  font-size: 1.15rem;
  font-variant-numeric: tabular-nums;
}

.trade-price-ask {
  color: var(--color-danger, #ef4444);
}

.trade-price-bid {
  color: var(--color-success, #22c55e);
}

.trade-price-hint {
  font-size: 0.75rem;
}

.trade-form {
  display: grid;
  gap: 1rem;
}

.trade-order-panel {
  display: grid;
  gap: 1rem;
  padding: 1rem;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-surface) 88%, var(--color-primary) 12%);
}

.trade-order-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.trade-order-context,
.trade-actions-header {
  display: grid;
  gap: 0.25rem;
}

.trade-order-caption,
.trade-actions-caption {
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-secondary);
}

.trade-order-name {
  font-size: 1rem;
  color: var(--color-text-primary);
}

.trade-order-hint,
.trade-actions-hint {
  margin: 0;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.trade-account-cash {
  display: inline-flex;
  align-items: center;
  padding: 0.35rem 0.7rem;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-primary) 12%, var(--color-surface));
  color: var(--color-text-primary);
  font-size: 0.82rem;
  font-variant-numeric: tabular-nums;
  font-weight: 700;
}

.trade-order-controls {
  display: grid;
  gap: 0.35rem 0;
}

.trade-controls-labels,
.trade-controls-inputs,
.trade-controls-estimates {
  display: grid;
  grid-template-columns: minmax(130px, 160px) 1fr 1fr;
  gap: 0 0.75rem;
}

.trade-controls-labels {
  align-items: end;
}

.trade-controls-inputs {
  align-items: stretch;
}

.trade-controls-estimates {
  align-items: start;
}

.trade-field {
  display: grid;
  gap: 0.35rem;
}

.trade-field-label {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.trade-action-btn {
  width: 100%;
  justify-content: center;
}

.trade-select,
.trade-input {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-background);
  color: var(--color-text);
  padding: 0.6rem 0.8rem;
  font-size: 0.9rem;
}

.trade-select {
  min-width: 200px;
}

.trade-input {
  width: 100%;
}

.trade-actions-card {
  display: grid;
  gap: 0.75rem;
}

.trade-actions {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  align-items: start;
}

.trade-action-group {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  align-items: stretch;
}

.trade-action-group .btn {
  width: 100%;
  justify-content: center;
}

.trade-est {
  font-size: 0.78rem;
  color: var(--color-text-muted, var(--color-text-secondary));
  font-variant-numeric: tabular-nums;
}

.trade-feedback {
  margin: 0;
  padding: 0.65rem 0.9rem;
  border-radius: 10px;
  font-size: 0.88rem;
}

.dividend-governance-panel {
  display: grid;
  gap: 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  padding: 0.9rem;
  background: color-mix(in srgb, var(--color-surface) 90%, var(--color-primary) 10%);
}

.dividend-governance-content {
  display: grid;
  gap: 0.8rem;
}

.dividend-propose-form h4,
.dividend-open-list h4 {
  margin: 0 0 0.4rem;
}

.dividend-propose-fields {
  display: grid;
  gap: 0.5rem;
  max-width: 380px;
}

.dividend-proposal-card {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 0.65rem 0.75rem;
  display: grid;
  gap: 0.5rem;
}

.dividend-proposal-header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
}

.dividend-progress {
  display: flex;
  width: 100%;
  height: 12px;
  border-radius: 999px;
  overflow: hidden;
  background: color-mix(in srgb, var(--color-border) 65%, transparent);
}

.dividend-progress-bar {
  transition: width 0.25s ease;
}

.dividend-progress-bar--for {
  background: color-mix(in srgb, var(--color-success, #22c55e) 75%, #ffffff 0%);
}

.dividend-progress-bar--against {
  background: color-mix(in srgb, var(--color-danger, #ef4444) 75%, #ffffff 0%);
}

.dividend-vote-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.dividend-vote-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}

.dividend-history-collapsible summary {
  cursor: pointer;
  font-weight: 600;
}

.dividend-history-list {
  margin: 0.6rem 0 0;
  padding: 0;
  list-style: none;
  display: grid;
  gap: 0.35rem;
}

.dividend-history-item {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.trade-feedback--success {
  background: color-mix(in srgb, var(--color-success, #22c55e) 14%, var(--color-surface));
  color: var(--color-success, #22c55e);
}

.trade-feedback--error {
  background: color-mix(in srgb, var(--color-danger, #ef4444) 14%, var(--color-surface));
  color: var(--color-danger, #ef4444);
}

/* ── Company snapshot ────────────────────────────────── */
.company-snapshot {
  background: var(--color-surface-secondary, var(--color-surface));
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 0.75rem 1rem;
  margin-bottom: 0.75rem;
}

.snapshot-grid {
  display: flex;
  gap: 2rem;
  flex-wrap: wrap;
  margin: 0;
}

.snapshot-item {
  display: grid;
  gap: 0.15rem;
}

.snapshot-item dt {
  font-size: 0.72rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-text-muted, var(--color-text-secondary));
}

.snapshot-item dd {
  margin: 0;
  font-size: 0.95rem;
  font-variant-numeric: tabular-nums;
}

.snapshot-pct {
  font-size: 0.78rem;
  color: var(--color-text-muted, var(--color-text-secondary));
  margin-left: 0.25rem;
}

/* ── Price history panel ─────────────────────────────── */
.history-panel {
  display: grid;
  gap: 0.6rem;
  padding-top: 0.25rem;
  border-top: 1px solid var(--color-border);
}

.history-panel__header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.history-panel__header h3 {
  margin: 0;
  font-size: 0.95rem;
}

.history-panel__hint,
.history-panel__state {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.history-panel__state--error {
  color: var(--color-danger, #ef4444);
}

.history-table-wrapper {
  overflow-x: auto;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
}

.history-table th,
.history-table td {
  padding: 0.5rem 0.35rem;
  border-bottom: 1px solid var(--color-border);
  text-align: left;
  white-space: nowrap;
}

/* ── Shareholders panel ──────────────────────────────── */
.shareholders-panel {
  display: grid;
  gap: 0.6rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--color-border);
  margin-top: 0.5rem;
}

.shareholders-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1.25rem;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.shareholders-summary__item strong {
  color: var(--color-text-primary);
}

.shareholders-single-owner {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
  font-style: italic;
}

.shareholders-layout {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
  align-items: flex-start;
}

.ownership-chart {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  min-width: 160px;
}

.ownership-donut {
  flex-shrink: 0;
}

.donut-segment {
  transition: opacity 0.15s;
}

.donut-segment:hover {
  opacity: 0.82;
}

.ownership-legend {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  font-size: 0.78rem;
  min-width: 140px;
}

.ownership-legend__item {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.ownership-legend__swatch {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 2px;
  flex-shrink: 0;
}

.ownership-legend__label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text-primary);
}

.ownership-legend__pct {
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  flex-shrink: 0;
}

.shareholders-table-wrapper {
  overflow-x: auto;
  flex: 1;
  min-width: 0;
}

.shareholders-table {
  border-collapse: collapse;
  width: 100%;
  font-size: 0.82rem;
}

.shareholders-table th {
  text-align: left;
  padding: 0.3rem 0.5rem;
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-weight: 600;
  white-space: nowrap;
}

.shareholders-table td {
  padding: 0.35rem 0.5rem;
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 60%, transparent);
  vertical-align: middle;
}

.shareholder-row--float td {
  color: var(--color-text-secondary);
  font-style: italic;
}

.holder-name {
  margin-right: 0.35rem;
  color: var(--color-text-primary);
}

.holder-type-badge {
  display: inline-block;
  font-size: 0.68rem;
  font-weight: 600;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  vertical-align: middle;
}

.holder-type-badge--person {
  background: color-mix(in srgb, var(--color-accent, #6366f1) 16%, var(--color-surface));
  color: var(--color-accent, #6366f1);
}

.holder-type-badge--company {
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 16%, var(--color-surface));
  color: color-mix(in srgb, var(--color-warning, #f59e0b) 80%, var(--color-text-primary));
}

.holder-type-badge--float {
  background: color-mix(in srgb, var(--color-text-secondary) 12%, var(--color-surface));
  color: var(--color-text-secondary);
}

.ownership-bar-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.ownership-bar {
  height: 8px;
  background: color-mix(in srgb, var(--color-accent, #6366f1) 18%, var(--color-surface));
  border-radius: 4px;
  flex: 1;
  min-width: 60px;
  overflow: hidden;
}

.ownership-bar--float {
  background: color-mix(in srgb, var(--color-text-secondary) 14%, var(--color-surface));
}

.ownership-bar__fill {
  height: 100%;
  background: var(--color-accent, #6366f1);
  border-radius: 4px;
  transition: width 0.3s;
}

.ownership-bar--float .ownership-bar__fill {
  background: var(--color-text-secondary);
}

.ownership-bar__pct {
  font-size: 0.78rem;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-secondary);
  white-space: nowrap;
  min-width: 3.5rem;
  text-align: right;
}

/* ── Mobile ──────────────────────────────────────────── */
@media (max-width: 720px) {
  .trade-price-context {
    gap: 1rem;
  }

  .trade-order-controls {
    gap: 0.5rem 0;
  }

  .trade-controls-labels {
    display: none;
  }

  .trade-controls-inputs,
  .trade-controls-estimates {
    grid-template-columns: 1fr 1fr;
  }

  .trade-controls-inputs .trade-input {
    grid-column: 1 / -1;
  }

  .trade-controls-estimates > :first-child {
    display: none;
  }

  .trade-actions {
    grid-template-columns: 1fr;
  }

  .dividend-proposal-header,
  .dividend-vote-actions,
  .dividend-history-item {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
