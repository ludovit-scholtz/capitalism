// Dashboard Pro tab, mirroring `DashboardMainContent.vue`'s inline
// `pro-tab-panel` section: an active/inactive status badge derived from
// `proSubscriptionEndsAtUtc` (same expiry-date check as web's
// `auth.isProSubscriber`), 4 static benefit cards, and a deep link out to
// the separate master-web portal (this app doesn't sell Pro itself, same as
// web — it only reflects/links out to it).

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/config/app_config.dart';
import '../../core/services/url_opener.dart';
import '../../core/theme/app_icons.dart';

class DashboardProPanel extends StatelessWidget {
  const DashboardProPanel({super.key, required this.proSubscriptionEndsAtUtc, this.urlOpener = const ExternalUrlOpener()});

  /// Raw ISO timestamp from `me.proSubscriptionEndsAtUtc`, or `null` if the
  /// player has never subscribed (or it hasn't loaded yet).
  final String? proSubscriptionEndsAtUtc;

  final UrlOpener urlOpener;

  bool get _isActive {
    final endsAt = proSubscriptionEndsAtUtc;
    if (endsAt == null) return false;
    final parsed = DateTime.tryParse(endsAt);
    return parsed != null && parsed.isAfter(DateTime.now().toUtc());
  }

  String _formatDate(String iso) {
    final parsed = DateTime.tryParse(iso);
    if (parsed == null) return iso;
    return '${parsed.year}-${parsed.month.toString().padLeft(2, '0')}-${parsed.day.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final active = _isActive;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Row(
          children: [
            Text('Pro access', style: theme.textTheme.headlineSmall),
            const SizedBox(width: 12),
            Chip(
              key: const Key('pro-status-chip'),
              label: Text(active ? 'Active' : 'Inactive'),
              backgroundColor: (active ? Colors.green : Colors.grey).withValues(alpha: 0.2),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Text(
          active
              ? 'Pro is active on your account until ${_formatDate(proSubscriptionEndsAtUtc!)}.'
              : 'You do not have an active Pro subscription.',
          style: theme.textTheme.bodyMedium,
        ),
        const SizedBox(height: 24),
        Text('What you unlock with Pro', style: theme.textTheme.titleMedium),
        const SizedBox(height: 12),
        const _BenefitCard(
          icon: AppIcons.sell,
          title: 'Products',
          body: 'Access every Pro-only product line across all industries.',
        ),
        const _BenefitCard(
          icon: AppIcons.factory,
          title: 'Advanced industries',
          body: 'Unlock advanced industries not available to free accounts.',
        ),
        const _BenefitCard(
          icon: AppIcons.checkCircle,
          title: 'No restrictions',
          body: 'Skip the caps and gating that apply to free-tier players.',
        ),
        const _BenefitCard(
          icon: AppIcons.upgrade,
          title: 'Priority',
          body: 'Get priority treatment across supported game features.',
        ),
        const SizedBox(height: 16),
        Text('Manage your subscription from the master portal.', style: theme.textTheme.bodySmall),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: () => urlOpener.open(AppConfig.masterWebUrl),
          icon: const FaIcon(AppIcons.arrowRight, size: 14),
          label: const Text('Open Portal'),
        ),
      ],
    );
  }
}

class _BenefitCard extends StatelessWidget {
  const _BenefitCard({required this.icon, required this.title, required this.body});

  final FaIconData icon;
  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            FaIcon(icon, size: 18),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: theme.textTheme.titleSmall),
                  const SizedBox(height: 2),
                  Text(body, style: theme.textTheme.bodySmall),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
