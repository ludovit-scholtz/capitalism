// Ported from `projects/frontend/src/views/OnboardingView.vue`. Real
// district-name/population-index lot recommendations (`onboarding_recommendation.dart`),
// wordlist-based company/person name generation (`onboarding_company_name.dart`,
// `onboarding_personal_name.dart`), FX-accurate currency conversion
// (`onboarding_fx.dart`), real Pro-subscription gating (`_visibleIndustries`,
// filtering Pro-only industries out of the list entirely for guests/non-Pro
// players rather than showing-then-blocking), the auto-polled
// first-sale-mission celebration panel (tick-driven via the app-wide
// `GameStateState`, same silent-refresh pattern as `GameStatusBar`), and the
// interactive map-based lot picker (`OnboardingLotStep`, `CapitalismMapView`)
// are now ported to match web.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart' show TileProvider;
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/auth/biatec_oidc_service.dart';
import '../../core/game_state/game_state_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'onboarding_company_name.dart';
import 'onboarding_complete_step.dart';
import 'onboarding_fx.dart';
import 'onboarding_models.dart';
import 'onboarding_personal_name.dart';
import 'onboarding_service.dart';
import 'onboarding_steps.dart';

T? _firstWhereOrNull<T>(Iterable<T> items, bool Function(T) test) {
  for (final item in items) {
    if (test(item)) return item;
  }
  return null;
}

String? friendlyOnboardingError(String? code) {
  switch (code) {
    case 'LOT_ALREADY_OWNED':
      return 'That lot was just taken by someone else. Please choose another.';
    case 'INSUFFICIENT_FUNDS':
    case 'INSUFFICIENT_LOCAL_CURRENCY_FUNDS':
      return 'You do not have enough cash for this choice.';
    case 'PRO_SUBSCRIPTION_REQUIRED':
      return 'This requires a Pro subscription.';
    case 'ONBOARDING_ALREADY_COMPLETED':
      return 'Onboarding is already complete for this account.';
    case 'ONBOARDING_ALREADY_IN_PROGRESS':
      return 'Onboarding is already in progress for this account.';
    default:
      return null;
  }
}

