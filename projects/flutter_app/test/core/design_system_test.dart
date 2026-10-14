import 'package:capitalism_app/core/theme/app_theme.dart';
import 'package:capitalism_app/core/widgets/animated_number.dart';
import 'package:capitalism_app/core/widgets/glass_card.dart';
import 'package:capitalism_app/core/widgets/skeleton.dart';
import 'package:capitalism_app/core/widgets/state_view.dart';
import 'package:capitalism_app/core/widgets/stat_tile.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _host(Widget child, {bool disableAnimations = false}) => MaterialApp(
      theme: AppTheme.dark,
      home: MediaQuery(
        data: MediaQueryData(disableAnimations: disableAnimations),
        child: Scaffold(body: child),
      ),
    );

void main() {
  group('GlassCard', () {
    testWidgets('renders child and is tappable when onTap is set', (tester) async {
      var taps = 0;
      await tester.pumpWidget(_host(GlassCard(onTap: () => taps++, child: const Text('hello'))));
      expect(find.text('hello'), findsOneWidget);
      await tester.tap(find.text('hello'));
      expect(taps, 1);
    });

    testWidgets('exposes button semantics only when tappable', (tester) async {
      await tester.pumpWidget(_host(const GlassCard(child: Text('static'))));
      expect(find.byType(InkWell), findsNothing);
    });
  });

  group('SkeletonBox', () {
    testWidgets('renders with requested size and animates', (tester) async {
      await tester.pumpWidget(_host(const Center(child: SkeletonBox(width: 120, height: 20))));
      final size = tester.getSize(find.byType(SkeletonBox));
      expect(size, const Size(120, 20));
      await tester.pump(const Duration(milliseconds: 400));
      expect(tester.hasRunningAnimations, isTrue);
    });

    testWidgets('does not run an infinite animation when animations are disabled', (tester) async {
      await tester.pumpWidget(_host(const SkeletonBox(width: 50, height: 10), disableAnimations: true));
      await tester.pumpAndSettle();
      expect(tester.hasRunningAnimations, isFalse);
    });
  });

  group('AnimatedNumber', () {
    testWidgets('shows the initial value immediately', (tester) async {
      await tester.pumpWidget(_host(AnimatedNumber(value: 100, builder: (v) => 'v=${v.round()}')));
      expect(find.text('v=100'), findsOneWidget);
    });

    testWidgets('tweens to a new value', (tester) async {
      Widget build(double v) => _host(AnimatedNumber(value: v, builder: (x) => 'v=${x.round()}'));
      await tester.pumpWidget(build(0));
      await tester.pumpWidget(build(1000));
      await tester.pump(const Duration(milliseconds: 100));
      final mid = int.parse((tester.widget<Text>(find.byType(Text)).data!).substring(2));
      expect(mid, inInclusiveRange(1, 999));
      await tester.pumpAndSettle();
      expect(find.text('v=1000'), findsOneWidget);
    });

    testWidgets('jumps straight to the value when animations are disabled', (tester) async {
      Widget build(double v) =>
          _host(AnimatedNumber(value: v, builder: (x) => 'v=${x.round()}'), disableAnimations: true);
      await tester.pumpWidget(build(0));
      await tester.pumpWidget(build(500));
      await tester.pump();
      expect(find.text('v=500'), findsOneWidget);
    });
  });

  group('StatTile', () {
    testWidgets('shows label and value', (tester) async {
      await tester.pumpWidget(_host(const StatTile(label: 'Cash', value: r'$1.2K', icon: Icons.attach_money)));
      expect(find.text('Cash'), findsOneWidget);
      expect(find.text(r'$1.2K'), findsOneWidget);
    });

    testWidgets('merges label and value into one semantics node', (tester) async {
      final handle = tester.ensureSemantics();
      await tester.pumpWidget(_host(const StatTile(label: 'Cash', value: '5', icon: Icons.attach_money)));
      expect(find.bySemanticsLabel(RegExp('Cash')), findsOneWidget);
      handle.dispose();
    });
  });

  group('StateView', () {
    testWidgets('loading shows skeletons, not a spinner', (tester) async {
      await tester.pumpWidget(_host(const StateView.loading()));
      expect(find.byType(SkeletonBox), findsWidgets);
      expect(find.byType(CircularProgressIndicator), findsNothing);
    });

    testWidgets('empty shows title and message', (tester) async {
      await tester.pumpWidget(_host(const StateView.empty(title: 'Nothing here', message: 'Build something')));
      expect(find.text('Nothing here'), findsOneWidget);
      expect(find.text('Build something'), findsOneWidget);
    });

    testWidgets('error shows message and retry invokes callback', (tester) async {
      var retries = 0;
      await tester.pumpWidget(_host(StateView.error(message: 'Boom', onRetry: () => retries++)));
      expect(find.text('Boom'), findsOneWidget);
      await tester.tap(find.text('Retry'));
      expect(retries, 1);
    });

    testWidgets('error without onRetry has no retry button', (tester) async {
      await tester.pumpWidget(_host(const StateView.error(message: 'Boom')));
      expect(find.text('Retry'), findsNothing);
    });
  });
}
