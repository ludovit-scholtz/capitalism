import 'dart:math';

import 'package:capitalism_app/features/onboarding/onboarding_personal_name.dart';
import 'package:flutter_test/flutter_test.dart';

/// Deterministic [Random] stand-in — see `onboarding_company_name_test.dart`
/// for rationale. [nextDouble] drives gender selection when [gender] is
/// omitted (`< 0.5` picks female).
class _SequenceRandom implements Random {
  _SequenceRandom(this._ints, {double nextDoubleValue = 0}) : _nextDoubleValue = nextDoubleValue;

  final List<int> _ints;
  final double _nextDoubleValue;
  int _index = 0;

  @override
  int nextInt(int max) {
    final value = _ints[_index % _ints.length] % max;
    _index++;
    return value;
  }

  @override
  double nextDouble() => _nextDoubleValue;

  @override
  bool nextBool() => false;
}

void main() {
  setUp(resetPersonalNameSession);

  group('generatePersonalAccountName', () {
    test('produces a "First Middle Last" name for the requested gender', () {
      final name = generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 1, 2]));
      expect(name, '${maleFirstNames[0]} ${maleFirstNames[1]} ${surnames[2]}');
    });

    test('draws first and middle names from the same gender pool', () {
      final name = generatePersonalAccountName(gender: PlayerGender.female, random: _SequenceRandom([3, 5, 0]));
      expect(name, '${femaleFirstNames[3]} ${femaleFirstNames[5]} ${surnames[0]}');
    });

    test('defaults to a 50/50 random gender when unspecified', () {
      final female = generatePersonalAccountName(random: _SequenceRandom([0, 0, 0], nextDoubleValue: 0.1));
      expect(female, '${femaleFirstNames[0]} ${femaleFirstNames[0]} ${surnames[0]}');

      resetPersonalNameSession();
      final male = generatePersonalAccountName(random: _SequenceRandom([0, 0, 0], nextDoubleValue: 0.9));
      expect(male, '${maleFirstNames[0]} ${maleFirstNames[0]} ${surnames[0]}');
    });

    test('retries within a single call when the first draw collides with an already-used name', () {
      final first = generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 0, 0]));
      expect(first, '${maleFirstNames[0]} ${maleFirstNames[0]} ${surnames[0]}');

      // First attempt (0,0,0) collides; retry draws (1,0,0).
      final second = generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 0, 0, 1, 0, 0]));
      expect(second, '${maleFirstNames[1]} ${maleFirstNames[0]} ${surnames[0]}');
    });

    test('resetPersonalNameSession clears tracking so a name can repeat', () {
      final first = generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 0, 0]));
      resetPersonalNameSession();
      final second = generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 0, 0]));
      expect(second, first);
    });

    test('exhausts 50 retries gracefully and still returns a well-formed name', () {
      generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 0, 0]));
      final afterExhaustion = generatePersonalAccountName(gender: PlayerGender.male, random: _SequenceRandom([0, 0, 0]));
      expect(afterExhaustion, '${maleFirstNames[0]} ${maleFirstNames[0]} ${surnames[0]}');
    });
  });
}
