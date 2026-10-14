// Static contract check: every GraphQL operation the app can send must name
// root fields that still exist on the backend (Api or MasterApi). Catches
// typos and server-side renames/removals without needing a running server.
// Skipped automatically when the backend sources aren't checked out beside
// the app (e.g. a Flutter-only checkout).
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

final _backendDirs = ['../Api', '../MasterApi'];

/// `public ... Name(` resolver methods in `Query*.cs` / `Mutation*.cs`.
Map<String, Set<String>> _backendFields() {
  final out = {'query': <String>{}, 'mutation': <String>{}};
  final method = RegExp(r'public\s+(?:static\s+)?(?:async\s+)?[\w<>\[\]?,.\s]+?\s+(\w+)\s*\(');
  final gqlName = RegExp(r'\[GraphQLName\("(\w+)"\)\]');
  for (final dir in _backendDirs) {
    final root = Directory(dir);
    if (!root.existsSync()) continue;
    for (final f in root.listSync(recursive: true).whereType<File>()) {
      final path = f.path.replaceAll(r'\', '/');
      if (!path.endsWith('.cs') || path.contains('/obj/') || path.contains('/bin/')) continue;
      final name = path.split('/').last;
      final kind = name.startsWith('Query') ? 'query' : (name.startsWith('Mutation') ? 'mutation' : null);
      if (kind == null) continue;
      final src = f.readAsStringSync();
      for (final m in method.allMatches(src)) {
        final raw = m.group(1)!.replaceAll(RegExp(r'Async$'), '');
        // Some resolvers keep the `Get` prefix on the wire (the web client
        // uses e.g. `getCityEconomicReport`), others drop it — accept both.
        out[kind]!.add(_camel(raw));
        out[kind]!.add(_graphQlName(raw));
      }
      for (final m in gqlName.allMatches(src)) {
        out[kind]!.add(m.group(1)!);
      }
    }
  }
  return out;
}

/// HotChocolate drops a `Get` prefix and `Async` suffix and camel-cases.
String _graphQlName(String method) {
  var n = method.replaceAll(RegExp(r'Async$'), '');
  if (RegExp(r'^Get[A-Z]').hasMatch(n)) n = n.substring(3);
  return _camel(n);
}

String _camel(String s) => s.isEmpty ? s : s[0].toLowerCase() + s.substring(1);

class _Op {
  _Op(this.file, this.kind, this.name, this.rootFields);
  final String file;
  final String kind;
  final String name;
  final List<String> rootFields;
}

/// Extracts `r'''query|mutation ... '''` documents and their depth-1 fields.
List<_Op> _appOperations() {
  final ops = <_Op>[];
  final doc = RegExp(r"r'''\s*((?:query|mutation)\b.*?)'''", dotAll: true);
  for (final f in Directory('lib').listSync(recursive: true).whereType<File>()) {
    if (!f.path.endsWith('.dart')) continue;
    final src = f.readAsStringSync();
    for (final m in doc.allMatches(src)) {
      final text = m.group(1)!;
      final kind = text.startsWith('mutation') ? 'mutation' : 'query';
      final name = RegExp(r'^(?:query|mutation)\s+(\w+)').firstMatch(text)?.group(1) ?? '(anonymous)';
      ops.add(_Op(f.path.replaceAll(r'\', '/'), kind, name, _rootFields(text)));
    }
  }
  return ops;
}

List<String> _rootFields(String text) {
  // Drop the `(...)` variable definitions of the operation itself.
  var s = text;
  final firstBrace = s.indexOf('{');
  s = s.substring(firstBrace + 1);
  final fields = <String>[];
  var depth = 1;
  var paren = 0;
  final buf = StringBuffer();
  void flush() {
    final token = buf.toString();
    buf.clear();
    if (depth == 1 && paren == 0 && token.isNotEmpty) fields.add(token);
  }

  for (var i = 0; i < s.length; i++) {
    final c = s[i];
    if (c == '(') {
      if (depth == 1 && paren == 0) flush();
      paren++;
    } else if (c == ')') {
      paren--;
    } else if (paren > 0) {
      continue;
    } else if (c == '{') {
      if (depth == 1) flush();
      depth++;
    } else if (c == '}') {
      depth--;
      if (depth == 0) break;
    } else if (RegExp(r'[\w:]').hasMatch(c)) {
      if (depth == 1) buf.write(c);
    } else {
      if (depth == 1) flush();
    }
  }
  // Handle aliases `alias: realField` — keep the real field name.
  return fields.map((f) => f.contains(':') ? f.split(':').last : f).where((f) => f.isNotEmpty).toList();
}

void main() {
  final backendPresent = _backendDirs.any((d) => Directory(d).existsSync());
  final ops = _appOperations();
  final backend = backendPresent ? _backendFields() : null;

  test('root-field parser handles args, aliases and nested selections', () {
    expect(_rootFields('query X(\$id: UUID!) { a(id: \$id) { b { c } } d x: e { f } }'), ['a', 'd', 'e']);
  });

  test('the app declares a meaningful number of GraphQL operations', () {
    expect(ops.length, greaterThan(100));
    for (final op in ops) {
      expect(op.rootFields, isNotEmpty, reason: '${op.file}: ${op.name}');
    }
  });

  test('every root field the app queries/mutates exists in the backend', () {
    final missing = <String>[];
    for (final op in ops) {
      for (final field in op.rootFields) {
        if (field.startsWith('__')) continue;
        if (!backend![op.kind]!.contains(field)) {
          missing.add('${op.kind} $field  (${op.name} in ${op.file})');
        }
      }
    }
    expect(missing, isEmpty, reason: 'Unknown backend fields:\n${missing.join('\n')}');
  }, skip: backendPresent ? false : 'backend sources not available');
}