class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({
    super.key,
    GraphQlService? graphQlService,
    OnboardingService? onboardingService,
    this.oidcService = const BiatecOidcService(),
    String? initialCompanyName,
    String? initialPersonalAccountName,
    this.tileProvider,
  }) : _injectedGraphQlService = graphQlService,
       _injectedOnboardingService = onboardingService,
       _initialCompanyName = initialCompanyName,
       _initialPersonalAccountName = initialPersonalAccountName;

  final GraphQlService? _injectedGraphQlService;
  final OnboardingService? _injectedOnboardingService;
  final BiatecOidcService oidcService;
  final String? _initialCompanyName;
  final String? _initialPersonalAccountName;

  /// Injectable so widget tests never hit real OSM tile servers — see
  /// `test/support/fake_tile_provider.dart`.
  final TileProvider? tileProvider;

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  late GraphQlService _graphQlService;
  late OnboardingService _service;
  late bool _isGuestMode;
  late String _companyName;
  late final String _personalAccountName;
  late final bool _companyNameInjected;

  int _step = 1;
  bool _loadingCatalog = true;
  String? _loadError;

  List<OnboardingCity> _cities = const [];
  StarterIndustries _starterIndustries = const StarterIndustries(industries: [], proOnlyIndustries: []);
  List<OnboardingProductType> _products = const [];
  List<CityLot> _cityLots = const [];
  OnboardingFxRates _fxRates = OnboardingFxRates.empty;

  String? _selectedCityId;
  String? _selectedIndustry;
  String? _selectedProductId;
  double? _selectedIpoRaiseTarget;
  String? _selectedFactoryLotId;
  String? _selectedShopLotId;

  double _companyCash = 0;
  bool _submittingFactory = false;
  bool _submittingShop = false;
  bool _savingProgress = false;
  bool _completingMilestone = false;
  bool _milestoneCompleted = false;
  String? _stepError;
  String? _proSubscriptionEndsAtUtc;

  OnboardingCompletionResult? _completionResult;
  FirstSaleMissionStatus? _firstSaleMission;

  late final GameStateState _gameStateState;
  int? _lastSeenTick;

  /// Mirrors web's `auth.isProSubscriber`: a non-null expiry strictly in the
  /// future. Guests (no resume state fetched at all) are never subscribers.
  bool get _isProSubscriber {
    final endsAt = _proSubscriptionEndsAtUtc;
    if (endsAt == null) return false;
    final parsed = DateTime.tryParse(endsAt);
    return parsed != null && parsed.isAfter(DateTime.now().toUtc());
  }

  /// Industries offered to the player: Pro-only industries are filtered out
  /// entirely for guests and non-Pro authenticated players (never shown,
  /// not shown-then-blocked), matching web's `visibleIndustries`.
  List<String> get _visibleIndustries => _isProSubscriber
      ? _starterIndustries.industries
      : _starterIndustries.industries.where((i) => !_starterIndustries.proOnlyIndustries.contains(i)).toList();

  String get _cityCurrencyCode => _selectedCity?.currencyCode ?? 'USD';

  /// Formats an amount already denominated in the selected city's currency
  /// (lot prices, company cash) — no conversion, symbol/decimals only.
  String _formatLocal(double amount) => formatOnboardingCurrency(amount, _cityCurrencyCode);

  /// Converts a USD-nominal amount (product base prices, IPO plan figures)
  /// into the selected city's currency, then formats it. Mirrors web's
  /// `getProductLocalPrice`/`cityUsdFxRate`-based display computeds.
  String _formatUsd(double usdAmount, {bool wholeUnits = false}) =>
      formatOnboardingCurrency(_fxRates.usdToLocal(usdAmount, _cityCurrencyCode, wholeUnits: wholeUnits), _cityCurrencyCode);

  /// Starting cash converted into the selected city's currency — mirrors
  /// web's `companyStartingCash`. Must stay local-currency-denominated since
  /// it's compared directly against `lot.price`, which the backend already
  /// generates in local currency (`LandService.ComputeAppraisedPrice`).
  double get _startingCash =>
      _fxRates.usdToLocal(onboardingFounderContribution + (_selectedIpoRaiseTarget ?? 200000), _cityCurrencyCode, wholeUnits: true);

  OnboardingCity? get _selectedCity => _selectedCityId == null
      ? null
      : _firstWhereOrNull(_cities, (c) => c.id == _selectedCityId);

  CityLot? get _selectedFactoryLot => _selectedFactoryLotId == null
      ? null
      : _firstWhereOrNull(_cityLots, (l) => l.id == _selectedFactoryLotId);

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedOnboardingService ?? OnboardingService(_graphQlService);
    _isGuestMode = !auth.isAuthenticated;
    _companyNameInjected = widget._initialCompanyName != null;
    // No industry is selected yet at this point — this is a placeholder,
    // regenerated in `_selectIndustry` once the industry (and thus the
    // themed wordlist) is known, mirroring web's reset-on-industry-change
    // behavior (`resetNameSession` watcher in `OnboardingView.vue`).
    _companyName = widget._initialCompanyName ?? generateOnboardingCompanyName('');
    _personalAccountName = widget._initialPersonalAccountName ?? generatePersonalAccountName();
    _gameStateState = context.read<GameStateState>();
    _gameStateState.addListener(_onGameStateChanged);
    _bootstrap();
  }

  @override
  void dispose() {
    _gameStateState.removeListener(_onGameStateChanged);
    super.dispose();
  }

  /// Mirrors web's `useTickRefresh`-driven first-sale-mission poll
  /// (`OnboardingView.vue`): only while on the completion step, for an
  /// authenticated player, before the milestone is already marked complete.
  /// `GameStatusBar` (mounted in `AppShell`'s app bar whenever authenticated)
  /// owns the actual tick-polling timer — this only listens for the
  /// resulting `currentTick` changes, exactly like `GameStatusBar`'s own
  /// `_onGameStateChanged`.
  void _onGameStateChanged() {
    final tick = _gameStateState.gameState?.currentTick;
    if (tick == null || tick == _lastSeenTick) return;
    _lastSeenTick = tick;
    if (_step != 7 || _isGuestMode || _milestoneCompleted) return;
    unawaited(_refreshFirstSaleMission());
  }

  Future<void> _refreshFirstSaleMission() async {
    try {
      final mission = await _service.fetchFirstSaleMission();
      if (!mounted) return;
      setState(() => _firstSaleMission = mission);
      if (mission.phase == 'FIRST_SALE_RECORDED' && !_milestoneCompleted && !_completingMilestone) {
        await _markMilestoneComplete();
      }
    } catch (_) {
      // Best-effort silent refresh — a transient failure here shouldn't
      // interrupt the player or show an error, mirroring web's tick-refresh
      // error isolation.
    }
  }

  Future<void> _bootstrap() async {
    setState(() {
      _loadingCatalog = true;
      _loadError = null;
    });
    try {
      OnboardingResumeState? resume;
      if (!_isGuestMode) {
        resume = await _service.fetchResumeState();
      }

      if (resume != null &&
          (resume.onboardingFirstSaleCompletedAtUtc != null ||
              (resume.onboardingCompletedAtUtc != null && resume.onboardingShopBuildingId == null))) {
        if (mounted) context.go('/dashboard');
        return;
      }

      final cities = await _service.fetchCities();
      final starterIndustries = await _service.fetchStarterIndustries();
      // Best-effort: a failed FX fetch falls back to `OnboardingFxRates.empty`
      // (identity conversion, i.e. plain USD figures), not a fatal load error.
      final fxRates = await _service.fetchFxRates().catchError((_) => OnboardingFxRates.empty);

      String? resumedCityId;
      String? resumedIndustry;
      String? resumedFactoryLotId;
      var resumedStep = _step;
      var resumedMilestoneCompleted = false;

      if (resume != null) {
        if (resume.onboardingCompletedAtUtc != null && resume.onboardingShopBuildingId != null) {
          resumedStep = 7;
        } else if (resume.onboardingCurrentStep == 'SHOP_SELECTION') {
          resumedIndustry = resume.onboardingIndustry;
          resumedCityId = resume.onboardingCityId;
          resumedFactoryLotId = resume.onboardingFactoryLotId;
          resumedStep = 6;
        }
      }

      var cityLots = const <CityLot>[];
      if (resumedCityId != null) {
        cityLots = await _service.fetchCityLots(resumedCityId);
      }
      var products = const <OnboardingProductType>[];
      if (resumedIndustry != null) {
        products = await _service.fetchProducts(resumedIndustry);
      }

      if (!mounted) return;
      setState(() {
        _cities = cities;
        _starterIndustries = starterIndustries;
        _fxRates = fxRates;
        _cityLots = cityLots;
        _products = products;
        if (resumedCityId != null) _selectedCityId = resumedCityId;
        if (resumedIndustry != null) _selectedIndustry = resumedIndustry;
        if (resumedFactoryLotId != null) _selectedFactoryLotId = resumedFactoryLotId;
        _proSubscriptionEndsAtUtc = resume?.proSubscriptionEndsAtUtc;
        _step = resumedStep;
        _milestoneCompleted = resumedMilestoneCompleted;
        _loadingCatalog = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadError = 'Could not load onboarding data. Please try again.';
        _loadingCatalog = false;
      });
    }
  }

  void _selectCity(String cityId) {
    setState(() {
      _selectedCityId = cityId;
      _selectedFactoryLotId = null;
      _selectedShopLotId = null;
      _step = 2;
      _stepError = null;
    });
  }

  void _selectIndustry(String industry) {
    // Defense-in-depth only: `_visibleIndustries` already excludes Pro-only
    // industries for non-subscribers, so this should be unreachable through
    // normal UI interaction — kept in case a subscription lapses between
    // list render and tap.
    if (!_isProSubscriber && _starterIndustries.proOnlyIndustries.contains(industry)) {
      setState(() => _stepError = friendlyOnboardingError('PRO_SUBSCRIPTION_REQUIRED'));
      return;
    }
    setState(() {
      _selectedIndustry = industry;
      if (!_companyNameInjected) {
        resetCompanyNameSession('$industry:${_selectedCityId ?? ''}');
        _companyName = generateOnboardingCompanyName(industry);
      }
      _selectedProductId = null;
      _selectedIpoRaiseTarget = null;
      _step = 3;
      _stepError = null;
    });
    _loadProducts(industry);
  }

  Future<void> _loadProducts(String industry) async {
    try {
      final all = await _service.fetchProducts(industry);
      final slugs = starterProductSlugsByIndustry[industry] ?? const [];
      final starterOnly = all.where((p) => slugs.contains(p.slug)).toList();
      if (!mounted) return;
      setState(() => _products = starterOnly.isNotEmpty ? starterOnly : all);
    } catch (_) {
      if (mounted) setState(() => _stepError = 'Could not load products for this industry.');
    }
  }

  void _selectProduct(String productId) {
    setState(() {
      _selectedProductId = productId;
      _step = 4;
      _stepError = null;
    });
  }

  void _selectIpoPlan(double raiseTarget) {
    setState(() {
      _selectedIpoRaiseTarget = raiseTarget;
      _step = 5;
      _stepError = null;
    });
    _loadCityLots();
  }

  Future<void> _loadCityLots() async {
    final cityId = _selectedCityId;
    if (cityId == null) return;
    try {
      final lots = await _service.fetchCityLots(cityId);
      if (!mounted) return;
      setState(() => _cityLots = lots);
    } catch (_) {
      if (mounted) setState(() => _stepError = 'Could not load building lots for this city.');
    }
  }

  void _selectFactoryLot(String lotId) => setState(() => _selectedFactoryLotId = lotId);

  void _selectShopLot(String lotId) => setState(() => _selectedShopLotId = lotId);

  Future<void> _purchaseFactory() async {
    final lotId = _selectedFactoryLotId;
    if (lotId == null) return;
    setState(() {
      _submittingFactory = true;
      _stepError = null;
    });
    try {
      if (_isGuestMode) {
        final lot = _firstWhereOrNull(_cityLots, (l) => l.id == lotId);
        final price = lot?.price ?? 0;
        if (!mounted) return;
        setState(() {
          _companyCash = _startingCash - price;
          _cityLots = _cityLots.map((l) => l.id == lotId ? l.copyAsOwned() : l).toList();
          _step = 6;
        });
      } else {
        final result = await _service.startOnboardingCompany(
          industry: _selectedIndustry!,
          cityId: _selectedCityId!,
          companyName: _companyName,
          factoryLotId: lotId,
          ipoRaiseTarget: _selectedIpoRaiseTarget ?? 200000,
        );
        if (!mounted) return;
        setState(() {
          _companyCash = result.companyCash;
          _step = 6;
        });
      }
    } on GraphQlException catch (e) {
      if (!mounted) return;
      setState(() => _stepError = friendlyOnboardingError(e.code) ?? e.message);
      if (e.code == 'LOT_ALREADY_OWNED') {
        setState(() => _selectedFactoryLotId = null);
        await _loadCityLots();
      }
    } finally {
      if (mounted) setState(() => _submittingFactory = false);
    }
  }

  Future<void> _purchaseShop() async {
    final lotId = _selectedShopLotId;
    if (lotId == null) return;
    setState(() {
      _submittingShop = true;
      _stepError = null;
    });
    try {
      if (_isGuestMode) {
        final lot = _firstWhereOrNull(_cityLots, (l) => l.id == lotId);
        final price = lot?.price ?? 0;
        final product = _firstWhereOrNull(_products, (p) => p.id == _selectedProductId);
        if (!mounted) return;
        setState(() {
          _companyCash -= price;
          _cityLots = _cityLots.map((l) => l.id == lotId ? l.copyAsOwned() : l).toList();
          _completionResult = OnboardingCompletionResult(
            companyName: _companyName,
            companyCash: _companyCash,
            factoryId: 'guest-factory',
            salesShopId: 'guest-shop',
            selectedProductName: product?.name ?? '',
            cityCurrencyCode: _selectedCity?.currencyCode ?? 'USD',
          );
          _step = 7;
        });
      } else {
        final result = await _service.finishOnboarding(productTypeId: _selectedProductId!, shopLotId: lotId);
        if (!mounted) return;
        setState(() {
          _completionResult = result;
          _step = 7;
        });
      }
    } on GraphQlException catch (e) {
      if (!mounted) return;
      setState(() => _stepError = friendlyOnboardingError(e.code) ?? e.message);
      if (e.code == 'LOT_ALREADY_OWNED') {
        setState(() => _selectedShopLotId = null);
        await _loadCityLots();
      }
    } finally {
      if (mounted) setState(() => _submittingShop = false);
    }
  }

  Future<void> _saveProgress() async {
    setState(() {
      _savingProgress = true;
      _stepError = null;
    });
    final auth = context.read<AuthState>();
    try {
      final oidcResult = await widget.oidcService.signIn();
      await auth.setToken(oidcResult.token);

      final authedGraphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
      final authedService = widget._injectedOnboardingService ?? OnboardingService(authedGraphQlService);

      final startResult = await authedService.startOnboardingCompany(
        industry: _selectedIndustry!,
        cityId: _selectedCityId!,
        companyName: _companyName,
        factoryLotId: _selectedFactoryLotId!,
        ipoRaiseTarget: _selectedIpoRaiseTarget ?? 200000,
      );
      final finishResult = await authedService.finishOnboarding(
        productTypeId: _selectedProductId!,
        shopLotId: _selectedShopLotId!,
      );

      if (!mounted) return;
      setState(() {
        _graphQlService = authedGraphQlService;
        _service = authedService;
        _isGuestMode = false;
        _companyCash = startResult.companyCash;
        _completionResult = finishResult;
      });
    } on BiatecOidcException catch (e) {
      if (!mounted) return;
      setState(() => _stepError = e.message);
    } on GraphQlException catch (e) {
      if (!mounted) return;
      if (e.code == 'LOT_ALREADY_OWNED') {
        setState(() {
          _stepError = 'One of your selected lots was taken while you were signing in. Please choose again.';
          _selectedFactoryLotId = null;
          _selectedShopLotId = null;
          _step = 1;
        });
      } else {
        setState(() => _stepError = friendlyOnboardingError(e.code) ?? e.message);
      }
    } finally {
      if (mounted) setState(() => _savingProgress = false);
    }
  }

  Future<void> _markMilestoneComplete() async {
    setState(() {
      _completingMilestone = true;
      _stepError = null;
    });
    try {
      await _service.completeFirstSaleMilestone();
      if (!mounted) return;
      setState(() => _milestoneCompleted = true);
    } on GraphQlException catch (e) {
      if (!mounted) return;
      setState(
        () => _stepError = e.code == 'FIRST_SALE_NOT_RECORDED'
            ? 'No sale has been recorded yet — configure your shop and wait for a sale.'
            : e.message,
      );
    } finally {
      if (mounted) setState(() => _completingMilestone = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loadingCatalog) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_loadError != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_loadError!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _bootstrap, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    switch (_step) {
      case 1:
        return OnboardingCityStep(cities: _cities, onSelect: _selectCity, error: _stepError);
      case 2:
        return OnboardingIndustryStep(
          industries: _visibleIndustries,
          proOnlyIndustries: _isProSubscriber ? _starterIndustries.proOnlyIndustries : const [],
          onSelect: _selectIndustry,
          error: _stepError,
        );
      case 3:
        return OnboardingProductStep(
          products: _products,
          onSelect: _selectProduct,
          formatBasePrice: (basePrice) => _formatUsd(basePrice),
          error: _stepError,
        );
      case 4:
        return OnboardingIpoStep(onSelect: _selectIpoPlan, formatUsdWhole: (amount) => _formatUsd(amount, wholeUnits: true), error: _stepError);
      case 5:
        return OnboardingLotStep(
          title: 'Purchase your factory lot',
          subtitle: '$_companyName (owner: $_personalAccountName) · starting cash ${_formatLocal(_startingCash)}',
          lots: _cityLots,
          buildingType: 'FACTORY',
          availableCash: _startingCash,
          selectedLotId: _selectedFactoryLotId,
          onSelect: _selectFactoryLot,
          onPurchase: _purchaseFactory,
          purchaseLabel: 'Purchase Factory',
          submitting: _submittingFactory,
          formatAmount: _formatLocal,
          tileProvider: widget.tileProvider,
          error: _stepError,
        );
      case 6:
        return OnboardingLotStep(
          title: 'Purchase your sales shop lot',
          subtitle: 'Factory purchased · available cash ${_formatLocal(_companyCash)}',
          lots: _cityLots,
          buildingType: 'SALES_SHOP',
          availableCash: _companyCash,
          selectedLotId: _selectedShopLotId,
          onSelect: _selectShopLot,
          onPurchase: _purchaseShop,
          purchaseLabel: 'Purchase Shop',
          submitting: _submittingShop,
          referenceLot: _selectedFactoryLot,
          formatAmount: _formatLocal,
          tileProvider: widget.tileProvider,
          error: _stepError,
        );
      case 7:
      default:
        return OnboardingCompleteStep(
          isGuestMode: _isGuestMode,
          result: _completionResult,
          onSaveProgress: _saveProgress,
          onMarkMilestoneComplete: _markMilestoneComplete,
          savingProgress: _savingProgress,
          completingMilestone: _completingMilestone,
          milestoneCompleted: _milestoneCompleted,
          formatMoney: formatOnboardingCurrency,
          missionStatus: _firstSaleMission,
          error: _stepError,
        );
    }
  }
}
