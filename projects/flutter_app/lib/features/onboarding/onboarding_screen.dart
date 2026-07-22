// Ported from `projects/frontend/src/views/OnboardingView.vue`. Deliberately
// trimmed from the web version (documented at each point below and in
// ROADMAP.md): a list-based lot picker instead of the interactive map, a
// simplified two-cheapest-unowned-lots "recommended" heuristic instead of
// the district-name-regex + population-index rules, no FX-precise currency
// conversion (USD-equivalent figures shown directly), simple local
// company/person name generation instead of the exact web wordlists, and no
// auto-polled first-sale-mission celebration panel.

import 'dart:math';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/auth/biatec_oidc_service.dart';
import '../../core/graphql/graphql_service.dart';
import 'onboarding_complete_step.dart';
import 'onboarding_models.dart';
import 'onboarding_service.dart';
import 'onboarding_steps.dart';

T? _firstWhereOrNull<T>(Iterable<T> items, bool Function(T) test) {
  for (final item in items) {
    if (test(item)) return item;
  }
  return null;
}

String _generateCompanyName() {
  const adjectives = ['Northgate', 'Silverline', 'Ironbridge', 'Bluewater', 'Redstone', 'Golden Valley'];
  const suffixes = ['Holdings', 'Industries', 'Group', 'Ventures', 'Co.'];
  final rand = Random();
  return '${adjectives[rand.nextInt(adjectives.length)]} ${suffixes[rand.nextInt(suffixes.length)]}';
}

String _generatePersonalAccountName() {
  const firstNames = ['Alex', 'Jordan', 'Sam', 'Taylor', 'Morgan', 'Casey'];
  const lastNames = ['Carter', 'Reed', 'Bennett', 'Hayes', 'Brooks', 'Foster'];
  final rand = Random();
  return '${firstNames[rand.nextInt(firstNames.length)]} ${lastNames[rand.nextInt(lastNames.length)]}';
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
  }) : _injectedGraphQlService = graphQlService,
       _injectedOnboardingService = onboardingService,
       _initialCompanyName = initialCompanyName,
       _initialPersonalAccountName = initialPersonalAccountName;

  final GraphQlService? _injectedGraphQlService;
  final OnboardingService? _injectedOnboardingService;
  final BiatecOidcService oidcService;
  final String? _initialCompanyName;
  final String? _initialPersonalAccountName;

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  late GraphQlService _graphQlService;
  late OnboardingService _service;
  late bool _isGuestMode;
  late final String _companyName;
  late final String _personalAccountName;

  int _step = 1;
  bool _loadingCatalog = true;
  String? _loadError;

  List<OnboardingCity> _cities = const [];
  StarterIndustries _starterIndustries = const StarterIndustries(industries: [], proOnlyIndustries: []);
  List<OnboardingProductType> _products = const [];
  List<CityLot> _cityLots = const [];

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

  OnboardingCompletionResult? _completionResult;

  double get _startingCash => onboardingFounderContribution + (_selectedIpoRaiseTarget ?? 200000);

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
    _companyName = widget._initialCompanyName ?? _generateCompanyName();
    _personalAccountName = widget._initialPersonalAccountName ?? _generatePersonalAccountName();
    _bootstrap();
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
        _cityLots = cityLots;
        _products = products;
        if (resumedCityId != null) _selectedCityId = resumedCityId;
        if (resumedIndustry != null) _selectedIndustry = resumedIndustry;
        if (resumedFactoryLotId != null) _selectedFactoryLotId = resumedFactoryLotId;
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
    if (_starterIndustries.proOnlyIndustries.contains(industry)) {
      setState(() => _stepError = friendlyOnboardingError('PRO_SUBSCRIPTION_REQUIRED'));
      return;
    }
    setState(() {
      _selectedIndustry = industry;
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
          industries: _starterIndustries.industries,
          proOnlyIndustries: _starterIndustries.proOnlyIndustries,
          onSelect: _selectIndustry,
          error: _stepError,
        );
      case 3:
        return OnboardingProductStep(products: _products, onSelect: _selectProduct, error: _stepError);
      case 4:
        return OnboardingIpoStep(onSelect: _selectIpoPlan, error: _stepError);
      case 5:
        return OnboardingLotStep(
          title: 'Purchase your factory lot',
          subtitle: '$_companyName (owner: $_personalAccountName) · starting cash \$${_startingCash.toStringAsFixed(0)}',
          lots: _cityLots,
          buildingType: 'FACTORY',
          availableCash: _startingCash,
          selectedLotId: _selectedFactoryLotId,
          onSelect: _selectFactoryLot,
          onPurchase: _purchaseFactory,
          purchaseLabel: 'Purchase Factory',
          submitting: _submittingFactory,
          error: _stepError,
        );
      case 6:
        return OnboardingLotStep(
          title: 'Purchase your sales shop lot',
          subtitle: 'Factory purchased · available cash \$${_companyCash.toStringAsFixed(0)}',
          lots: _cityLots,
          buildingType: 'SALES_SHOP',
          availableCash: _companyCash,
          selectedLotId: _selectedShopLotId,
          onSelect: _selectShopLot,
          onPurchase: _purchaseShop,
          purchaseLabel: 'Purchase Shop',
          submitting: _submittingShop,
          referenceLot: _selectedFactoryLot,
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
          error: _stepError,
        );
    }
  }
}
