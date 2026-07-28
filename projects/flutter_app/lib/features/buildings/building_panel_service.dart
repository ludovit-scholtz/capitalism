// GraphQL calls for the building-type-specific management panels
// (Apartment/Commercial property, Power Plant, Research & Development,
// Media House). Split from `building_detail_service.dart` to keep each
// file under the repo's 500-line guideline — this is a sibling service the
// screen uses alongside `BuildingDetailService`, not a replacement for it.

import '../../core/graphql/graphql_service.dart';
import 'building_panel_models.dart';

const _apartmentDetailQuery = r'''
  query ApartmentBuildingDetail($buildingId: UUID!) {
    apartmentBuildingDetail(buildingId: $buildingId) {
      buildingId occupancyPercent totalAreaSqm pricePerSqm pendingPricePerSqm pendingPriceActivationTick
      cityAverageRentPerSqm adjustedMarketRentPerSqm populationIndex currencyCode
      revenueHistory { tick revenue occupancyPercent rentPerSqm }
    }
  }
''';

const _setRentPerSqmMutation = r'''
  mutation SetRentPerSqm($input: SetRentPerSqmInput!) {
    setRentPerSqm(input: $input) { id pricePerSqm pendingPricePerSqm pendingPriceActivationTick }
  }
''';

const _companyBrandsQuery = r'''
  query CompanyBrands($companyId: UUID!) {
    companyBrands(companyId: $companyId) {
      id companyId name scope productTypeId productName industryCategory
      awareness quality marketingQuality marketingEfficiencyMultiplier
      accumulatedResearchBudget baseResearchBudget maxCompetitorBudget
    }
  }
''';

const _powerPlantAnalyticsQuery = r'''
  query PowerPlantAnalytics($buildingId: UUID!, $limit: Int) {
    powerPlantAnalytics(buildingId: $buildingId, limit: $limit) {
      buildingId currentOutputMw dispatchTargetPercent fuelReserveMwh maxFuelReserveMwh
      fuelReservePercent fuelPurchaseCapacityMwhPerTick energyProducingCapacityMw
      fuelConstrainedOutputMw fuelTypeLabel
      totalSurplusIncome totalGridFines totalOperatingCosts totalFuelCosts totalSpotMarketRevenue totalNetProfit
      activeListing { listingId pricePerKwhLocal capacityKw availableKw }
    }
  }
''';

const _cityPowerBalanceQuery = r'''
  query CityPowerBalance($cityId: UUID!) {
    cityPowerBalance(cityId: $cityId) { totalSupplyMw totalDemandMw reserveMw reservePercent status powerPlantCount }
  }
''';

const _setPlantDispatchMutation = r'''
  mutation SetPlantDispatch($input: SetPlantDispatchInput!) {
    setPlantDispatch(input: $input) { id dispatchTargetPercent }
  }
''';

const _setPowerPriorityMutation = r'''
  mutation SetPowerPriority($input: SetPowerPriorityInput!) {
    setPowerPriority(input: $input) { id powerPriority }
  }
''';

const _setMaxEnergyBidPriceMutation = r'''
  mutation SetMaxEnergyBidPrice($input: SetMaxEnergyBidPriceInput!) {
    setMaxEnergyBidPrice(input: $input) { id maxEnergyBidPrice }
  }
''';

const _listEnergyForSaleMutation = r'''
  mutation ListEnergyForSale($input: ListEnergyForSaleInput!) {
    listEnergyForSale(input: $input) { listingId }
  }
''';

const _cancelEnergyListingMutation = r'''
  mutation CancelEnergyListing($input: CancelEnergyListingInput!) {
    cancelEnergyListing(input: $input) { id }
  }
''';

const _cityMediaHousesQuery = r'''
  query CityMediaHouses($cityId: UUID!) {
    cityMediaHouses(cityId: $cityId) {
      id name mediaType ownerCompanyName contentRanking isGovernmentOwned
      effectivenessMultiplier powerStatus isUnderConstruction
    }
  }
''';

