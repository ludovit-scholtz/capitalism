// NPC competitor pause/resume panel for the Operations Overview screen,
// ported from the NPC management section of
// `projects/frontend/src/views/OperationsOverviewView.vue`.

import 'package:flutter/material.dart';

import 'operations_models.dart';
import 'operations_service.dart';

class OperationsNpcPanel extends StatefulWidget {
  const OperationsNpcPanel({super.key, required this.service});

  final OperationsService service;

  @override
  State<OperationsNpcPanel> createState() => _OperationsNpcPanelState();
}

class _OperationsNpcPanelState extends State<OperationsNpcPanel> {
  bool _loading = true;
  String? _error;
  List<NpcCompanySummary> _npcs = const [];
  final Set<String> _busyIds = {};

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final npcs = await widget.service.fetchNpcCompanies();
      if (!mounted) return;
      setState(() {
        _npcs = npcs;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load NPC companies.';
        _loading = false;
      });
    }
  }

  Future<void> _toggle(NpcCompanySummary npc) async {
    setState(() => _busyIds.add(npc.id));
    try {
      if (npc.isActive) {
        await widget.service.pauseNpcCompany(npc.id);
      } else {
        await widget.service.resumeNpcCompany(npc.id);
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Could not ${npc.isActive ? 'pause' : 'resume'} ${npc.name}.')));
      }
    } finally {
      if (mounted) setState(() => _busyIds.remove(npc.id));
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('NPC Competitors', style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            if (_loading)
              const Center(child: Padding(padding: EdgeInsets.symmetric(vertical: 12), child: CircularProgressIndicator()))
            else if (_error != null)
              Text(_error!, style: TextStyle(color: theme.colorScheme.error))
            else if (_npcs.isEmpty)
              const Text('No NPC companies exist yet.')
            else
              for (final npc in _npcs)
                ListTile(
                  key: ValueKey('npc-${npc.id}'),
                  dense: true,
                  title: Text(npc.name),
                  subtitle: Text('${npc.archetype} · ${npc.homeCityName} · ${npc.buildingCount} buildings'),
                  trailing: FilledButton.tonal(
                    onPressed: _busyIds.contains(npc.id) ? null : () => _toggle(npc),
                    child: Text(npc.isActive ? 'Pause' : 'Resume'),
                  ),
                ),
          ],
        ),
      ),
    );
  }
}
