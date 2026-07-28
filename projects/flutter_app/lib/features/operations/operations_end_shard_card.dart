// "End shard manually" destructive admin action for the Operations
// Overview screen, ported from the end-shard section of
// `projects/frontend/src/views/OperationsOverviewView.vue`. Ends the game
// immediately, records the current wealth leader as the winner, and
// publishes a final newsletter — irreversible, so this always confirms
// with a dialog before calling the mutation.

import 'package:flutter/material.dart';

import 'operations_service.dart';

class OperationsEndShardCard extends StatefulWidget {
  const OperationsEndShardCard({super.key, required this.service});

  final OperationsService service;

  @override
  State<OperationsEndShardCard> createState() => _OperationsEndShardCardState();
}

class _OperationsEndShardCardState extends State<OperationsEndShardCard> {
  bool _ending = false;

  Future<void> _confirmAndEnd() async {
    final reasonController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('End this game shard?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('This immediately ends the game, records the current wealth leader as the winner, and publishes a final newsletter to all players. This cannot be undone.'),
            const SizedBox(height: 12),
            TextField(
              key: const ValueKey('end-shard-reason-field'),
              controller: reasonController,
              decoration: const InputDecoration(labelText: 'Reason (optional)'),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(
            key: const ValueKey('end-shard-confirm-button'),
            style: FilledButton.styleFrom(backgroundColor: Theme.of(dialogContext).colorScheme.error),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('End shard'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _ending = true);
    try {
      final reason = reasonController.text.trim();
      await widget.service.endShardManually(reason: reason.isEmpty ? null : reason);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Shard ended.')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not end the shard.')));
      }
    } finally {
      if (mounted) setState(() => _ending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      color: theme.colorScheme.errorContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Danger zone', style: theme.textTheme.titleSmall?.copyWith(color: theme.colorScheme.onErrorContainer)),
                  Text('End this game shard immediately.', style: TextStyle(color: theme.colorScheme.onErrorContainer)),
                ],
              ),
            ),
            FilledButton(
              key: const ValueKey('end-shard-button'),
              style: FilledButton.styleFrom(backgroundColor: theme.colorScheme.error),
              onPressed: _ending ? null : _confirmAndEnd,
              child: Text(_ending ? 'Ending…' : 'End shard'),
            ),
          ],
        ),
      ),
    );
  }
}