const _mediaHouseUnitsQuery = r'''
  query MediaHouseStatsUnits($buildingId: UUID!) {
    mediaHouseStats(buildingId: $buildingId) {
      units { id targetCompanyId targetCompanyName mediaType campaignBudgetPerTick isActive }
    }
  }
''';

const _setMediaHouseContentBudgetMutation = r'''
  mutation SetMediaHouseContentBudget($input: SetMediaHouseContentBudgetInput!) {
    setMediaHouseContentBudget(input: $input) { id contentBudgetPerTick contentValue }
  }
''';

const _upgradeMediaHouseMutation = r'''
  mutation UpgradeMediaHouse($input: UpgradeMediaHouseInput!) {
    upgradeMediaHouse(input: $input) { id level }
  }
''';

const _configureMediaHouseUnitMutation = r'''
  mutation ConfigureMediaHouseUnit($input: ConfigureMediaHouseUnitInput!) {
    configureMediaHouseUnit(input: $input) { id }
  }
''';

// --- Building bank account ---------------------------------------------

const _buildingBankAccountQuery = r'''
  query BuildingBankAccount($buildingId: UUID!) {
    buildingBankAccount(buildingId: $buildingId) {
      buildingId buildingName cityName currencyCode hasBankAccount bankAccountId
      accountNumber balance alertMinBalanceThreshold isSuspendedForFunds suspendedReason
    }
  }
''';

const _setBankAccountAlertThresholdMutation = r'''
  mutation SetBankAccountAlertThreshold($input: SetBankAccountAlertThresholdInput!) {
    setBankAccountAlertThreshold(input: $input) { bankAccountId alertMinBalanceThreshold }
  }
''';

class BuildingPanelService {
  const BuildingPanelService(this._graphQlService);

  final GraphQlService _graphQlService;

  // --- Apartment / Commercial -----------------------------------------

  Future<ApartmentBuildingDetail?> fetchApartmentBuildingDetail(String buildingId) async {
    final result = await _graphQlService.request(_apartmentDetailQuery, variables: {'buildingId': buildingId});
    final detail = result['apartmentBuildingDetail'] as Map<String, dynamic>?;
    return detail == null ? null : ApartmentBuildingDetail.fromJson(detail);
  }

  Future<void> setRentPerSqm({required String buildingId, required double rentPerSqm}) {
    return _graphQlService.request(
      _setRentPerSqmMutation,
      variables: {
        'input': {'buildingId': buildingId, 'rentPerSqm': rentPerSqm},
      },
    );
  }

  // --- Research & Development -------------------------------------------

  Future<List<CompanyBrand>> fetchCompanyBrands(String companyId) async {
    final result = await _graphQlService.request(_companyBrandsQuery, variables: {'companyId': companyId});
    final list = result['companyBrands'] as List<dynamic>? ?? const [];
    return list.map((e) => CompanyBrand.fromJson(e as Map<String, dynamic>)).toList();
  }

  // --- Power Plant --------------------------------------------------------

  Future<PowerPlantAnalytics?> fetchPowerPlantAnalytics(String buildingId) async {
    final result = await _graphQlService.request(_powerPlantAnalyticsQuery, variables: {'buildingId': buildingId, 'limit': 100});
    final analytics = result['powerPlantAnalytics'] as Map<String, dynamic>?;
    return analytics == null ? null : PowerPlantAnalytics.fromJson(analytics);
  }

  Future<CityPowerBalance?> fetchCityPowerBalance(String cityId) async {
    final result = await _graphQlService.request(_cityPowerBalanceQuery, variables: {'cityId': cityId});
    final balance = result['cityPowerBalance'] as Map<String, dynamic>?;
    return balance == null ? null : CityPowerBalance.fromJson(balance);
  }

  Future<void> setPlantDispatch({required String buildingId, required int dispatchTargetPercent}) {
    return _graphQlService.request(
      _setPlantDispatchMutation,
      variables: {
        'input': {'buildingId': buildingId, 'dispatchTargetPercent': dispatchTargetPercent},
      },
    );
  }

