<template>
  <div class="city-map-view container">
    <!-- Header -->
    <div class="page-header">
      <div>
        <button class="btn btn-secondary btn-sm" @click="router.push('/dashboard')">├ö─ç├ë {{ t('cityMap.backToDashboard') }}</button>
        <h1 v-if="city">┬ş─Ź┼ÜÔĽĹ┬┤┼×─ć {{ city.name }} ├ö├ç├Â {{ t('cityMap.title') }}</h1>
        <p class="subtitle">{{ t('cityMap.subtitle') }}</p>
      </div>
      <div class="header-controls">
        <div class="view-toggle">
          <button class="toggle-btn" :class="{ active: viewMode === 'map' }" @click="viewMode = 'map'">┬ş─Ź┼ÜÔĽĹ┬┤┼×─ć {{ t('cityMap.mapView') }}</button
          ><button class="toggle-btn" :class="{ active: viewMode === 'list' }" @click="viewMode = 'list'">┬ş─Ź├┤┼Ĺ {{ t('cityMap.listView') }}</button>
        </div>
        <div class="filter-toggle">
          <button class="toggle-btn" :class="{ active: !showAvailableOnly }" @click="showAvailableOnly = false">{{ t('cityMap.filterAll') }}</button
          ><button class="toggle-btn" :class="{ active: showAvailableOnly }" @click="showAvailableOnly = true">{{ t('cityMap.filterAvailable') }}</button>
        </div>
        <span class="lot-count">{{ t('cityMap.lotCount', { count: filteredLots.length }) }}</span>
      </div>
    </div>
    <!-- Loading -->
    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>
    <!-- Error -->
    <div v-else-if="error" class="error-message" role="alert">
      {{ error }} <button class="btn btn-secondary" @click="fetchData()">{{ t('common.tryAgain') }}</button>
    </div>
    <!-- Content --><template v-else-if="city"
      ><div class="city-content" :class="{ 'has-selection': !!selectedLot }">
        <!-- Map / List: use v-show instead of v-if so the Leaflet container is never removed from the DOM and the map renders reliably when switching back from list view (fixes blank-map regression). -->
        <div class="map-area">
          <div v-show="viewMode === 'map'" ref="mapContainer" class="map-container"></div>
          <div v-show="viewMode === 'list'" class="lot-list">
            <button
              v-for="lot in filteredLots"
              :key="lot.id"
              class="lot-list-item"
              :class="{ selected: selectedLot?.id === lot.id, available: getLotStatus(lot) === 'available', owned: getLotStatus(lot) === 'owned', yours: getLotStatus(lot) === 'yours' }"
              @click="selectLot(lot)"
            >
              <div class="lot-status-dot" :style="{ background: getLotMarkerColor(lot) }"></div>
              <div class="lot-list-info">
                <span class="lot-list-name">{{ lot.name }}</span
                ><span class="lot-list-district">{{ lot.district }}</span
                ><span v-if="lot.resourceType" class="lot-list-resource-badge" data-testid="lot-resource-badge"> ├ö┼Ą─ć {{ lot.resourceType.name }} </span>
              </div>
              <div class="lot-list-meta">
                <span class="lot-list-price">{{ formatCurrency(lot.price) }}</span
                ><span class="lot-list-status" :class="getLotStatus(lot)">
                  {{ getLotStatus(lot) === 'available' ? t('cityMap.available') : getLotStatus(lot) === 'yours' ? t('cityMap.yourProperty') : t('cityMap.owned') }}
                </span>
              </div>
            </button>
            <div v-if="filteredLots.length === 0" class="empty-state">{{ t('cityMap.noLotsAvailable') }}</div>
          </div>
        </div>
        <!-- Detail Panel -->
        <aside v-if="selectedLot" class="detail-panel">
          <div class="detail-header">
            <h2>{{ selectedLot.name }}</h2>
            <span class="status-badge" :class="getLotStatus(selectedLot)">
              {{ getLotStatus(selectedLot) === 'available' ? t('cityMap.available') : getLotStatus(selectedLot) === 'yours' ? t('cityMap.yourProperty') : t('cityMap.owned') }}
            </span>
          </div>
          <p class="lot-description">{{ selectedLot.description }}</p>
          <!-- Strategic recommendation badge -->
          <div class="strategic-recommendation" :class="strategicRecommendation(selectedLot).cssClass" data-testid="strategic-recommendation">
            <span class="rec-icon">┬ş─Ź├ä┬╗</span><span class="rec-label">{{ t(`cityMap.${strategicRecommendation(selectedLot).key}`) }}</span>
          </div>
          <div class="detail-grid">
            <div class="detail-item">
              <span class="detail-label">{{ t('cityMap.district') }}</span
              ><span class="detail-value">{{ selectedLot.district }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-label">{{ t('cityMap.appraisedValue') }}</span
              ><span class="detail-value" data-testid="appraised-value">{{ formatCurrency(selectedLot.basePrice) }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-label">{{ t('cityMap.price') }}</span
              ><span class="detail-value price" data-testid="asking-price">
                {{ formatCurrency(selectedLot.price) }}
                <span v-if="selectedLot.resourceType && selectedLot.price > selectedLot.basePrice" class="resource-premium-badge" :title="t('cityMap.resourcePremiumTooltip')">
                  {{ t('cityMap.resourcePremium') }}
                </span></span
              >
            </div>
            <div class="detail-item full-width population-index-item">
              <span class="detail-label">{{ t('cityMap.populationIndex') }}</span>
              <div class="population-index-display">
                <span class="population-index-value">{{ formatPopulationIndex(selectedLot.populationIndex) }}</span
                ><span class="population-index-tag" :class="populationIndexClass(selectedLot.populationIndex)"> {{ populationIndexLabel(selectedLot.populationIndex) }} </span>
              </div>
              <p class="population-index-hint">{{ t('cityMap.populationIndexHint') }}</p>
            </div>
            <div class="detail-item full-width">
              <span class="detail-label">{{ t('cityMap.suitableFor') }}</span>
              <div class="suitable-types">
                <span v-for="type in suitableTypesForLot" :key="type" class="type-tag"> {{ formatBuildingType(type) }} </span>
              </div>
            </div>
            <div class="detail-item full-width coordinates-item">
              <span class="detail-label">{{ t('cityMap.coordinates') }}</span
              ><span class="detail-value coordinates-value" data-testid="lot-coordinates">
                {{ Math.abs(selectedLot.latitude).toFixed(5) }}ÔöČÔľĹ{{ selectedLot.latitude >= 0 ? 'N' : 'S' }}, {{ Math.abs(selectedLot.longitude).toFixed(5) }}ÔöČÔľĹ{{
                  selectedLot.longitude >= 0 ? 'E' : 'W'
                }}
              </span>
              <p class="coordinates-hint">{{ t('cityMap.coordinatesHint') }}</p>
            </div>
          </div>
          <!-- Raw material deposit panel (shown for MINE-eligible lots with resource data) -->
          <div v-if="selectedLot.resourceType && selectedLot.materialQuality != null && selectedLot.materialQuantity != null" class="raw-material-panel" data-testid="raw-material-panel">
            <h3 class="raw-material-title">├ö┼Ą─ć {{ t('cityMap.rawMaterialTitle') }}</h3>
            <div class="raw-material-grid">
              <div class="raw-material-item">
                <span class="detail-label">{{ t('cityMap.rawMaterialResource') }}</span
                ><span class="detail-value">{{ selectedLot.resourceType.name }}</span>
              </div>
              <div class="raw-material-item">
                <span class="detail-label">{{ t('cityMap.rawMaterialQuality') }}</span
                ><span class="quality-badge" :class="materialQualityClass(selectedLot.materialQuality)">
                  {{ materialQualityLabel(selectedLot.materialQuality) }} ({{ Math.round(selectedLot.materialQuality * 100) }}%)
                </span>
              </div>
              <div class="raw-material-item full-width">
                <span class="detail-label">{{ t('cityMap.rawMaterialQuantity') }}</span
                ><span class="detail-value"> {{ selectedLot.materialQuantity.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }} </span>
              </div>
            </div>
            <p class="raw-material-hint">{{ t('cityMap.rawMaterialHint') }}</p>
          </div>
          <!-- Placement guidance panel -->
          <div class="placement-guidance-panel" data-testid="placement-guidance-panel">
            <h3 class="guidance-title">{{ t('cityMap.placementGuidanceTitle') }}</h3>
            <ul class="guidance-list">
              <li v-for="type in suitableTypesForLot" :key="type" class="guidance-item">
                <span class="guidance-building-type">{{ formatBuildingType(type) }}</span
                ><span class="guidance-text">{{ t(`cityMap.${placementGuidanceKey(type)}`) }}</span>
              </li>
            </ul>
            <p class="transport-cost-note"><span class="transport-icon">┬ş─Ź├ť├ť</span> {{ t('cityMap.transportCostNote') }}</p>
          </div>
          <!-- Weather outlook: shown for lots suitable for POWER_PLANT -->
          <div v-if="suitableTypesForLot.includes('POWER_PLANT') && cityWeather" class="weather-outlook-panel" data-testid="weather-outlook-panel">
            <h3 class="weather-outlook-title">┬ş─Ź├«─ä┬┤┼×─ć {{ t('powerPlant.weatherOutlook') }}</h3>
            <div class="weather-outlook-row">
              <span class="weather-badge solar-badge">├ö┼Ť├ç┬┤┼×─ć {{ t('powerPlant.solarPercent', { percent: Math.round(cityWeather.currentSolarPercent) }) }}</span
              ><span class="weather-badge wind-badge">┬ş─Ź─║─ś {{ t('powerPlant.windPercent', { percent: Math.round(cityWeather.currentWindPercent) }) }}</span>
            </div>
            <div v-if="cityWeather.forecast.length > 0" class="weather-forecast-bars">
              <div
                v-for="(tick, i) in cityWeather.forecast.slice(0, 12)"
                :key="tick.tick"
                class="forecast-bar-group"
                :title="`Tick ${tick.tick}: ├ö┼Ť├ç┬┤┼×─ć${Math.round(tick.solarPercent)}% ┬ş─Ź─║─ś${Math.round(tick.windPercent)}%`"
              >
                <div class="forecast-bar solar-bar" :style="{ height: Math.round(tick.solarPercent) + '%' }"></div>
                <div class="forecast-bar wind-bar" :style="{ height: Math.round(tick.windPercent) + '%' }"></div>
                <span v-if="i === 0 || i === 11" class="forecast-bar-label">{{ i === 0 ? 'Now' : '+12' }}</span>
              </div>
            </div>
          </div>
          <!-- Owner info for owned lots -->
          <div v-if="selectedLot.ownerCompany" class="owner-info">
            <span class="detail-label">{{ t('cityMap.owner') }}</span
            ><span class="detail-value">{{ selectedLot.ownerCompany.name }}</span>
          </div>
          <div v-if="selectedLot.building" class="building-info">
            <span class="detail-label">{{ t('cityMap.building') }}</span
            ><span class="detail-value"> {{ selectedLot.building.name }} ({{ formatBuildingType(selectedLot.building.type) }}) </span>
          </div>
          <!-- Purchase flow -->
          <div v-if="!auth.isAuthenticated" class="purchase-notice">{{ t('cityMap.loginRequired') }}</div>
          <div v-else-if="companies.length === 0" class="purchase-notice">{{ t('cityMap.noCompany') }}</div>
          <div v-else-if="!isCompanyAccountActive" class="purchase-notice">{{ t('cityMap.companyAccountRequired') }}</div>
          <template v-else
            ><!-- Stale-lot / general purchase error shown regardless of current lot availability -->
            <div v-if="purchaseError && !purchaseMode" class="error-message purchase-error-notice" role="alert" aria-live="polite">{{ purchaseError }}</div>
            <template v-if="canPurchase"
              ><div v-if="!purchaseMode" class="purchase-actions">
                <button class="btn btn-primary" @click="startPurchase()">{{ t('cityMap.purchase') }}</button>
              </div>
              <div v-else class="purchase-form">
                <div class="form-group">
                  <label>{{ t('cityMap.buildingType') }}</label>
                  <div class="building-type-cards" role="radiogroup" :aria-label="t('cityMap.buildingType')">
                    <button
                      v-for="type in suitableTypesForLot"
                      :key="type"
                      class="building-type-card"
                      :class="{ selected: selectedBuildingType === type }"
                      type="button"
                      role="radio"
                      :aria-checked="selectedBuildingType === type"
                      @click="selectedBuildingType = type"
                    >
                      <span class="card-type-icon">{{ t(`buildings.typeIcons.${type}`) }}</span
                      ><span class="card-type-name">{{ formatBuildingType(type) }}</span
                      ><span class="card-type-desc">{{ t(`buildings.typeDescriptions.${type}`) }}</span>
                    </button>
                  </div>
                  <p v-if="selectedBuildingType" class="selected-type-guidance">{{ t(`cityMap.${placementGuidanceKey(selectedBuildingType)}`) }}</p>
                </div>
                <div class="form-group">
                  <label
                    >{{ t('cityMap.buildingName') }} <span class="optional-hint">({{ t('common.optional') }})</span></label
                  ><input v-model="buildingName" type="text" class="form-input" :placeholder="t('cityMap.buildingNamePlaceholder')" />
                </div>
                <div class="form-group">
                  <label>{{ t('cityMap.company') }}</label>
                  <div class="active-company-summary">
                    <strong>{{ selectedCompany?.name }}</strong
                    ><span>{{ selectedCompany ? formatCurrency(selectedCompany.cash) : '' }}</span>
                  </div>
                </div>
                <!-- Media house channel type (only for MEDIA_HOUSE) -->
                <div v-if="selectedBuildingType === 'MEDIA_HOUSE'" class="form-group">
                  <label>{{ t('cityMap.mediaHouseChannelType') }}</label
                  ><select v-model="selectedMediaType" class="form-select" required>
                    <option value="">{{ t('cityMap.selectMediaType') }}</option>
                    <option value="NEWSPAPER">┬ş─Ź├┤ÔľĹ {{ t('cityMap.mediaTypespaper') }} (Ôöť┼Ü1.0)</option>
                    <option value="RADIO">┬ş─Ź├┤ÔĽŚ {{ t('cityMap.mediaTypeRadio') }} (Ôöť┼Ü1.5)</option>
                    <option value="TV">┬ş─Ź├┤ÔĽĹ {{ t('cityMap.mediaTypeTv') }} (Ôöť┼Ü2.0)</option>
                  </select>
                  <p class="form-hint">{{ t('cityMap.mediaTypeHint') }}</p>
                </div>
                <!-- Power plant type picker (only for POWER_PLANT) -->
                <div v-if="selectedBuildingType === 'POWER_PLANT'" class="form-group">
                  <label>{{ t('powerPlant.plantTypeLabel') }}</label>
                  <div class="plant-type-cards" role="radiogroup" :aria-label="t('powerPlant.plantTypeLabel')">
                    <button
                      v-for="pt in POWER_PLANT_TYPES"
                      :key="pt.type"
                      class="plant-type-card"
                      :class="{ selected: selectedPowerPlantType === pt.type }"
                      type="button"
                      role="radio"
                      :aria-checked="selectedPowerPlantType === pt.type"
                      @click="selectedPowerPlantType = pt.type"
                    >
                      <span class="plant-type-name">{{ t(pt.labelKey) }}</span
                      ><span class="plant-type-mw">{{ t('powerPlant.outputMw', { output: pt.mw }) }}</span
                      ><span v-if="pt.type === 'SOLAR' && cityWeather" class="plant-weather-badge solar"> ├ö┼Ť├ç┬┤┼×─ć {{ Math.round(cityWeather.currentSolarPercent) }}% </span
                      ><span v-else-if="pt.type === 'WIND' && cityWeather" class="plant-weather-badge wind"> ┬ş─Ź─║─ś {{ Math.round(cityWeather.currentWindPercent) }}% </span
                      ><span v-else-if="pt.type === 'SOLAR' || pt.type === 'WIND'" class="plant-type-badge renewable"> {{ t('powerPlant.renewableBadge') }} </span
                      ><span v-else class="plant-type-badge fuel">{{ t('powerPlant.fuelBadge') }}</span
                      ><span class="plant-type-desc">{{ t(pt.descKey) }}</span>
                    </button>
                  </div>
                  <p v-if="!selectedPowerPlantType" class="form-hint">{{ t('powerPlant.noPlantTypeSelected') }}</p>
                </div>
                <!-- Mining deposit investment summary (shown when MINE type selected and lot has resource) -->
                <div v-if="selectedBuildingType === 'MINE' && selectedLot?.resourceType" class="mining-deposit-summary" data-testid="mining-deposit-summary">
                  <h4 class="deposit-summary-title">├ö┼Ą─ć {{ t('cityMap.miningDepositSummaryTitle') }}</h4>
                  <div class="deposit-summary-grid">
                    <div class="deposit-summary-item">
                      <span class="deposit-label">{{ t('cityMap.rawMaterialResource') }}</span
                      ><span class="deposit-value deposit-resource-name">{{ selectedLot.resourceType.name }}</span>
                    </div>
                    <div class="deposit-summary-item" v-if="selectedLot.materialQuality !== null">
                      <span class="deposit-label">{{ t('cityMap.rawMaterialQuality') }}</span
                      ><span class="quality-badge" :class="materialQualityClass(selectedLot.materialQuality)">
                        {{ materialQualityLabel(selectedLot.materialQuality) }} ({{ Math.round(selectedLot.materialQuality * 100) }}%)
                      </span>
                    </div>
                    <div class="deposit-summary-item" v-if="selectedLot.materialQuantity !== null">
                      <span class="deposit-label">{{ t('cityMap.rawMaterialQuantity') }}</span
                      ><span class="deposit-value">{{ selectedLot.materialQuantity.toLocaleString(locale) }} {{ t('cityMap.rawMaterialQuantityUnit') }}</span>
                    </div>
                  </div>
                  <p class="deposit-investment-hint">{{ t('cityMap.miningInvestmentHint') }}</p>
                </div>
                <!-- Purchase cost summary -->
                <div class="purchase-cost-summary" aria-label="Purchase cost summary">
                  <div class="cost-row">
                    <span class="cost-label">{{ t('cityMap.costLotPrice') }}</span
                    ><span class="cost-value cost-debit">{{ selectedLot ? formatCurrency(selectedLot.price) : '├ö├ç├Â' }}</span>
                  </div>
                  <div v-if="selectedBuildingType" class="cost-row">
                    <span class="cost-label">{{ t('cityMap.costConstruction') }}</span
                    ><span class="cost-value cost-debit">{{ formatCurrency(constructionCostForType(selectedBuildingType)) }}</span>
                  </div>
                  <div v-if="selectedBuildingType" class="cost-row construction-time-row">
                    <span class="cost-label">{{ t('cityMap.constructionTime') }}</span
                    ><span class="cost-value construction-ticks" :title="constructionTicksForType(selectedBuildingType) + ' ticks'">
                      {{ t('cityMap.constructionTicks', { time: formatTickDuration(constructionTicksForType(selectedBuildingType), locale) }) }}
                    </span>
                  </div>
                  <div v-if="selectedCompany" class="cost-row">
                    <span class="cost-label">{{ t('cityMap.costCurrentCash') }}</span
                    ><span class="cost-value">{{ formatCurrency(selectedCompany.cash) }}</span>
                  </div>
                  <div v-if="cashAfterPurchase !== null" class="cost-row cost-row-result">
                    <span class="cost-label">{{ t('cityMap.costRemainingCash') }}</span
                    ><span class="cost-value" :class="cashAfterPurchase < 0 ? 'cost-negative' : 'cost-positive'"> {{ formatCurrency(cashAfterPurchase) }} </span>
                  </div>
                </div>
                <div v-if="purchaseError" class="error-message" role="alert">{{ purchaseError }}</div>
                <div class="purchase-actions">
                  <button class="btn btn-secondary" @click="purchaseMode = false">{{ t('common.cancel') }}</button
                  ><button class="btn btn-primary" :disabled="!canSubmitPurchase" @click="confirmPurchase()">{{ purchasing ? t('cityMap.purchasing') : t('cityMap.confirmPurchase') }}</button>
                </div>
              </div></template
            ></template
          ><!-- Post-purchase banner: under-construction state -->
          <div
            v-if="justPurchasedBuildingId && isOwnedByActiveCompany && justPurchasedIsUnderConstruction"
            class="post-purchase-banner construction-banner"
            role="status"
            data-testid="construction-banner"
          >
            <div class="post-purchase-body">
              <strong class="post-purchase-title"> ┬ş─Ź─ć┼Ü┬┤┼×─ć {{ t('cityMap.constructionStartedTitle') }} </strong>
              <p class="post-purchase-text">
                {{
                  t('cityMap.constructionStartedBody', {
                    type: formatBuildingType(justPurchasedBuildingType ?? 'FACTORY'),
                    time: formatTickDuration(
                      justPurchasedConstructionCompletesAtTick
                        ? constructionTicksRemaining(justPurchasedConstructionCompletesAtTick)
                        : constructionTicksForType(justPurchasedBuildingType ?? 'FACTORY'),
                      locale,
                    ),
                  })
                }}
              </p>
              <div class="construction-progress-bar" aria-label="Construction progress"><div class="construction-progress-fill" style="width: 0%"></div></div>
              <p class="construction-hint">{{ t('cityMap.constructionHint') }}</p>
            </div>
          </div>
          <!-- Post-purchase setup guidance (shown immediately after a successful purchase, operational) -->
          <div v-else-if="justPurchasedBuildingId && isOwnedByActiveCompany" class="post-purchase-banner" role="status">
            <div class="post-purchase-body">
              <strong class="post-purchase-title">{{ t(`buildings.typeIcons.${justPurchasedBuildingType ?? 'FACTORY'}`) }} {{ t('cityMap.postPurchaseTitle') }}</strong>
              <p class="post-purchase-text">{{ t(`cityMap.${postPurchaseBodyKey(justPurchasedBuildingType ?? 'FACTORY')}`) }}</p>
            </div>
            <RouterLink :to="justPurchasedBuildingType === 'BANK' ? `/bank/${justPurchasedBuildingId}` : `/building/${justPurchasedBuildingId}`" class="btn btn-primary">
              {{ t('cityMap.setupBuilding') }} ├ö─ç─║
            </RouterLink>
          </div>
          <div v-else-if="isOwnedByDifferentControlledCompany" class="purchase-notice">
            {{ t('cityMap.switchCompanyToManage', { company: selectedLot.ownerCompany?.name ?? t('cityMap.company') }) }}
          </div>
          <!-- Already owned by player: building under construction -->
          <div
            v-else-if="isOwnedByActiveCompany && selectedLot.building && selectedLot.building.isUnderConstruction"
            class="your-building-actions construction-state"
            data-testid="under-construction-panel"
          >
            <div class="construction-info">
              <span class="construction-badge">┬ş─Ź─ć┼Ü┬┤┼×─ć {{ t('cityMap.underConstruction') }}</span>
              <p class="construction-detail">{{ selectedLot.building.name }} ({{ formatBuildingType(selectedLot.building.type) }})</p>
              <p class="construction-ticks-info" data-testid="construction-ticks-remaining" :title="constructionTicksRemaining(selectedLot.building.constructionCompletesAtTick) + ' ticks'">
                {{ t('cityMap.ticksRemaining', { time: formatTickDuration(constructionTicksRemaining(selectedLot.building.constructionCompletesAtTick), locale) }) }}
              </p>
            </div>
            <RouterLink :to="selectedLot.building?.type === 'BANK' ? `/bank/${selectedLot.buildingId}` : `/building/${selectedLot.buildingId}`" class="btn btn-ghost">
              {{ t('cityMap.viewConstruction') }}
            </RouterLink>
          </div>
          <!-- Already owned by player (standard manage link, building operational) -->
          <div v-else-if="isOwnedByActiveCompany && selectedLot.buildingId" class="your-building-actions">
            <RouterLink :to="selectedLot.building?.type === 'BANK' ? `/bank/${selectedLot.buildingId}` : `/building/${selectedLot.buildingId}`" class="btn btn-primary">
              {{ t('cityMap.manageBuilding') }}
            </RouterLink>
          </div>
        </aside>
        <!-- No selection prompt -->
        <aside v-else class="detail-panel empty-panel">
          <p class="select-prompt">{{ t('cityMap.selectLot') }}</p>
        </aside>
      </div></template
    ><!-- City media houses section -->
    <section class="media-houses-section" aria-labelledby="media-houses-heading">
      <h2 id="media-houses-heading" class="section-heading">┬ş─Ź├┤ÔĽĹ {{ t('cityMap.mediaHouses.title') }}</h2>
      <p class="section-subtitle">{{ t('cityMap.mediaHouses.subtitle') }}</p>
      <div v-if="mediaHousesLoading" class="media-houses-loading">{{ t('common.loading') }}</div>
      <div v-else-if="cityMediaHouses.length === 0" class="media-houses-empty">
        <p>{{ t('cityMap.mediaHouses.empty') }}</p>
        <p class="hint">{{ t('cityMap.mediaHouses.emptyHint') }}</p>
      </div>
      <div v-else class="media-houses-grid">
        <div v-for="mh in cityMediaHouses" :key="mh.id" class="media-house-card" :class="{ 'mh-offline': mh.powerStatus === 'OFFLINE', 'mh-construction': mh.isUnderConstruction }">
          <div class="mh-channel-icon"><span v-if="mh.mediaType === 'TV'">┬ş─Ź├┤ÔĽĹ</span><span v-else-if="mh.mediaType === 'RADIO'">┬ş─Ź├┤ÔĽŚ</span><span v-else>┬ş─Ź├┤ÔľĹ</span></div>
          <div class="mh-info">
            <strong class="mh-name">{{ mh.name }}</strong>
            <div class="mh-badges">
              <span class="mh-type-badge">{{ mh.mediaType ?? '?' }}</span
              ><span v-if="mh.isGovernmentOwned" class="mh-gov-badge" :title="t('cityMap.mediaHouses.governmentOwned')">{{ t('cityMap.mediaHouses.govBadge') }}</span
              ><span v-if="mh.isUnderConstruction" class="mh-status-badge construction"> {{ t('cityMap.mediaHouses.underConstruction') }} </span
              ><span v-else-if="mh.powerStatus === 'OFFLINE'" class="mh-status-badge offline"> {{ t('cityMap.mediaHouses.offline') }} </span>
            </div>
            <div class="mh-owner">{{ t('cityMap.mediaHouses.owner') }}: {{ mh.ownerCompanyName }}</div>
            <div class="mh-effectiveness">
              {{ t('cityMap.mediaHouses.effectiveness') }}: <strong>Ôöť┼Ü{{ mh.effectivenessMultiplier.toFixed(1) }}</strong
              ><span class="effectiveness-hint">
                {{ mh.mediaType === 'TV' ? t('cityMap.mediaHouses.tvHint') : mh.mediaType === 'RADIO' ? t('cityMap.mediaHouses.radioHint') : t('cityMap.mediaHouses.newspaperHint') }}
              </span>
            </div>
            <div class="mh-ranking">
              {{ t('cityMap.mediaHouses.contentRanking') }}: <strong>{{ mh.contentRanking.toFixed(0) }}%</strong>
            </div>
          </div>
        </div>
      </div>
    </section>
    <!-- City Power Planning & Weather Forecast section (always visible) -->
    <section class="city-power-section" aria-labelledby="city-power-heading" data-testid="city-power-section">
      <h2 id="city-power-heading" class="section-heading">├ö├ť├ş {{ t('powerGrid.weatherSectionTitle') }}</h2>
      <div class="power-planning-grid">
        <!-- Weather forecast card -->
        <div v-if="cityWeather" class="power-card weather-card" data-testid="city-weather-card">
          <h3 class="power-card-title">┬ş─Ź├«─ä┬┤┼×─ć {{ t('powerGrid.currentConditions') }}</h3>
          <div class="weather-badges">
            <span class="weather-big-badge solar" data-testid="solar-badge"> ├ö┼Ť├ç┬┤┼×─ć {{ Math.round(cityWeather.currentSolarPercent) }}% </span
            ><span class="weather-big-badge wind" data-testid="wind-badge"> ┬ş─Ź─║─ś {{ Math.round(cityWeather.currentWindPercent) }}% </span>
          </div>
          <div v-if="cityWeather.forecast.length > 0" class="forecast-chart">
            <p class="forecast-chart-label">{{ t('powerGrid.forecastBarsLabel', { count: Math.min(cityWeather.forecast.length, 24) }) }}</p>
            <div class="forecast-bars-row" aria-label="Weather forecast chart">
              <div
                v-for="(tick, i) in cityWeather.forecast.slice(0, 24)"
                :key="tick.tick"
                class="forecast-bar-group"
                :title="`Tick ${tick.tick}: ├ö┼Ť├ç┬┤┼×─ć${Math.round(tick.solarPercent)}% ┬ş─Ź─║─ś${Math.round(tick.windPercent)}%`"
              >
                <div class="forecast-bar solar-bar" :style="{ height: Math.round(tick.solarPercent) + '%' }"></div>
                <div class="forecast-bar wind-bar" :style="{ height: Math.round(tick.windPercent) + '%' }"></div>
                <span v-if="i === 0 || i === 23 || (i === cityWeather.forecast.slice(0, 24).length - 1 && i !== 23)" class="forecast-bar-label">
                  {{ i === 0 ? t('powerGrid.forecastNow') : t('powerGrid.forecastTickLabel', { count: i + 1 }) }}
                </span>
              </div>
            </div>
          </div>
        </div>
        <!-- Power balance card -->
        <div class="power-card balance-card" data-testid="city-power-balance-card">
          <h3 class="power-card-title">┬ş─Ź─ć┼č {{ t('powerGrid.planningTitle') }}</h3>
          <template v-if="cityPowerBalance"
            ><div class="balance-status-row">
              <span
                class="balance-status-badge"
                :class="{
                  'status-balanced': cityPowerBalance.status === 'BALANCED',
                  'status-constrained': cityPowerBalance.status === 'CONSTRAINED',
                  'status-critical': cityPowerBalance.status === 'CRITICAL',
                }"
              >
                {{ t(`powerGrid.status.${cityPowerBalance.status}`) }} </span
              ><span v-if="cityPowerBalance.powerPlantCount === 0" class="legacy-badge">{{ t('powerGrid.legacyGrid') }}</span>
            </div>
            <div class="balance-metrics">
              <div class="balance-metric">
                <span class="balance-metric-label">{{ t('powerGrid.supply') }}</span
                ><span class="balance-metric-value supply">{{ cityPowerBalance.totalSupplyMw.toFixed(1) }} MW</span>
              </div>
              <div class="balance-metric">
                <span class="balance-metric-label">{{ t('powerGrid.demand') }}</span
                ><span class="balance-metric-value demand">{{ cityPowerBalance.totalDemandMw.toFixed(1) }} MW</span>
              </div>
              <div class="balance-metric">
                <span class="balance-metric-label">{{ t('powerGrid.reserve') }}</span
                ><span class="balance-metric-value" :class="cityPowerBalance.reserveMw >= 0 ? 'reserve-ok' : 'reserve-low'">
                  {{ cityPowerBalance.reserveMw >= 0 ? '+' : '' }}{{ cityPowerBalance.reserveMw.toFixed(1) }} MW
                </span>
              </div>
            </div>
            <p class="balance-guidance">
              {{
                cityPowerBalance.powerPlantCount === 0
                  ? t('powerGrid.guidanceLegacy')
                  : cityPowerBalance.status === 'BALANCED'
                    ? t('powerGrid.guidanceBalanced')
                    : cityPowerBalance.status === 'CONSTRAINED'
                      ? t('powerGrid.guidanceConstrained')
                      : t('powerGrid.guidanceCritical')
              }}
            </p></template
          >
          <p v-else class="balance-loading">{{ t('common.loading') }}</p>
        </div>
        <!-- Why it matters card -->
        <div class="power-card why-card" data-testid="why-matters-card">
          <h3 class="power-card-title">┬ş─Ź─║├ş {{ t('powerGrid.whyMattersTitle') }}</h3>
          <ul class="why-list">
            <li class="why-item solar-item">├ö┼Ť├ç┬┤┼×─ć {{ t('powerGrid.whyMattersSolar') }}</li>
            <li class="why-item wind-item">┬ş─Ź─║─ś {{ t('powerGrid.whyMattersWind') }}</li>
            <li class="why-item power-item">├ö├ť├ş {{ t('powerGrid.whyMattersPower') }}</li>
          </ul>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/* oxlint-disable no-unused-vars */

// Split-file SFC: script symbols are consumed by CityMapView.template.html.

import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameStateStore } from '@/stores/gameState'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { formatTickDuration } from '@/lib/gameTime'
import { formatMoney } from '@/lib/currencyFormat'
import {
  getLotStatus as lotStatusFromOwnership,
  getLotMarkerColor as markerColorFromStatus,
  formatPopulationIndex,
  populationIndexClass,
  canPurchaseLot as isPurchasable,
  canSubmitPurchaseForm as isFormSubmittable,
  constructionCostForType,
  constructionTicksForType,
  constructionTicksRemaining as computeConstructionTicksRemaining,
} from '@/lib/cityMapHelpers'
import { getActiveCompany } from '@/lib/accountContext'
import type { City, BuildingLot, Company, PurchaseLotResult, CityMediaHouseInfo, CityWeatherForecast, CityPowerBalance } from '@/types'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const gameStateStore = useGameStateStore()

