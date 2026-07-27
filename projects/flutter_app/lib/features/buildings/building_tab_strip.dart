// Shared tab-strip widget backing every tab system on the Building Detail
// screen — the building-level Overview/Supply Chain/Bank Account tabs, the
// selected-unit view-mode tabs, the selected-unit edit-mode tabs, and the
// outer edit-mode Basic Data/Energy/Bank Account/Layouts tabs. Mirrors the
// web's shared `unit-tab-btn`/`role="tablist"` pill pattern used identically
// by `BuildingOverviewSidebar.vue`, `BuildingReadonlySidebar.vue`, and
// `UnitConfigurationTabView.vue` — one widget here instead of four bespoke
// `TabBar`s. Uses a horizontally-scrollable row of `ChoiceChip`s rather than
// `TabBar`/`TabController` so tab sets can change shape at runtime (e.g. the
// Supply Chain tab only appears for `FACTORY` once data has loaded) without
// juggling a `TickerProvider`-backed controller's `length`.

import 'package:flutter/material.dart';

class BuildingTab {
  const BuildingTab({required this.key, required this.label, required this.builder});

  final String key;
  final String label;
  final WidgetBuilder builder;
}

class BuildingTabStrip extends StatefulWidget {
  const BuildingTabStrip({super.key, required this.tabs, this.initialKey, this.onTabChanged});

  final List<BuildingTab> tabs;
  final String? initialKey;
  final ValueChanged<String>? onTabChanged;

  @override
  State<BuildingTabStrip> createState() => _BuildingTabStripState();
}

class _BuildingTabStripState extends State<BuildingTabStrip> {
  late String _selectedKey;

  @override
  void initState() {
    super.initState();
    _selectedKey = widget.initialKey ?? (widget.tabs.isNotEmpty ? widget.tabs.first.key : '');
  }

  @override
  void didUpdateWidget(covariant BuildingTabStrip oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.tabs.isEmpty) return;
    if (!widget.tabs.any((tab) => tab.key == _selectedKey)) {
      setState(() => _selectedKey = widget.tabs.first.key);
    }
  }

  void _select(String key) {
    setState(() => _selectedKey = key);
    widget.onTabChanged?.call(key);
  }

  @override
  Widget build(BuildContext context) {
    if (widget.tabs.isEmpty) return const SizedBox.shrink();
    final active = widget.tabs.firstWhere((tab) => tab.key == _selectedKey, orElse: () => widget.tabs.first);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final tab in widget.tabs)
                Padding(
                  padding: const EdgeInsets.only(right: 6),
                  child: ChoiceChip(
                    key: ValueKey('building-tab-${tab.key}'),
                    label: Text(tab.label),
                    selected: active.key == tab.key,
                    onSelected: (_) => _select(tab.key),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        Builder(key: ValueKey('building-tab-content-${active.key}'), builder: active.builder),
      ],
    );
  }
}
