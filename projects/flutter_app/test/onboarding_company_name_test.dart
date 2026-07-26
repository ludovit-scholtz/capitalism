import 'dart:math';

import 'package:capitalism_app/features/onboarding/onboarding_company_name.dart';
import 'package:flutter_test/flutter_test.dart';

/// A fully deterministic stand-in for [Random] that returns `values[i] % max`
/// on the i-th call to [nextInt] (cycling once exhausted), so tests can pin
/// down exactly which word/suffix combination the generator draws without
/// depending on Dart's actual PRNG sequence for a given seed.
class _SequenceRandom implements Random {
  _SequenceRandom(this._values);

  final List<int> _values;
  int _index = 0;

  @override
  int nextInt(int max) {
    final value = _values[_index % _values.length] % max;
    _index++;
    return value;
  }

  @override
  double nextDouble() => 0;

  @override
  bool nextBool() => false;
}

void main() {
  group('generateOnboardingCompanyName', () {
    test('produces a "Word Suffix" name from the industry wordlist', () {
      resetCompanyNameSession('format-test:FURNITURE');
      final name = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));
      expect(name, '${industryWords['FURNITURE']![0]} ${businessSuffixes[0]}');
    });

    test('falls back to fallbackWords for an unknown industry', () {
      resetCompanyNameSession('format-test:UNKNOWN');
      final name = generateOnboardingCompanyName('NOT_A_REAL_INDUSTRY', random: _SequenceRandom([0, 0]));
      expect(name, '${fallbackWords[0]} ${businessSuffixes[0]}');
    });

    test('retries within a single call when the first draw collides with an already-used name', () {
      resetCompanyNameSession('retry-test');
      // First call consumes (word[0], suffix[0]).
      final first = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));
      expect(first, '${industryWords['FURNITURE']![0]} ${businessSuffixes[0]}');

      // Second call: sequence draws (word[0], suffix[0]) again (a collision,
      // since it's already used), then (word[1], suffix[0]) on the retry.
      final second = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0, 1, 0]));
      expect(second, '${industryWords['FURNITURE']![1]} ${businessSuffixes[0]}');
    });

    test('resetCompanyNameSession is a no-op when the key is unchanged', () {
      resetCompanyNameSession('stable-key');
      final first = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));

      resetCompanyNameSession('stable-key'); // same key — must NOT clear used-name tracking

      // First candidate (word[0], suffix[0]) collides with `first`; only the
      // retry (word[1], suffix[0]) should be accepted if the session was
      // correctly preserved.
      final second = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0, 1, 0]));
      expect(second, isNot(first));
      expect(second, '${industryWords['FURNITURE']![1]} ${businessSuffixes[0]}');
    });

    test('resetCompanyNameSession clears used-name tracking when the key changes', () {
      resetCompanyNameSession('key-a');
      final first = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));

      resetCompanyNameSession('key-b'); // different key — clears tracking

      // Same first-choice sequence as before now succeeds immediately
      // (no retry needed) because the used-name set was cleared.
      final second = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));
      expect(second, first);
    });

    test('exhausts 50 retries gracefully and still returns a well-formed name', () {
      resetCompanyNameSession('exhaustion-test');
      generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));

      // A sequence that always draws (word[0], suffix[0]) collides on every
      // one of the 50 attempts, forcing the exhaustion fallback path.
      final afterExhaustion = generateOnboardingCompanyName('FURNITURE', random: _SequenceRandom([0, 0]));
      expect(afterExhaustion, '${industryWords['FURNITURE']![0]} ${businessSuffixes[0]}');
    });
  });
}