const cityId = computed(() => route.params.id as string)
const highlightedBuildingId = computed(() => (typeof route.query.building === 'string' ? route.query.building : null))

const loading = ref(true)
const error = ref<string | null>(null)
const city = ref<City | null>(null)
const lots = ref<BuildingLot[]>([])
const companies = ref<Company[]>([])
const selectedLot = ref<BuildingLot | null>(null)
const showAvailableOnly = ref(false)
const viewMode = ref<'map' | 'list'>('map')

// Purchase form state
const purchaseMode = ref(false)
const selectedBuildingType = ref('')
const selectedPowerPlantType = ref('')
const buildingName = ref('')
const selectedMediaType = ref('')
const purchasing = ref(false)
const purchaseError = ref<string | null>(null)
const purchaseSuccess = ref<string | null>(null)
const justPurchasedBuildingId = ref<string | null>(null)
const justPurchasedBuildingType = ref<string | null>(null)
const justPurchasedIsUnderConstruction = ref(false)
const justPurchasedConstructionCompletesAtTick = ref<number | null>(null)

// Weather forecast and power balance for the current city
const cityWeather = ref<CityWeatherForecast | null>(null)
const cityPowerBalance = ref<CityPowerBalance | null>(null)

// Power plant type options with MW output
const POWER_PLANT_TYPES = [
  { type: 'COAL', labelKey: 'powerGrid.plantTypes.COAL', mw: 50, descKey: 'powerPlant.coalDescription' },
  { type: 'GAS', labelKey: 'powerGrid.plantTypes.GAS', mw: 40, descKey: 'powerPlant.gasDescription' },
  { type: 'SOLAR', labelKey: 'powerGrid.plantTypes.SOLAR', mw: 20, descKey: 'powerPlant.solarDescription' },
  { type: 'WIND', labelKey: 'powerGrid.plantTypes.WIND', mw: 25, descKey: 'powerPlant.windDescription' },
  { type: 'NUCLEAR', labelKey: 'powerGrid.plantTypes.NUCLEAR', mw: 200, descKey: 'powerPlant.nuclearDescription' },
]

