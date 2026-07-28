// News compose/edit/publish form for the Operations News screen, ported
// from `projects/frontend/src/views/OperationsNewsView.vue`'s editor —
// previously the feed was shown read-only (drafts included) instead of
// this full CMS form.

import 'package:flutter/material.dart';

import 'operations_models.dart';
import 'operations_service.dart';

const _entryTypes = ['NEWS', 'CHANGELOG'];
const _statuses = ['DRAFT', 'PUBLISHED'];
const _locales = ['en', 'sk', 'de'];

/// Opens the compose/edit dialog. Pass [entry] to pre-fill for editing, or
/// omit it to compose a new entry. Returns true if the entry was saved.
Future<bool?> showNewsEntryEditor(BuildContext context, {required OperationsService service, AdminNewsEntry? entry}) {
  return showDialog<bool>(
    context: context,
    builder: (_) => _NewsEntryEditorDialog(service: service, entry: entry),
  );
}

class _NewsEntryEditorDialog extends StatefulWidget {
  const _NewsEntryEditorDialog({required this.service, this.entry});

  final OperationsService service;
  final AdminNewsEntry? entry;

  @override
  State<_NewsEntryEditorDialog> createState() => _NewsEntryEditorDialogState();
}

class _NewsEntryEditorDialogState extends State<_NewsEntryEditorDialog> {
  late String _entryType;
  late String _status;
  late final Map<String, TextEditingController> _titleControllers;
  late final Map<String, TextEditingController> _summaryControllers;
  late final Map<String, TextEditingController> _contentControllers;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final entry = widget.entry;
    _entryType = entry?.entryType ?? _entryTypes.first;
    _status = entry?.status ?? _statuses.first;
    _titleControllers = {for (final locale in _locales) locale: TextEditingController(text: entry?.localizationFor(locale)?.title ?? '')};
    _summaryControllers = {for (final locale in _locales) locale: TextEditingController(text: entry?.localizationFor(locale)?.summary ?? '')};
    _contentControllers = {
      for (final locale in _locales) locale: TextEditingController(text: _htmlContentFor(entry, locale)),
    };
  }

  String _htmlContentFor(AdminNewsEntry? entry, String locale) {
    if (entry == null) return '';
    for (final localization in entry.localizations) {
      if (localization.locale == locale) return localization.htmlContent;
    }
    return '';
  }

  @override
  void dispose() {
    for (final controller in _titleControllers.values) {
      controller.dispose();
    }
    for (final controller in _summaryControllers.values) {
      controller.dispose();
    }
    for (final controller in _contentControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final localizations = <Map<String, String>>[];
    for (final locale in _locales) {
      final title = _titleControllers[locale]!.text.trim();
      if (title.isEmpty) continue;
      localizations.add({
        'locale': locale,
        'title': title,
        'summary': _summaryControllers[locale]!.text.trim(),
        'htmlContent': _contentControllers[locale]!.text.trim(),
      });
    }
    if (localizations.isEmpty) {
      setState(() => _error = 'At least one language needs a title.');
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.service.upsertGameNewsEntry(
        entryId: widget.entry?.id,
        entryType: _entryType,
        status: _status,
        localizations: localizations,
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Could not save this entry.';
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isEditing = widget.entry != null;
    return AlertDialog(
      title: Text(isEditing ? 'Edit news entry' : 'New news entry'),
      content: SizedBox(
        width: 480,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: DropdownButtonFormField<String>(
                      initialValue: _entryType,
                      decoration: const InputDecoration(labelText: 'Type'),
                      items: [for (final type in _entryTypes) DropdownMenuItem(value: type, child: Text(type))],
                      onChanged: (value) => setState(() => _entryType = value ?? _entryType),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: DropdownButtonFormField<String>(
                      initialValue: _status,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: [for (final status in _statuses) DropdownMenuItem(value: status, child: Text(status))],
                      onChanged: (value) => setState(() => _status = value ?? _status),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              for (final locale in _locales) ...[
                Text(locale.toUpperCase(), style: Theme.of(context).textTheme.labelLarge),
                TextField(
                  key: ValueKey('news-title-$locale'),
                  controller: _titleControllers[locale],
                  decoration: const InputDecoration(labelText: 'Title'),
                ),
                TextField(
                  key: ValueKey('news-summary-$locale'),
                  controller: _summaryControllers[locale],
                  decoration: const InputDecoration(labelText: 'Summary'),
                ),
                TextField(
                  key: ValueKey('news-content-$locale'),
                  controller: _contentControllers[locale],
                  decoration: const InputDecoration(labelText: 'HTML content'),
                  maxLines: 3,
                ),
                const SizedBox(height: 12),
              ],
              if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancel')),
        FilledButton(
          key: const ValueKey('save-news-entry-button'),
          onPressed: _saving ? null : _save,
          child: Text(_saving ? 'Saving…' : 'Save'),
        ),
      ],
    );
  }
}
