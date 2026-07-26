// "Launch New Company" flow, mirroring `NewCompanyModal.vue` +
// `DashboardMainContent.vue`'s CTA/checklist. Placed on the Overview tab
// since this app's dashboard has no person-account-mode landing page (the
// only place the web version puts this CTA) — see the deviation note below.
//
// Deliberate deviation from web (documented, not a bug-for-bug port):
// after a successful launch, web redirects to `/onboarding?companyId=X`,
// but `OnboardingView.vue` never actually reads that query param and
// `startOnboardingCompany` throws `ONBOARDING_ALREADY_IN_PROGRESS` once a
// player already owns a company — that redirect is dead code on web itself.
// This port redirects to `/buy-building/:companyId` instead, the real,
// already-working path for outfitting any company (first or fifth) with
// lots.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../onboarding/onboarding_models.dart' show IpoPlan, onboardingIpoPlans;
import 'dashboard_models.dart';

class DashboardNewCompanyCard extends StatefulWidget {
  const DashboardNewCompanyCard({super.key, required this.prerequisites, required this.cities, required this.onLaunch});

  /// `null` while the eligibility check is still loading.
  final AdditionalCompanyPrerequisites? prerequisites;
  final List<NewCompanyCity> cities;

  /// Performs the actual `startAdditionalCompany` call and post-launch
  /// navigation. Exceptions propagate back to this widget so the wizard
  /// dialog can show an inline error rather than silently closing.
  final Future<void> Function({required String companyName, required String cityId, required double ipoRaiseTarget}) onLaunch;

  @override
  State<DashboardNewCompanyCard> createState() => _DashboardNewCompanyCardState();
}

class _DashboardNewCompanyCardState extends State<DashboardNewCompanyCard> {
  Future<void> _openWizard() async {
    if (widget.cities.isEmpty) return;

    final nameController = TextEditingController();
    var step = 0;
    String? selectedCityId = widget.cities.first.id;
    double selectedRaiseTarget = onboardingIpoPlans.first.raiseTarget;
    String? error;
    var submitting = false;

    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          Future<void> handlePrimaryAction() async {
            if (step == 0) {
              if (nameController.text.trim().length < 3 || selectedCityId == null) {
                setDialogState(() => error = 'Enter a company name (3+ characters) and choose a city.');
                return;
              }
              setDialogState(() {
                step = 1;
                error = null;
              });
              return;
            }

            setDialogState(() {
              submitting = true;
              error = null;
            });
            try {
              await widget.onLaunch(
                companyName: nameController.text.trim(),
                cityId: selectedCityId!,
                ipoRaiseTarget: selectedRaiseTarget,
              );
              if (dialogContext.mounted) Navigator.of(dialogContext).pop();
            } catch (e) {
              setDialogState(() {
                submitting = false;
                error = e is GraphQlException ? e.message : 'Could not launch the new company. Please try again.';
              });
            }
          }

          return AlertDialog(
            title: Text(step == 0 ? 'Company details' : 'Choose IPO plan'),
            content: SizedBox(
              width: 360,
              child: step == 0
                  ? _NameAndCityStep(
                      key: const Key('new-company-step-details'),
                      nameController: nameController,
                      cities: widget.cities,
                      selectedCityId: selectedCityId,
                      onCityChanged: (value) => setDialogState(() => selectedCityId = value),
                      error: error,
                    )
                  : _IpoPlanStep(
                      key: const Key('new-company-step-ipo'),
                      selectedRaiseTarget: selectedRaiseTarget,
                      onChanged: (value) => setDialogState(() => selectedRaiseTarget = value),
                      error: error,
                    ),
            ),
            actions: [
              TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Cancel')),
              if (step == 1)
                TextButton(
                  onPressed: submitting ? null : () => setDialogState(() => step = 0),
                  child: const Text('Back'),
                ),
              FilledButton(
                onPressed: submitting ? null : handlePrimaryAction,
                child: submitting
                    ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(step == 0 ? 'Next' : 'Launch'),
              ),
            ],
          );
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final prerequisites = widget.prerequisites;

    return Card(
      key: const Key('new-company-card'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Launch a new company', style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text('Expand into a new city with a fresh IPO.', style: theme.textTheme.bodySmall),
            const SizedBox(height: 12),
            if (prerequisites == null)
              const Center(child: Padding(padding: EdgeInsets.all(12), child: CircularProgressIndicator()))
            else ...[
              _PrereqRow(label: 'Oldest company is at least 1 game year old', met: prerequisites.companyAgeRequirementMet),
              _PrereqRow(label: 'Oldest company is profitable', met: prerequisites.profitabilityRequirementMet),
              _PrereqRow(label: 'Personal balance is at least \$200,000', met: prerequisites.balanceRequirementMet),
              _PrereqRow(label: 'Under the maximum company limit', met: prerequisites.underMaxCap),
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: prerequisites.allRequirementsMet ? _openWizard : null,
                  child: const Text('Launch New Company'),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _PrereqRow extends StatelessWidget {
  const _PrereqRow({required this.label, required this.met});

  final String label;
  final bool met;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          FaIcon(met ? AppIcons.checkCircle : AppIcons.warning, size: 14, color: met ? Colors.green : Colors.amber),
          const SizedBox(width: 8),
          Expanded(child: Text(label, style: Theme.of(context).textTheme.bodySmall)),
        ],
      ),
    );
  }
}

class _NameAndCityStep extends StatelessWidget {
  const _NameAndCityStep({
    super.key,
    required this.nameController,
    required this.cities,
    required this.selectedCityId,
    required this.onCityChanged,
    this.error,
  });

  final TextEditingController nameController;
  final List<NewCompanyCity> cities;
  final String? selectedCityId;
  final ValueChanged<String?> onCityChanged;
  final String? error;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(controller: nameController, decoration: const InputDecoration(labelText: 'Company name')),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          initialValue: selectedCityId,
          decoration: const InputDecoration(labelText: 'City'),
          items: [for (final city in cities) DropdownMenuItem(value: city.id, child: Text(city.name))],
          onChanged: onCityChanged,
        ),
        if (error != null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ),
      ],
    );
  }
}

class _IpoPlanStep extends StatelessWidget {
  const _IpoPlanStep({super.key, required this.selectedRaiseTarget, required this.onChanged, this.error});

  final double selectedRaiseTarget;
  final ValueChanged<double> onChanged;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final IpoPlan plan in onboardingIpoPlans)
          Card(
            key: ValueKey('new-company-ipo-${plan.raiseTarget}'),
            color: selectedRaiseTarget == plan.raiseTarget ? theme.colorScheme.primaryContainer : null,
            child: ListTile(
              selected: selectedRaiseTarget == plan.raiseTarget,
              leading: FaIcon(
                selectedRaiseTarget == plan.raiseTarget ? AppIcons.radioChecked : AppIcons.radioUnchecked,
                size: 18,
              ),
              title: Text(plan.label),
              subtitle: Text(
                'Raise \$${plan.raiseTarget.toStringAsFixed(0)} · you keep ${(plan.founderOwnershipRatio * 100).toStringAsFixed(0)}% ownership',
              ),
              onTap: () => onChanged(plan.raiseTarget),
            ),
          ),
        if (error != null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(error!, style: TextStyle(color: theme.colorScheme.error)),
          ),
      ],
    );
  }
}