  Future<void> setPowerPriority({required String buildingId, required int priority}) {
    return _graphQlService.request(
      _setPowerPriorityMutation,
      variables: {
        'input': {'buildingId': buildingId, 'priority': priority},
      },
    );
  }

  Future<void> setMaxEnergyBidPrice({required String buildingId, required double? maxBidPricePerKwh}) {
    return _graphQlService.request(
      _setMaxEnergyBidPriceMutation,
      variables: {
        'input': {'buildingId': buildingId, 'maxBidPricePerKwh': maxBidPricePerKwh},
      },
    );
  }

  Future<void> listEnergyForSale({required String buildingId, required double pricePerKwhLocal, required double capacityKw}) {
    return _graphQlService.request(
      _listEnergyForSaleMutation,
      variables: {
        'input': {'buildingId': buildingId, 'pricePerKwhLocal': pricePerKwhLocal, 'capacityKw': capacityKw},
      },
    );
  }

  Future<void> cancelEnergyListing(String listingId) {
    return _graphQlService.request(
      _cancelEnergyListingMutation,
      variables: {
        'input': {'listingId': listingId},
      },
    );
  }

  // --- Media House ----------------------------------------------------------

  Future<List<CityMediaHouse>> fetchCityMediaHouses(String cityId) async {
    final result = await _graphQlService.request(_cityMediaHousesQuery, variables: {'cityId': cityId});
    final list = result['cityMediaHouses'] as List<dynamic>? ?? const [];
    return list.map((e) => CityMediaHouse.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<MediaHouseUnitConfig>> fetchMediaHouseUnits(String buildingId) async {
    final result = await _graphQlService.request(_mediaHouseUnitsQuery, variables: {'buildingId': buildingId});
    final stats = result['mediaHouseStats'] as Map<String, dynamic>?;
    final units = stats?['units'] as List<dynamic>? ?? const [];
    return units.map((e) => MediaHouseUnitConfig.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> setMediaHouseContentBudget({required String buildingId, required double contentBudgetPerTick}) {
    return _graphQlService.request(
      _setMediaHouseContentBudgetMutation,
      variables: {
        'input': {'buildingId': buildingId, 'contentBudgetPerTick': contentBudgetPerTick},
      },
    );
  }

  Future<void> upgradeMediaHouse(String buildingId) {
    return _graphQlService.request(
      _upgradeMediaHouseMutation,
      variables: {
        'input': {'buildingId': buildingId},
      },
    );
  }

  Future<void> configureMediaHouseUnit({
    required String buildingId,
    String? unitId,
    required String targetCompanyId,
    required String mediaType,
    required double campaignBudgetPerTick,
    required bool isActive,
  }) {
    return _graphQlService.request(
      _configureMediaHouseUnitMutation,
      variables: {
        'input': {
          'buildingId': buildingId,
          'unitId': unitId,
          'targetCompanyId': targetCompanyId,
          'mediaType': mediaType,
          'campaignBudgetPerTick': campaignBudgetPerTick,
          'isActive': isActive,
        },
      },
    );
  }

  // --- Bank account ---------------------------------------------------------

  Future<BuildingBankAccountInfo?> fetchBuildingBankAccount(String buildingId) async {
    final result = await _graphQlService.request(_buildingBankAccountQuery, variables: {'buildingId': buildingId});
    final info = result['buildingBankAccount'] as Map<String, dynamic>?;
    return info == null ? null : BuildingBankAccountInfo.fromJson(info);
  }

  Future<void> setBankAccountAlertThreshold({required String bankAccountId, required double? minBalanceThreshold}) {
    return _graphQlService.request(
      _setBankAccountAlertThresholdMutation,
      variables: {
        'input': {'bankAccountId': bankAccountId, 'minBalanceThreshold': minBalanceThreshold},
      },
    );
  }
}