// Map reference
const mapContainer = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null

// City media houses
const cityMediaHouses = ref<CityMediaHouseInfo[]>([])
const mediaHousesLoading = ref(false)
let markers: L.Marker[] = []

const filteredLots = computed(() => {
  if (showAvailableOnly.value) {
    return lots.value.filter((lot) => !lot.ownerCompanyId)
  }
  return lots.value
})

const suitableTypesForLot = computed(() => {
  if (!selectedLot.value) return []
  return selectedLot.value.suitableTypes.split(',').map((s) => s.trim())
})

const isOwnedByPlayer = computed(() => {
  if (!selectedLot.value) return false
  return companies.value.some((c) => c.id === selectedLot.value?.ownerCompanyId)
})

const activeCompany = computed(() => getActiveCompany(auth.player, companies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const isOwnedByActiveCompany = computed(() => !!selectedLot.value?.ownerCompanyId && selectedLot.value.ownerCompanyId === activeCompany.value?.id)
const isOwnedByDifferentControlledCompany = computed(() => isOwnedByPlayer.value && !!selectedLot.value?.ownerCompanyId && selectedLot.value.ownerCompanyId !== activeCompany.value?.id)

const canPurchase = computed(() => (selectedLot.value ? isCompanyAccountActive.value && isPurchasable(auth.isAuthenticated, companies.value.length, selectedLot.value.ownerCompanyId) : false))

const canSubmitPurchase = computed(() => {
  const baseValid = isFormSubmittable(selectedBuildingType.value, buildingName.value, activeCompany.value?.id ?? '', purchasing.value)
  // Media houses require a channel type selection.
  if (selectedBuildingType.value === 'MEDIA_HOUSE' && !selectedMediaType.value) return false
  // Power plants require a plant type selection.
  if (selectedBuildingType.value === 'POWER_PLANT' && !selectedPowerPlantType.value) return false
  return baseValid
})

const selectedCompany = computed(() => activeCompany.value)

const cashAfterPurchase = computed(() => {
  if (!selectedCompany.value || !selectedLot.value) return null
  const constructionCost = selectedBuildingType.value ? constructionCostForType(selectedBuildingType.value) : 0
  return selectedCompany.value.cash - selectedLot.value.price - constructionCost
})

/** Returns remaining construction ticks for the current building, using the live tick from the game state store. */
function constructionTicksRemaining(completesAtTick: number | null): number {
  const currentTick = gameStateStore.gameState?.currentTick ?? 0
  return computeConstructionTicksRemaining(completesAtTick, currentTick)
}

function getLotStatus(lot: BuildingLot): 'available' | 'owned' | 'yours' {
  return lotStatusFromOwnership(
    lot.ownerCompanyId,
    companies.value.map((c) => c.id),
  )
}

function getLotMarkerColor(lot: BuildingLot): string {
  return markerColorFromStatus(getLotStatus(lot))
}

function formatCurrency(value: number): string {
  return formatMoney(value, city.value?.currencyCode ?? 'EUR', locale.value)
}

function formatBuildingType(type: string): string {
  const key = `buildings.types.${type}`
  const translated = t(key)
  if (translated !== key) return translated
  return type.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

function populationIndexLabel(value: number): string {
  if (value >= 1.8) return t('cityMap.populationIndexVeryHigh')
  if (value >= 1.3) return t('cityMap.populationIndexHigh')
  if (value >= 0.9) return t('cityMap.populationIndexMedium')
  return t('cityMap.populationIndexLow')
}

/**
 * Returns a short strategic recommendation label for the lot based on its
 * population index and resource data. This implements the ROADMAP requirement:
 * "include a simple recommendation label such as 'strong for retail demand,'
 * 'balanced starter location,' or 'resource-oriented.'"
 */
function strategicRecommendation(lot: BuildingLot): { key: string; cssClass: string } {
  const suitable = lot.suitableTypes.split(',').map((s) => s.trim())
  const hasMine = suitable.includes('MINE')
  const hasRetail = suitable.includes('SALES_SHOP')
  const hasFactory = suitable.includes('FACTORY')

  if (hasMine && lot.resourceType) {
    return { key: 'recommendationResourceOriented', cssClass: 'rec-resource' }
  }
  if (hasRetail && lot.populationIndex >= 1.3) {
    return { key: 'recommendationStrongRetail', cssClass: 'rec-retail' }
  }
  if (hasFactory && lot.populationIndex < 0.9) {
    return { key: 'recommendationIndustrialEfficiency', cssClass: 'rec-industrial' }
  }
  return { key: 'recommendationBalancedStarter', cssClass: 'rec-balanced' }
}

function materialQualityLabel(quality: number): string {
  if (quality >= 0.8) return t('cityMap.rawMaterialQualityExcellent')
  if (quality >= 0.6) return t('cityMap.rawMaterialQualityGood')
  if (quality >= 0.4) return t('cityMap.rawMaterialQualityFair')
  return t('cityMap.rawMaterialQualityPoor')
}

function materialQualityClass(quality: number): string {
  if (quality >= 0.8) return 'quality-excellent'
  if (quality >= 0.6) return 'quality-good'
  if (quality >= 0.4) return 'quality-fair'
  return 'quality-poor'
}

function placementGuidanceKey(buildingType: string): string {
  const map: Record<string, string> = {
    SALES_SHOP: 'placementGuidanceSalesShop',
    COMMERCIAL: 'placementGuidanceCommercial',
    FACTORY: 'placementGuidanceFactory',
    MINE: 'placementGuidanceMine',
    APARTMENT: 'placementGuidanceApartment',
    RESEARCH_DEVELOPMENT: 'placementGuidanceResearchDevelopment',
    POWER_PLANT: 'placementGuidancePowerPlant',
    BANK: 'placementGuidanceBank',
    EXCHANGE: 'placementGuidanceExchange',
    MEDIA_HOUSE: 'placementGuidanceMediaHouse',
  }
  return map[buildingType] ?? 'placementGuidanceGeneric'
}

function postPurchaseBodyKey(buildingType: string): string {
  const map: Record<string, string> = {
    FACTORY: 'postPurchaseBodyFactory',
    MINE: 'postPurchaseBodyMine',
    SALES_SHOP: 'postPurchaseBodySalesShop',
    RESEARCH_DEVELOPMENT: 'postPurchaseBodyResearchDevelopment',
    APARTMENT: 'postPurchaseBodyApartment',
    COMMERCIAL: 'postPurchaseBodyCommercial',
    MEDIA_HOUSE: 'postPurchaseBodyMediaHouse',
    BANK: 'postPurchaseBodyBank',
    EXCHANGE: 'postPurchaseBodyExchange',
    POWER_PLANT: 'postPurchaseBodyPowerPlant',
  }
  return map[buildingType] ?? 'postPurchaseBody'
}

async function fetchData() {
  loading.value = true
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const [cityData, lotsData, companiesData] = await Promise.all([
      gqlRequest<{ city: City }>(
        `query GetCity($id: UUID!) {
          city(id: $id) {
            id name countryCode latitude longitude population
            resources { resourceType { id name slug category } abundance }
          }
        }`,
        { id: cityId.value },
      ),
      gqlRequest<{ cityLots: BuildingLot[] }>(
        `query CityLots($cityId: UUID!) {
          cityLots(cityId: $cityId) {
            id cityId name description district latitude longitude
            populationIndex basePrice price suitableTypes
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
            resourceType { id name slug }
            materialQuality materialQuantity
          }
        }`,
        { cityId: cityId.value },
      ),
      auth.isAuthenticated ? gqlRequest<{ myCompanies: Company[] }>(`{ myCompanies { id name cash foundedAtUtc buildings { id } } }`) : Promise.resolve({ myCompanies: [] as Company[] }),
    ])

    city.value = cityData.city
    lots.value = lotsData.cityLots
    companies.value = companiesData.myCompanies
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load city data'
  } finally {
    loading.value = false
  }
}

async function fetchMediaHouses() {
  if (!cityId.value) return
  mediaHousesLoading.value = true
  try {
    const data = await gqlRequest<{ cityMediaHouses: CityMediaHouseInfo[] }>(
      `query CityMediaHouses($cityId: UUID!) {
        cityMediaHouses(cityId: $cityId) {
          id name cityName mediaType effectivenessMultiplier ownerCompanyName
          powerStatus isUnderConstruction contentRanking contentValue contentBudgetPerTick isGovernmentOwned
        }
      }`,
      { cityId: cityId.value },
    )
    cityMediaHouses.value = data.cityMediaHouses ?? []
  } catch {
    cityMediaHouses.value = []
  } finally {
    mediaHousesLoading.value = false
  }
}

async function fetchWeatherForecast() {
  if (!cityId.value) return
  try {
    const data = await gqlRequest<{ cityWeatherForecast: CityWeatherForecast | null }>(
      `query CityWeatherForecast($cityId: UUID!) {
        cityWeatherForecast(cityId: $cityId) {
          cityId currentWindPercent currentSolarPercent
          forecast { tick windPercent solarPercent }
        }
      }`,
      { cityId: cityId.value },
    )
    cityWeather.value = data.cityWeatherForecast ?? null
  } catch {
    cityWeather.value = null
  }
}

async function fetchCityPowerBalance() {
  if (!cityId.value) return
  try {
    const data = await gqlRequest<{ cityPowerBalance: CityPowerBalance }>(
      `query CityPowerBalance($cityId: UUID!) {
        cityPowerBalance(cityId: $cityId) {
          cityId totalSupplyMw totalDemandMw reserveMw reservePercent status
          powerPlantCount consumerBuildingCount
        }
      }`,
      { cityId: cityId.value },
    )
    cityPowerBalance.value = data.cityPowerBalance ?? null
  } catch {
    cityPowerBalance.value = null
  }
}

function createMarkerIcon(color: string, isSelected: boolean): L.DivIcon {
  const size = isSelected ? 18 : 12
  const border = isSelected ? '3px solid #fff' : '2px solid rgba(255,255,255,0.8)'
  return L.divIcon({
    className: 'lot-marker',
    html: `<div style="
      width:${size}px;height:${size}px;
      background:${color};
      border-radius:50%;
      border:${border};
      box-shadow:0 2px 6px rgba(0,0,0,0.4);
    "></div>`,
    iconSize: [size + 6, size + 6],
    iconAnchor: [(size + 6) / 2, (size + 6) / 2],
  })
}

function initMap() {
  if (!mapContainer.value || !city.value) return

  map = L.map(mapContainer.value, {
    center: [city.value.latitude, city.value.longitude],
    zoom: 14,
    zoomControl: true,
  })

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  updateMarkers()
}

function updateMarkers() {
  if (!map) return

  // Clear existing markers
  markers.forEach((m) => m.remove())
  markers = []

  for (const lot of filteredLots.value) {
    const color = getLotMarkerColor(lot)
    const isSelected = selectedLot.value?.id === lot.id
    const icon = createMarkerIcon(color, isSelected)

    const marker = L.marker([lot.latitude, lot.longitude], { icon }).addTo(map)

    marker.bindTooltip(lot.name, {
      direction: 'top',
      offset: [0, -10],
    })

    marker.on('click', () => {
      selectLot(lot)
    })

    markers.push(marker)
  }

  // Fit bounds if we have lots
  if (filteredLots.value.length > 0) {
    const bounds = L.latLngBounds(filteredLots.value.map((lot) => [lot.latitude, lot.longitude] as [number, number]))
    map.fitBounds(bounds.pad(0.15))
  }
}

function selectLot(lot: BuildingLot) {
  selectedLot.value = lot
  purchaseMode.value = false
  purchaseError.value = null
  purchaseSuccess.value = null
  justPurchasedBuildingId.value = null
  justPurchasedBuildingType.value = null
  justPurchasedIsUnderConstruction.value = false
  justPurchasedConstructionCompletesAtTick.value = null
  selectedBuildingType.value = ''
  buildingName.value = ''
  selectedMediaType.value = ''
  selectedPowerPlantType.value = ''

  // Update markers to show selection
  updateMarkers()

  // Pan map to selected lot
  if (map) {
    map.panTo([lot.latitude, lot.longitude])
  }
}

function selectRequestedBuildingLot() {
  const buildingId = highlightedBuildingId.value
  if (!buildingId) return

  const matchingLot = lots.value.find((lot) => lot.buildingId === buildingId)
  if (!matchingLot) return

  if (selectedLot.value?.id === matchingLot.id) {
    if (map) {
      map.panTo([matchingLot.latitude, matchingLot.longitude])
    }
    return
  }

  selectLot(matchingLot)
}

function startPurchase() {
  purchaseMode.value = true
  purchaseError.value = null
  purchaseSuccess.value = null
}

async function confirmPurchase() {
  if (!selectedLot.value || !canSubmitPurchase.value || !activeCompany.value) return

  purchasing.value = true
  purchaseError.value = null

  try {
    const data = await gqlRequest<{ purchaseLot: PurchaseLotResult }>(
      `mutation PurchaseLot($input: PurchaseLotInput!) {
        purchaseLot(input: $input) {
          lot {
            id cityId name description district latitude longitude price suitableTypes
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
          }
          building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
          company { id name cash }
        }
      }`,
      {
        input: {
          companyId: activeCompany.value.id,
          lotId: selectedLot.value.id,
          buildingType: selectedBuildingType.value,
          buildingName: buildingName.value.trim() || null,
          mediaType: selectedBuildingType.value === 'MEDIA_HOUSE' ? selectedMediaType.value || null : null,
          powerPlantType: selectedBuildingType.value === 'POWER_PLANT' ? selectedPowerPlantType.value || null : null,
        },
      },
    )

    // Update the lot in our local state
    const idx = lots.value.findIndex((l) => l.id === data.purchaseLot.lot.id)
    if (idx >= 0) {
      lots.value[idx] = data.purchaseLot.lot
    }
    selectedLot.value = data.purchaseLot.lot

    // Update company cash
    const companyIdx = companies.value.findIndex((c) => c.id === data.purchaseLot.company.id)
    if (companyIdx >= 0) {
      companies.value[companyIdx]!.cash = data.purchaseLot.company.cash
    }

    purchaseSuccess.value = t('cityMap.purchaseSuccess')
    justPurchasedBuildingId.value = data.purchaseLot.building.id
    justPurchasedBuildingType.value = data.purchaseLot.building.type
    justPurchasedIsUnderConstruction.value = data.purchaseLot.building.isUnderConstruction ?? false
    justPurchasedConstructionCompletesAtTick.value = data.purchaseLot.building.constructionCompletesAtTick ?? null
    purchaseMode.value = false
    updateMarkers()
  } catch (e: unknown) {
    if (e instanceof GraphQLError) {
      if (e.code === 'LOT_ALREADY_OWNED') {
        // Stale lot: another player claimed this lot after the player opened the form.
        // Re-fetch just this single lot so the UI reflects new ownership immediately
        // without fetching the full city list.
        purchaseError.value = t('cityMap.purchaseErrorAlreadyOwned')
        purchaseMode.value = false
        try {
          const refreshedLot = await gqlRequest<{ lot: BuildingLot | null }>(
            `query GetLot($id: UUID!) {
              lot(id: $id) {
                id cityId name description district latitude longitude price suitableTypes
                ownerCompanyId buildingId
                ownerCompany { id name }
                building { id name type isUnderConstruction constructionCompletesAtTick constructionCost }
              }
            }`,
            { id: selectedLot.value?.id },
          )
          if (refreshedLot.lot) {
            const idx = lots.value.findIndex((l) => l.id === refreshedLot.lot!.id)
            if (idx >= 0) lots.value[idx] = refreshedLot.lot
            selectedLot.value = refreshedLot.lot
            updateMarkers()
          }
        } catch {
          // Silently ignore refresh errors; the stale-lot error message is already shown
        }
      } else if (e.code === 'INSUFFICIENT_FUNDS') {
        purchaseError.value = t('cityMap.purchaseErrorInsufficientFunds')
      } else if (e.code === 'UNSUITABLE_BUILDING_TYPE') {
        purchaseError.value = t('cityMap.purchaseErrorUnsuitable')
      } else {
        purchaseError.value = e.message
      }
    } else {
      purchaseError.value = e instanceof Error ? e.message : t('cityMap.purchaseError')
    }
  } finally {
    purchasing.value = false
  }
}

watch(filteredLots, () => {
  if (map) {
    updateMarkers()
  }
})

watch(
  () => [highlightedBuildingId.value, lots.value.map((lot) => `${lot.id}:${lot.buildingId ?? ''}`).join('|')],
  () => {
    if (highlightedBuildingId.value) {
      selectRequestedBuildingLot()
    }
  },
)

watch(selectedCityId, (nextCityId) => {
  if (!nextCityId || nextCityId === cityId.value) {
    return
  }
  router.push({ name: 'city-map', params: { id: nextCityId } })
})

// Reload data and reinitialize map when city changes via the picker or back navigation.
// fetchData() handles its own error state (sets error.value), so no extra try-catch needed here.
watch(cityId, async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }
  selectedLot.value = null
  purchaseMode.value = false
  purchaseError.value = null
  purchaseSuccess.value = null
  justPurchasedBuildingId.value = null
  justPurchasedBuildingType.value = null
  cityWeather.value = null
  cityPowerBalance.value = null
  viewMode.value = 'map'
  if (map) {
    map.remove()
    map = null
  }
  await fetchData()
  void fetchMediaHouses()
  void fetchWeatherForecast()
  void fetchCityPowerBalance()
  if (!error.value) {
    await nextTick()
    initMap()
  }
})

onMounted(async () => {
  if (selectedCityId.value !== cityId.value) {
    auth.switchCity(cityId.value)
  }
  await fetchData()
  void fetchMediaHouses()
  void fetchWeatherForecast()
  void fetchCityPowerBalance()
  await nextTick()
  if (viewMode.value === 'map') {
    initMap()
  }
  selectRequestedBuildingLot()
})

onUnmounted(() => {
  if (map) {
    map.remove()
    map = null
  }
})

// Fix blank-map regression: v-show keeps the container in the DOM so we can always
// call invalidateSize() on the existing Leaflet instance without re-initializing.
watch(viewMode, async (mode) => {
  if (mode === 'map') {
    await nextTick()
    if (!map) {
      initMap()
    } else {
      map.invalidateSize()
    }
  }
})
</script>

<style scoped src="./CityMapView.styles.css"></style>

