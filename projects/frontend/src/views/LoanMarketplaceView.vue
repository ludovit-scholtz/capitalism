<template>
  <main class="loan-marketplace-view container mx-auto px-4 pb-16 pt-6 sm:px-6 lg:px-8 lg:pb-20 lg:pt-8">
    <div class="flex flex-col">
      <div class="page-header flex flex-col gap-3">
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ t('bank.banks') }}</h1>
        <p class="page-subtitle max-w-3xl text-sm text-muted sm:text-base">{{ t('bank.browseBanks') }}</p>
      </div>

      <!-- Tab switcher -->
      <div class="marketplace-tabs flex flex-wrap gap-2 border-b border-divider pb-1" role="tablist">
        <button
          role="tab"
          :aria-selected="activeTab === 'borrow'"
          :class="[
            'tab-btn inline-flex items-center gap-2 rounded-t-2xl border-b-2 px-5 py-3 text-sm font-semibold transition-colors',
            activeTab === 'borrow' ? 'tab-active border-brand text-brand' : 'border-transparent text-muted hover:text-body',
          ]"
          @click="activeTab = 'borrow'"
        >
          {{ t('bank.borrowTab') }}
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'deposit'"
          :class="[
            'tab-btn inline-flex items-center gap-2 rounded-t-2xl border-b-2 px-5 py-3 text-sm font-semibold transition-colors',
            activeTab === 'deposit' ? 'tab-active border-brand text-brand' : 'border-transparent text-muted hover:text-body',
          ]"
          @click="activeTab = 'deposit'"
        >
          {{ t('bank.depositTab') }}
          <span v-if="myDeposits.length > 0" class="tab-badge inline-flex min-w-5 items-center justify-center rounded-full bg-brand px-2 py-0.5 text-xs font-bold text-white">{{
            myDeposits.length
          }}</span>
        </button>
      </div>

      <div v-if="loading" class="loading-state">
        <div class="spinner" />
        <span>{{ t('common.loading') }}</span>
      </div>

      <div v-else-if="error" class="error-state">
        <p class="error-message">{{ error }}</p>
        <button class="btn btn-secondary" @click="() => loadData()">{{ t('common.retry') }}</button>
      </div>

      <template v-else>
        <!-- ÔöÇÔöÇ BORROW TAB ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
        <div v-if="activeTab === 'borrow'" class="flex flex-col gap-10 lg:gap-12">
          <!-- Lender action panel: context-aware CTA for offering loans -->
          <section class="lender-cta-section flex flex-col gap-6" aria-label="Lender action">
            <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.becomeALender') }}</h2>

            <!-- Unauthenticated: prompt login -->
            <div
              v-if="!auth.isAuthenticated"
              class="lender-cta-card lender-cta-login flex flex-col gap-5 rounded-3xl border border-divider bg-card p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8"
            >
              <div class="lender-cta-icon" aria-hidden="true">­čĆŽ</div>
              <div class="lender-cta-body">
                <h3 class="lender-cta-title">{{ t('bank.loginToLendTitle') }}</h3>
                <p class="lender-cta-description">{{ t('bank.loginToLendDescription') }}</p>
              </div>
              <router-link to="/login" class="btn btn-secondary lender-cta-btn" aria-label="Log in to offer loans"> {{ t('bank.loginToLend') }} </router-link>
            </div>

            <!-- Authenticated, no bank building: acquire CTA -->
            <div
              v-else-if="!hasBankBuilding"
              class="lender-cta-card lender-cta-acquire flex flex-col gap-5 rounded-3xl border border-divider bg-card p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8"
            >
              <div class="lender-cta-icon" aria-hidden="true">­čĆŽ</div>
              <div class="lender-cta-body">
                <h3 class="lender-cta-title">{{ t('bank.noBankCTATitle') }}</h3>
                <p class="lender-cta-description">{{ t('bank.noBankCTADescription') }}</p>
              </div>
              <button class="btn btn-primary lender-cta-btn" @click="navigateToAcquireBank" aria-label="Acquire a Bank building">{{ t('bank.acquireBank') }}</button>
            </div>

            <!-- Authenticated, has bank: manage bank CTA -->
            <div v-else class="lender-cta-card lender-cta-manage flex flex-col gap-5 rounded-3xl border border-brand/40 bg-card p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8">
              <div class="lender-cta-icon" aria-hidden="true">­čĆŽ</div>
              <div class="lender-cta-body">
                <h3 class="lender-cta-title">{{ t('bank.hasBankCTATitle') }}</h3>
                <p class="lender-cta-description">{{ t('bank.hasBankCTADescription') }}</p>
                <span class="lender-bank-name">{{ firstBankBuilding?.name }}</span>
              </div>
              <button class="btn btn-primary lender-cta-btn" @click="navigateToManageBank">{{ t('bank.manageBank') }}</button>
            </div>
          </section>

          <!-- Active loans section (authenticated borrowers) -->
          <section v-if="auth.isAuthenticated && activeLoans.length > 0" class="my-loans-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.myLoans') }}</h2>
            <div class="loans-grid mt-6 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
              <div v-for="loan in activeLoans" :key="loan.id" class="loan-card rounded-2xl border border-divider bg-card-raised p-5 shadow-sm" :class="loanStatusClass(loan.status)">
                <div class="loan-card-header">
                  <span class="lender-name">{{ loan.lenderCompanyName }}</span>
                  <span class="loan-status-badge" :class="loanStatusClass(loan.status)"> {{ t(`bank.statusBadge.${loan.status}`) }} </span>
                </div>
                <div class="loan-card-body">
                  <div class="loan-stat">
                    <span class="stat-label">{{ t('bank.remainingPrincipal') }}</span>
                    <span class="stat-value">{{ formatCurrency(loan.remainingPrincipal) }}</span>
                  </div>
                  <div class="loan-stat">
                    <span class="stat-label">{{ t('bank.nextPayment') }}</span>
                    <span class="stat-value">{{ formatCurrency(loan.paymentAmount) }}</span>
                  </div>
                  <div class="loan-stat">
                    <span class="stat-label">{{ t('bank.paymentsMade') }}</span>
                    <span class="stat-value">{{ loan.paymentsMade }} / {{ loan.totalPayments }}</span>
                  </div>
                  <div class="loan-stat">
                    <span class="stat-label">{{ t('bank.interestRate') }}</span>
                    <span class="stat-value">{{ formatPercent(loan.annualInterestRatePercent) }}</span>
                  </div>
                </div>
                <div v-if="loan.missedPayments > 0" class="overdue-warning">ÔÜá {{ loan.missedPayments }} missed payment(s) ÔÇö penalty accumulated: {{ formatCurrency(loan.accumulatedPenalty) }}</div>
                <div v-if="loan.collateralBuildingId" class="collateral-badge">
                  ­čĆŤ {{ t('bank.securedLoan') }}: {{ loan.collateralBuildingName }}
                  <span v-if="loan.collateralAppraisedValue" class="collateral-badge-value"> ({{ t('bank.collateralAppraisedValue') }}: {{ formatCurrency(loan.collateralAppraisedValue) }}) </span>
                </div>
              </div>
            </div>
          </section>

          <!-- Bank discovery section for borrowers: choose a bank first, then create loan on bank page -->
          <section class="offers-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <div class="flex flex-col gap-3">
              <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.chooseBankToBorrow') }}</h2>
              <p class="section-subtitle max-w-3xl text-sm text-muted sm:text-base">{{ t('bank.chooseBankToBorrowHint') }}</p>
            </div>
            <div v-if="sortedBanksForBorrow.length === 0" class="empty-state">
              <p>{{ t('bank.noBanksAvailable') }}</p>
            </div>
            <div v-else class="banks-for-borrow-grid mt-6 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
              <div v-for="bank in sortedBanksForBorrow" :key="bank.bankBuildingId" class="bank-borrow-card flex flex-col gap-4 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
                <div class="bank-borrow-card-header">
                  <div class="bank-borrow-identity">
                    <span class="bank-borrow-icon">­čĆŽ</span>
                    <div>
                      <span class="bank-borrow-name">{{ bank.bankBuildingName }}</span>
                      <span class="bank-borrow-lender">{{ bank.lenderCompanyName }}</span>
                    </div>
                  </div>
                  <div class="bank-borrow-rate">
                    <span class="rate-value">{{ formatPercent(bank.lendingInterestRatePercent) }}</span>
                    <span class="rate-label">{{ t('bank.perYear') }}</span>
                  </div>
                </div>
                <div class="bank-borrow-stats">
                  <div class="borrow-stat">
                    <span class="stat-label">{{ t('bank.availableCapacity') }}</span>
                    <span class="stat-value" :class="bank.availableLendingCapacity > 0 ? '' : 'stat-zero'"> {{ formatCurrency(bank.availableLendingCapacity) }} </span>
                  </div>
                  <div class="borrow-stat">
                    <span class="stat-label">{{ t('common.city') }}</span>
                    <span class="stat-value">{{ bank.cityName }}</span>
                  </div>
                </div>
                <div class="bank-borrow-card-footer">
                  <router-link :to="`/bank/${bank.bankBuildingId}`" class="btn btn-primary btn-sm"> {{ t('bank.visitBankToBorrow') }} </router-link>
                </div>
              </div>
            </div>
          </section>
        </div>
        <!-- end borrow tab -->

        <!-- ÔöÇÔöÇ DEPOSIT TAB ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ -->
        <div v-if="activeTab === 'deposit'" class="deposit-tab flex flex-col gap-8 lg:gap-10">
          <section v-if="auth.isAuthenticated" class="my-bank-accounts-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.myBankAccounts') }}</h2>
            <div class="deposits-list mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              <div v-for="account in visibleBankAccounts" :key="account.id" class="deposit-card rounded-2xl border border-divider bg-card-raised p-5 shadow-sm" data-testid="bank-account-row">
                <div class="deposit-card-header">
                  <span class="deposit-bank-name">{{ account.ownerDisplayName }}</span>
                  <span class="deposit-rate-badge">{{ account.currencyCode }}</span>
                </div>
                <div class="deposit-stats">
                  <div class="deposit-stat">
                    <span class="deposit-stat-label">{{ t('bank.accountNumber') }}</span>
                    <span class="deposit-stat-value">{{ account.accountNumber }}</span>
                  </div>
                  <div class="deposit-stat">
                    <span class="deposit-stat-label">{{ t('bank.accountBalance') }}</span>
                    <span class="deposit-stat-value">{{ formatCurrency(account.balance, account.currencyCode) }}</span>
                  </div>
                </div>
                <!-- Zero-balance ready-to-close indicator (non-deposit accounts only) -->
                <p v-if="account.balance == 0 && !account.isDepositAccount" class="account-ready-close mt-2 text-xs font-medium text-success">Ôťô {{ t('bank.accountReadyToClose') }}</p>
                <!-- Non-zero balance hint -->
                <p v-else-if="account.balance != 0 && !account.isDepositAccount" class="account-nonzero-hint mt-2 text-xs text-muted">{{ t('bank.closeAccountNonZeroHint') }}</p>
                <!-- Close error feedback -->
                <p v-if="closeAccountErrors[account.id]" class="close-account-error mt-2 text-xs text-error" role="alert">{{ closeAccountErrors[account.id] }}</p>
                <!-- Close button (non-deposit accounts with zero balance only) -->
                <button
                  v-if="account.balance == 0 && !account.isDepositAccount"
                  class="btn btn-danger btn-sm mt-3"
                  :disabled="closingAccountId === account.id"
                  @click="closeBankAccount(account.id, false)"
                >
                  {{ closingAccountId === account.id ? 'ÔÇŽ' : t('bank.closeAccount') }}
                </button>
                <!-- Deposit account zero-balance close (existing flow) -->
                <button
                  v-else-if="account.balance == 0 && account.isDepositAccount"
                  class="btn btn-danger btn-sm mt-3"
                  :disabled="closingAccountId === account.id"
                  @click="closeBankAccount(account.id, true)"
                >
                  {{ closingAccountId === account.id ? 'ÔÇŽ' : t('bank.closeAccount') }}
                </button>
              </div>
            </div>
          </section>

          <!-- Banks List -->
          <section class="banks-list-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.allBanks') }}</h2>

            <div v-if="allBanks.length === 0" class="empty-state">
              <p>{{ t('bank.noBanksAvailable') }}</p>
            </div>

            <template v-else>
              <!-- Sort/filter controls -->
              <div class="banks-controls mt-6 flex flex-col gap-5 rounded-2xl border border-divider bg-card-raised p-5 lg:flex-row lg:items-end lg:justify-between">
                <div class="banks-filter flex flex-col gap-4 lg:flex-row lg:flex-wrap lg:items-end">
                  <label class="filter-label" for="city-filter">{{ t('bank.cityFilter') }}</label>
                  <select id="city-filter" v-model="bankCityFilter" class="filter-select rounded-xl border border-divider bg-card px-4 py-3 text-sm text-body">
                    <option value="">{{ t('common.city') }}: All</option>
                    <option v-for="city in availableBankCities" :key="city" :value="city">{{ city }}</option>
                  </select>
                  <label class="filter-check inline-flex items-center gap-3 rounded-xl border border-divider px-4 py-3 text-sm text-body">
                    <input v-model="bankShowAvailableOnly" type="checkbox" />
                    {{ t('bank.showAvailableOnly') }}
                  </label>
                </div>
                <div class="banks-sort flex flex-wrap items-center gap-3" role="group" :aria-label="t('bank.sortBy')">
                  <span class="sort-label text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('bank.sortBy') }}</span>
                  <button
                    v-for="field in bankSortFields"
                    :key="field"
                    class="sort-btn inline-flex items-center gap-2 rounded-full border border-divider px-4 py-2 text-sm font-medium text-body transition-colors hover:border-brand hover:text-brand"
                    :class="{ 'sort-active bg-card text-brand border-brand': bankSortBy === field }"
                    @click="toggleBankSort(field)"
                  >
                    <span v-if="field === 'depositRate'">{{ t('bank.depositInterestRate') }}</span>
                    <span v-else-if="field === 'lendingRate'">{{ t('bank.lendingInterestRate') }}</span>
                    <span v-else-if="field === 'capacity'">{{ t('bank.availableLendingCapacity') }}</span>
                    <span v-else>{{ t('common.city') }}</span>
                    <span v-if="bankSortBy === field" class="sort-dir-icon" aria-hidden="true"> {{ bankSortDir === 'asc' ? 'ÔćĹ' : 'Ôćô' }} </span>
                  </button>
                </div>
              </div>

              <div v-if="filteredAndSortedBanks.length === 0" class="empty-state">
                <p>{{ t('bank.noBanksAvailable') }}</p>
              </div>

              <div v-else class="banks-grid mt-6 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
                <div v-for="bank in filteredAndSortedBanks" :key="bank.bankBuildingId" class="bank-card flex flex-col gap-5 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
                  <div class="bank-card-header">
                    <div>
                      <h3 class="bank-card-name">{{ bank.bankBuildingName }}</h3>
                      <span class="bank-card-city">{{ bank.cityName }} ┬Ě {{ bank.lenderCompanyName }}</span>
                    </div>
                  </div>
                  <div class="bank-card-rates">
                    <div class="bank-rate deposit-rate">
                      <span class="rate-label">{{ t('bank.depositInterestRate') }}</span>
                      <span class="rate-value green">{{ formatPercent(bank.depositInterestRatePercent) }}</span>
                    </div>
                    <div class="bank-rate lending-rate">
                      <span class="rate-label">{{ t('bank.lendingInterestRate') }}</span>
                      <span class="rate-value orange">{{ formatPercent(bank.lendingInterestRatePercent) }}</span>
                    </div>
                  </div>
                  <div class="bank-card-capacity">
                    <span class="capacity-label">{{ t('bank.availableLendingCapacity') }}</span>
                    <span class="capacity-value" :class="bank.availableLendingCapacity > 0 ? 'positive' : 'zero'"> {{ formatCurrency(bank.availableLendingCapacity) }} </span>
                  </div>
                  <div class="bank-card-actions">
                    <button class="btn btn-secondary btn-sm" @click="navigateToBank(bank.bankBuildingId)">{{ t('bank.viewBankDetail') }}</button>
                    <button v-if="auth.isAuthenticated" class="btn btn-primary btn-sm bank-deposit-btn" @click="openDepositModal(bank)">{{ t('bank.makeDeposit') }}</button>
                    <router-link v-else-if="!auth.isAuthenticated" to="/login" class="btn btn-primary btn-sm"> {{ t('auth.login') }} </router-link>
                  </div>
                </div>
              </div>
            </template>
          </section>
        </div>
        <!-- end deposit tab -->
      </template>

      <!-- Accept Loan Modal -->
      <div v-if="showAcceptModal && selectedOffer" class="modal-overlay" @click.self="closeAcceptModal">
        <div class="modal" role="dialog" :aria-label="t('bank.confirmAccept')">
          <div class="modal-header">
            <h2>{{ t('bank.confirmAccept') }}</h2>
            <button class="modal-close" @click="closeAcceptModal" :aria-label="t('common.close')">ÔťĽ</button>
          </div>
          <div class="modal-body">
            <div class="loan-summary">
              <div class="summary-row">
                <span>{{ t('bank.lender') }}</span>
                <strong>{{ selectedOffer.lenderCompanyName }}</strong>
              </div>
              <div class="summary-row">
                <span>{{ t('bank.interestRate') }}</span>
                <strong>{{ formatPercent(selectedOffer.annualInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
              </div>
              <div class="summary-row">
                <span>{{ t('bank.duration') }}</span>
                <strong>{{ formatLoanDuration(selectedOffer.durationTicks) }}</strong>
              </div>
            </div>

            <div class="form-group">
              <label for="borrow-company">{{ t('bank.borrower') }}</label>
              <div id="borrow-company" class="active-borrower-company">
                <strong>{{ activeCompany?.name ?? t('bank.activeBorrowerCompany') }}</strong>
                <span>{{ activeCompany ? formatCurrency(activeCompany.cash) : '' }}</span>
              </div>
              <span class="form-hint">{{ t('bank.borrowerHint') }}</span>
            </div>

            <div class="form-group">
              <label for="principal-amount">{{ t('bank.principalAmount') }}</label>
              <input
                id="principal-amount"
                v-model.number="principalAmount"
                type="number"
                :min="1000"
                :max="Math.min(selectedOffer.maxPrincipalPerLoan, selectedOffer.remainingCapacity)"
                step="1000"
                class="form-input"
              />
              <span class="form-hint">{{ t('bank.companyCashAvailable', { amount: formatCurrency(selectedCompanyCash) }) }}</span>
            </div>

            <div class="repayment-summary">
              <div class="summary-row">
                <span>{{ t('bank.originalPrincipal') }}</span>
                <strong>{{ formatCurrency(principalAmount) }}</strong>
              </div>
              <div class="summary-row">
                <span>{{ t('bank.paymentAmount') }}</span>
                <strong>{{ formatCurrency(estimatedPaymentAmount) }} ├Ś {{ estimatedTotalPayments }}</strong>
              </div>
              <div class="summary-row total-row">
                <span>{{ t('bank.totalRepayment') }}</span>
                <strong>{{ formatCurrency(estimatedTotalRepayment) }}</strong>
              </div>
            </div>

            <!-- Collateral selection -->
            <div class="form-group collateral-group">
              <label>{{ t('bank.collateralOptional') }}</label>
              <p class="form-hint">{{ t('bank.collateralHint') }}</p>
              <div v-if="collateralLoadError" class="form-hint error-inline">{{ collateralLoadError }}</div>
              <div v-else-if="collateralBuildings.length === 0" class="form-hint muted-hint">{{ t('bank.noBuildingsForCollateral') }}</div>
              <div v-else class="collateral-list">
                <!-- None option -->
                <label class="collateral-option" :class="{ selected: selectedCollateralBuildingId === null }">
                  <input type="radio" :value="null" v-model="selectedCollateralBuildingId" class="collateral-radio" />
                  <span class="collateral-option-name">{{ t('bank.collateralNone') }}</span>
                </label>
                <!-- Buildings -->
                <label v-for="b in collateralBuildings" :key="b.buildingId" class="collateral-option" :class="{ selected: selectedCollateralBuildingId === b.buildingId, ineligible: !b.isEligible }">
                  <input type="radio" :value="b.buildingId" v-model="selectedCollateralBuildingId" :disabled="!b.isEligible" class="collateral-radio" />
                  <span class="collateral-option-body">
                    <span class="collateral-option-name">{{ b.buildingName }}</span>
                    <span class="collateral-option-type">{{ b.buildingType }} ┬Ě Lv{{ b.level }}</span>
                    <span v-if="!b.isEligible" class="collateral-tag ineligible-tag">{{ t('bank.collateralAlreadyPledged') }}</span>
                    <span v-else class="collateral-stats">
                      <span>{{ t('bank.collateralAppraisedValue') }}: {{ formatCurrency(b.appraisedValue) }}</span>
                      <span class="stat-highlight">{{ t('bank.collateralMaxBorrowable') }}: {{ formatCurrency(b.maxBorrowable) }}</span>
                      <span v-if="b.existingSecuredExposure > 0" class="stat-warn"> {{ t('bank.collateralExistingExposure') }}: {{ formatCurrency(b.existingSecuredExposure) }} </span>
                      <span class="stat-capacity">{{ t('bank.collateralRemainingCapacity') }}: {{ formatCurrency(b.remainingBorrowingCapacity) }}</span>
                    </span>
                  </span>
                </label>
              </div>
              <!-- Collateral-specific warning -->
              <p v-if="collateralRequiredWarning" class="risk-warning collateral-warning">ÔÜá {{ collateralRequiredWarning }}</p>
              <p v-if="collateralCapacityWarning" class="risk-warning collateral-warning">ÔÜá {{ collateralCapacityWarning }}</p>
              <!-- Selected collateral summary -->
              <div v-if="selectedCollateral" class="collateral-selected-summary">
                <span class="collateral-selected-label">{{ t('bank.collateralBuilding') }}:</span>
                <strong>{{ selectedCollateral.buildingName }}</strong>
                <span class="capacity-bar-wrap">
                  <span
                    class="capacity-bar-fill"
                    :style="{ width: Math.min(100, (principalAmount / selectedCollateral.maxBorrowable) * 100).toFixed(1) + '%' }"
                    :class="{ 'capacity-bar-danger': principalAmount > selectedCollateral.remainingBorrowingCapacity }"
                  ></span>
                </span>
                <span class="capacity-bar-label">
                  {{ formatCurrency(principalAmount) }} / {{ formatCurrency(selectedCollateral.maxBorrowable) }} ({{
                    Math.min(100, Math.round((principalAmount / selectedCollateral.maxBorrowable) * 100))
                  }}% LTV)
                </span>
              </div>
            </div>

            <p class="risk-warning">ÔÜá {{ t('bank.riskWarning') }}</p>

            <div v-if="acceptError" class="error-message">{{ acceptError }}</div>
          </div>
          <div class="modal-footer">
            <button class="btn btn-secondary" @click="closeAcceptModal">{{ t('common.cancel') }}</button>
            <button class="btn btn-primary" :disabled="acceptLoading || principalAmount <= 0 || !!collateralRequiredWarning || !!collateralCapacityWarning" @click="confirmAcceptLoan">
              <span v-if="acceptLoading">{{ t('common.loading') }}</span>
              <span v-else>{{ t('bank.acceptLoan') }}</span>
            </button>
          </div>
        </div>
      </div>

      <!-- Deposit Modal -->
      <div v-if="showDepositModal && selectedBank" class="modal-overlay fixed inset-0 z-1000 flex items-center justify-center bg-black/60 p-4" @click.self="closeDepositModal">
        <div class="modal w-full max-w-xl overflow-y-auto rounded-[28px] border border-divider bg-card shadow-2xl" role="dialog" :aria-label="t('bank.makeDeposit')">
          <div class="modal-header flex items-center justify-between border-b border-divider px-6 py-5 sm:px-8 sm:py-6">
            <h2 class="text-2xl font-bold text-body">{{ t('bank.makeDeposit') }}</h2>
            <button class="modal-close" :aria-label="t('common.close')" @click="closeDepositModal">ÔťĽ</button>
          </div>
          <div class="modal-body flex flex-col gap-6 px-6 py-6 sm:px-8 sm:py-8">
            <div class="loan-summary rounded-2xl border border-divider bg-card-raised p-5">
              <div class="summary-row flex items-center justify-between gap-4 py-1.5 text-sm">
                <span>Bank</span>
                <strong>{{ selectedBank.bankBuildingName }}</strong>
              </div>
              <div class="summary-row flex items-center justify-between gap-4 py-1.5 text-sm">
                <span>{{ t('bank.depositInterestRate') }}</span>
                <strong>{{ formatPercent(selectedBank.depositInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
              </div>
            </div>
            <p class="rounded-2xl border border-divider bg-card-raised px-4 py-3 text-sm text-muted">{{ t('bank.zeroBalanceFundingHint') }}</p>
            <div v-if="depositSuccess" class="success-message">{{ t('bank.depositCreated') }}</div>
            <div v-if="depositError" class="error-message">{{ depositError }}</div>
          </div>
          <div class="modal-footer flex justify-end gap-3 border-t border-divider px-6 py-5 sm:px-8 sm:py-6">
            <button class="btn btn-secondary" @click="closeDepositModal">{{ t('common.cancel') }}</button>
            <button class="btn btn-primary" :disabled="depositLoading" @click="submitDeposit">
              <span v-if="depositLoading">{{ t('common.loading') }}</span>
              <span v-else>{{ t('bank.confirmDeposit') }}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </main>
</template>

<script setup lang="ts">
/* oxlint-disable no-unused-vars */

// Split-file SFC: script symbols are consumed by LoanMarketplaceView.template.html.

import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import type { LoanOfferSummary, LoanSummary, Company, BankDepositSummary, BankInfoSummary, CollateralEligibilitySummary, PlayerBankAccountSummary } from '@/types'
import { formatLoanDuration, computeTotalRepayment, computePaymentAmount, computeTotalPayments, loanStatusClass, formatCurrency, formatPercent } from '@/lib/loanHelpers'

const { t } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const router = useRouter()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()
const loading = ref(true)
const error = ref<string | null>(null)
const myLoans = ref<LoanSummary[]>([])
const myCompanies = ref<Company[]>([])

// Active tab: 'borrow' | 'deposit'
const activeTab = ref<'borrow' | 'deposit'>('borrow')

// Banks list (for deposit tab)
const allBanks = ref<BankInfoSummary[]>([])
const myDeposits = ref<BankDepositSummary[]>([])
const myBankAccounts = ref<PlayerBankAccountSummary[]>([])

// Sort/filter state for bank list
type BankSortField = 'depositRate' | 'lendingRate' | 'capacity' | 'city'
const bankSortFields: BankSortField[] = ['depositRate', 'lendingRate', 'capacity', 'city']
const bankSortBy = ref<BankSortField>('depositRate')
const bankSortDir = ref<'asc' | 'desc'>('desc')
const bankCityFilter = ref('')
const bankShowAvailableOnly = ref(false)

// Deposit modal state
const showDepositModal = ref(false)
const selectedBank = ref<BankInfoSummary | null>(null)
const depositLoading = ref(false)
const depositError = ref<string | null>(null)
const depositSuccess = ref(false)

// Close account state
const closingAccountId = ref<string | null>(null)
const closeAccountErrors = ref<Record<string, string>>({})

// Accept modal state
const showAcceptModal = ref(false)
const selectedOffer = ref<LoanOfferSummary | null>(null)
const selectedCompanyId = ref('')
const principalAmount = ref(0)
const acceptLoading = ref(false)
const acceptError = ref<string | null>(null)

// Collateral selection state
const collateralBuildings = ref<CollateralEligibilitySummary[]>([])
const selectedCollateralBuildingId = ref<string | null>(null)
const collateralLoadError = ref<string | null>(null)

const MY_LOANS_QUERY = `
  {
    myLoans {
      id
      loanOfferId
      borrowerCompanyId
      borrowerCompanyName
      lenderCompanyId
      lenderCompanyName
      bankBuildingId
      bankBuildingName
      originalPrincipal
      remainingPrincipal
      annualInterestRatePercent
      durationTicks
      startTick
      dueTick
      nextPaymentTick
      paymentAmount
      paymentsMade
      totalPayments
      status
      missedPayments
      accumulatedPenalty
      acceptedAtUtc
      closedAtUtc
      collateralBuildingId
      collateralBuildingName
      collateralAppraisedValue
    }
  }
`

const MY_COMPANIES_QUERY = `
  {
    myCompanies {
      id
      name
      cash
      buildings {
        id
        type
        name
      }
    }
  }
`

const ACCEPT_LOAN_MUTATION = `
  mutation AcceptLoan($input: AcceptLoanInput!) {
    acceptLoan(input: $input) {
      id
      status
      originalPrincipal
      remainingPrincipal
      paymentAmount
      totalPayments
      collateralBuildingId
      collateralAppraisedValue
    }
  }
`

const ALL_BANKS_QUERY = `
  {
    allBanks {
      bankBuildingId
      bankBuildingName
      cityId
      cityName
      lenderCompanyId
      lenderCompanyName
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      outstandingLoanPrincipal
      availableLendingCapacity
      baseCapitalDeposited
    }
  }
`

const MY_DEPOSITS_QUERY = `
  {
    myDeposits {
      id
      bankBuildingId
      bankBuildingName
      depositorCompanyId
      depositorCompanyName
      amount
      depositInterestRatePercent
      isBaseCapital
      isActive
      depositedAtTick
      depositedAtUtc
      totalInterestPaid
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
      isDepositAccount
    }
  }
`

const CREATE_DEPOSIT_MUTATION = `
  mutation OpenBankAccount($input: OpenBankAccountInput!) {
    openBankAccount(input: $input) {
      id
      amount
      depositInterestRatePercent
      isActive
    }
  }
`

const CLOSE_BANK_ACCOUNT_MUTATION = `
  mutation CloseBankAccountById($input: CloseBankAccountInput!) {
    closeBankAccount(input: $input) {
      id
      isActive
      withdrawnAtUtc
    }
  }
`

const CLOSE_COMPANY_BANK_ACCOUNT_MUTATION = `
  mutation CloseCompanyBankAccountById($input: CloseCompanyBankAccountInput!) {
    closeCompanyBankAccount(input: $input) {
      id
      accountNumber
      currencyCode
      closedAtUtc
    }
  }
`

async function loadData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const banksResult = await gqlRequest<{ allBanks: BankInfoSummary[] }>(ALL_BANKS_QUERY)
    const newBanks = banksResult.allBanks ?? []
    if (!deepEqual(allBanks.value, newBanks)) {
      allBanks.value = newBanks
    }

    if (auth.isAuthenticated) {
      const [loansResult, companiesResult, depositsResult, accountsResult] = await Promise.all([
        gqlRequest<{ myLoans: LoanSummary[] }>(MY_LOANS_QUERY),
        gqlRequest<{ myCompanies: Company[] }>(MY_COMPANIES_QUERY),
        gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY),
        gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY),
      ])
      const newLoans = loansResult.myLoans ?? []
      if (!deepEqual(myLoans.value, newLoans)) {
        myLoans.value = newLoans
      }
      myCompanies.value = companiesResult.myCompanies ?? []
      const newDeposits = depositsResult.myDeposits ?? []
      if (!deepEqual(myDeposits.value, newDeposits)) {
        myDeposits.value = newDeposits
      }
      const newAccounts = accountsResult.myBankAccounts ?? []
      if (!deepEqual(myBankAccounts.value, newAccounts)) {
        myBankAccounts.value = newAccounts
      }
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadData(true)
  await restoreScrollPosition(scrollPos)
})

const activeLoans = computed(() => myLoans.value.filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE'))
const activeCompany = computed(() => getActiveCompany(auth.player, myCompanies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const visibleBankAccounts = computed(() => {
  let accounts: PlayerBankAccountSummary[] = []

  if (auth.player?.activeAccountType === 'COMPANY' && auth.player.activeCompanyId) {
    accounts = myBankAccounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === auth.player?.activeCompanyId)
  } else {
    accounts = myBankAccounts.value.filter((account) => account.ownerType === 'PERSON')
  }

  return accounts
})

// Lender eligibility: detect BANK buildings across all companies
const myBankBuildings = computed(() => myCompanies.value.flatMap((c) => (c.buildings ?? []).filter((b) => b.type === 'BANK').map((b) => ({ ...b, companyId: c.id }))))
const hasBankBuilding = computed(() => myBankBuildings.value.length > 0)
const firstBankBuilding = computed(() => myBankBuildings.value[0] ?? null)
const firstCompanyId = computed(() => myCompanies.value[0]?.id ?? null)

// Bank list sort/filter computeds
const availableBankCities = computed(() => {
  const cities = new Set(allBanks.value.map((b) => b.cityName))
  return [...cities].sort()
})

const filteredAndSortedBanks = computed(() => {
  let banks = allBanks.value

  // Filter by selected city from navbar
  if (selectedCityId.value) {
    banks = banks.filter((b) => b.cityId === selectedCityId.value)
  }

  // Filter by manual city filter if set
  if (bankCityFilter.value) {
    banks = banks.filter((b) => b.cityName === bankCityFilter.value)
  }

  if (bankShowAvailableOnly.value) {
    banks = banks.filter((b) => b.availableLendingCapacity > 0)
  }
  return [...banks].sort((a, b) => {
    let aVal: number | string
    let bVal: number | string
    if (bankSortBy.value === 'depositRate') {
      aVal = a.depositInterestRatePercent
      bVal = b.depositInterestRatePercent
    } else if (bankSortBy.value === 'lendingRate') {
      aVal = a.lendingInterestRatePercent
      bVal = b.lendingInterestRatePercent
    } else if (bankSortBy.value === 'capacity') {
      aVal = a.availableLendingCapacity
      bVal = b.availableLendingCapacity
    } else {
      aVal = a.cityName
      bVal = b.cityName
    }
    const dir = bankSortDir.value === 'asc' ? 1 : -1
    return aVal < bVal ? -dir : aVal > bVal ? dir : 0
  })
})

function toggleBankSort(field: BankSortField) {
  if (bankSortBy.value === field) {
    bankSortDir.value = bankSortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    bankSortBy.value = field
    bankSortDir.value = 'desc'
  }
}

function navigateToAcquireBank() {
  if (firstCompanyId.value) {
    router.push(`/buy-building/${firstCompanyId.value}?type=BANK`)
  } else {
    router.push('/dashboard')
  }
}

// Banks sorted for the borrow section: all open banks sorted by lowest lending rate, filtered by selected city
const sortedBanksForBorrow = computed(() => {
  let banks = allBanks.value.filter((b) => b.baseCapitalDeposited)

  // Filter by selected city from navbar
  if (selectedCityId.value) {
    banks = banks.filter((b) => b.cityId === selectedCityId.value)
  }

  return [...banks].sort((a, b) => a.lendingInterestRatePercent - b.lendingInterestRatePercent)
})

function navigateToManageBank() {
  if (firstBankBuilding.value) {
    router.push(`/bank/${firstBankBuilding.value.id}`)
  }
}

function navigateToBank(bankBuildingId: string) {
  router.push(`/bank/${bankBuildingId}`)
}

function closeAcceptModal() {
  showAcceptModal.value = false
  selectedOffer.value = null
  acceptError.value = null
  selectedCollateralBuildingId.value = null
  collateralBuildings.value = []
}

const estimatedTotalRepayment = computed(() => {
  if (!selectedOffer.value || principalAmount.value <= 0) return 0
  return computeTotalRepayment(principalAmount.value, selectedOffer.value.annualInterestRatePercent, selectedOffer.value.durationTicks)
})

const estimatedPaymentAmount = computed(() => {
  if (!selectedOffer.value || principalAmount.value <= 0) return 0
  return computePaymentAmount(principalAmount.value, selectedOffer.value.annualInterestRatePercent, selectedOffer.value.durationTicks)
})

const estimatedTotalPayments = computed(() => {
  if (!selectedOffer.value) return 0
  return computeTotalPayments(selectedOffer.value.durationTicks)
})

const selectedCompanyCash = computed(() => {
  const company = myCompanies.value.find((c) => c.id === selectedCompanyId.value)
  return company?.cash ?? 0
})

const selectedCollateral = computed(() => collateralBuildings.value.find((b) => b.buildingId === selectedCollateralBuildingId.value) ?? null)

const collateralCapacityWarning = computed(() => {
  if (!selectedCollateral.value || principalAmount.value <= 0) return null
  if (principalAmount.value > selectedCollateral.value.remainingBorrowingCapacity) {
    return t('bank.collateralExceedsLimit')
  }
  return null
})

const collateralRequiredWarning = computed(() => {
  if (principalAmount.value <= 0) return null
  if (!selectedCollateralBuildingId.value) {
    return t('bank.collateralRequired')
  }
  return null
})

async function confirmAcceptLoan() {
  if (!selectedOffer.value || !selectedCompanyId.value || principalAmount.value <= 0) return
  if (!selectedCollateralBuildingId.value) {
    acceptError.value = t('bank.collateralRequired')
    return
  }
  acceptLoading.value = true
  acceptError.value = null
  try {
    await gqlRequest(ACCEPT_LOAN_MUTATION, {
      input: {
        loanOfferId: selectedOffer.value.id,
        borrowerCompanyId: selectedCompanyId.value,
        principalAmount: principalAmount.value,
        collateralBuildingId: selectedCollateralBuildingId.value,
      },
    })
    closeAcceptModal()
    await loadData()
  } catch (err) {
    acceptError.value = err instanceof Error ? err.message : String(err)
  } finally {
    acceptLoading.value = false
  }
}

// ÔöÇÔöÇ Deposit functions ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

function openDepositModal(bank: BankInfoSummary) {
  selectedBank.value = bank
  depositError.value = null
  depositSuccess.value = false
  showDepositModal.value = true
}

function formatOpenAccountError(errorMessage: string) {
  if (!errorMessage.includes('Insufficient company funds to open this bank account.')) {
    return errorMessage
  }

  return `${errorMessage} ${t('bank.zeroBalanceFundingHint')}`
}

function closeDepositModal() {
  showDepositModal.value = false
  selectedBank.value = null
  depositError.value = null
  depositSuccess.value = false
}

async function submitDeposit() {
  if (!selectedBank.value) return
  depositLoading.value = true
  depositError.value = null
  depositSuccess.value = false
  const contextCompanyId = isCompanyAccountActive.value ? (activeCompany.value?.id ?? null) : null
  try {
    await gqlRequest(CREATE_DEPOSIT_MUTATION, {
      input: {
        bankBuildingId: selectedBank.value.bankBuildingId,
        depositorCompanyId: contextCompanyId,
        amount: 0,
      },
    })
    depositSuccess.value = true
    await loadData()
    setTimeout(closeDepositModal, 1500)
  } catch (err) {
    depositError.value = formatOpenAccountError(err instanceof Error ? err.message : String(err))
  } finally {
    depositLoading.value = false
  }
}

async function closeBankAccount(accountId: string, isDepositAccount: boolean) {
  if (!confirm(t('bank.confirmCloseAccount'))) return
  closingAccountId.value = accountId
  closeAccountErrors.value = { ...closeAccountErrors.value, [accountId]: '' }
  try {
    if (isDepositAccount) {
      await gqlRequest(CLOSE_BANK_ACCOUNT_MUTATION, { input: { depositId: accountId, amount: 0 } })
    } else {
      await gqlRequest(CLOSE_COMPANY_BANK_ACCOUNT_MUTATION, { input: { bankAccountId: accountId } })
    }
    await loadData()
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err)
    let friendlyMsg = msg
    if (msg.includes('ACCOUNT_IN_USE')) {
      friendlyMsg = t('bank.closeAccountBlockedInUse')
    } else if (msg.includes('NON_ZERO_BALANCE')) {
      friendlyMsg = t('bank.closeAccountNonZeroHint')
    } else if (msg.includes('ACTIVE_LOAN_REPAYMENT_ACCOUNT')) {
      friendlyMsg = t('bank.closeAccountBlockedActiveLoan')
    }
    closeAccountErrors.value = { ...closeAccountErrors.value, [accountId]: friendlyMsg }
  } finally {
    closingAccountId.value = null
  }
}
</script>

<style scoped src="./LoanMarketplaceView.styles.css"></style>
