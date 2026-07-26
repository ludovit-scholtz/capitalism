// Ported from `projects/frontend/src/lib/personalAccountName.ts` — a
// three-part fictional name (Firstname Middlename Lastname), NOT the
// player's real name (enforced by UI copy, not by this generator).

import 'dart:math';

enum PlayerGender { female, male }

/// Mirrors web's `femaleFirstNames`.
const List<String> femaleFirstNames = [
  'Alice', 'Amelia', 'Aria', 'Ava', 'Beatrice', 'Camila', 'Charlotte', 'Chloe',
  'Clara', 'Diana', 'Eleanor', 'Elise', 'Emma', 'Eva', 'Freya', 'Grace',
  'Hannah', 'Hazel', 'Iris', 'Isla', 'Jasmine', 'Lena', 'Lily', 'Luna',
  'Maya', 'Mia', 'Nora', 'Olivia', 'Ruby', 'Sofia',
];

/// Mirrors web's `maleFirstNames`.
const List<String> maleFirstNames = [
  'Adrian', 'Alexander', 'Benjamin', 'Caleb', 'Charles', 'Daniel', 'Dominic', 'Edward',
  'Elias', 'Ethan', 'Felix', 'Finn', 'Gabriel', 'Henry', 'Isaac', 'James',
  'Julian', 'Leo', 'Liam', 'Lucas', 'Marcus', 'Mason', 'Nathan', 'Noah',
  'Oliver', 'Oscar', 'Samuel', 'Theo', 'Thomas', 'William',
];

/// Mirrors web's `surnames` (international, diverse-origin).
const List<String> surnames = [
  'Anderson', 'Baker', 'Carter', 'Davis', 'Evans', 'Foster', 'Garcia', 'Harris',
  'Irving', 'Jones', 'Knight', 'Lewis', 'Morgan', 'Nelson', 'Owen', 'Parker',
  'Quinn', 'Roberts', 'Smith', 'Taylor', 'Urban', 'Vargas', 'Wilson', 'Xavier',
  'Young', 'Zhang', 'Allen', 'Brooks', 'Collins', 'Duncan', 'Edwards', 'Fleming',
  'Grant', 'Hunter', 'Ingram', 'Jenkins', 'Keller', 'Lambert', 'Martinez', 'Nash',
  'Pierce', 'Rivera', 'Santos', 'Torres', 'Vance', 'Walker', 'Yamamoto', 'Zhou',
  'Archer', 'Bell', 'Cooper', 'Drake', 'Ellis', 'Fisher', 'Gordon', 'Hayes',
  'Irwin', 'Jacobs', 'Kim', 'Lane', 'Mitchell', 'Norton', 'Ortega', 'Patel',
  'Reynolds', 'Stone', 'Turner', 'Upton', 'Vicente', 'Warren', 'York', 'Zimmerman',
  'Adler', 'Baxter', 'Crawford', 'Dean', 'Erikson', 'Ford', 'Graham', 'Hayden',
  'Jensen', 'Knox', 'Lucas', 'Morrison', 'Nguyen', 'Okafor', 'Porter', 'Reyes',
  'Sato', 'Tanaka', 'Underwood', 'Vasquez', 'Webb', 'Yamazaki', 'Zucker',
  'Hoffman', 'Kowalski', 'Nowak', 'Petrov', 'Romero', 'Schreiber', 'Tobin',
  'Volkov', 'Watanabe', 'Xu', 'Yilmaz', 'Zielinski',
];

final Set<String> _usedPersonalNames = {};

/// Clears the session uniqueness tracker. Mirrors `resetPersonalNameSession`.
void resetPersonalNameSession() => _usedPersonalNames.clear();

String _pickGivenName(PlayerGender gender, Random rand) {
  final source = gender == PlayerGender.female ? femaleFirstNames : maleFirstNames;
  return source[rand.nextInt(source.length)];
}

String _pickSurname(Random rand) => surnames[rand.nextInt(surnames.length)];

String _generateRawName(PlayerGender gender, Random rand) {
  final first = _pickGivenName(gender, rand);
  final middle = _pickGivenName(gender, rand);
  final last = _pickSurname(rand);
  return '$first $middle $last';
}

/// Generates a unique "Firstname Middlename Lastname" suggestion, retrying up
/// to 50 times to avoid a name already returned this session (cleared via
/// [resetPersonalNameSession]). [gender] defaults to a 50/50 random pick when
/// omitted. Mirrors `generatePersonalAccountName`.
String generatePersonalAccountName({PlayerGender? gender, Random? random}) {
  final rand = random ?? Random();
  final resolvedGender = gender ?? (rand.nextDouble() < 0.5 ? PlayerGender.female : PlayerGender.male);

  for (var i = 0; i < 50; i++) {
    final name = _generateRawName(resolvedGender, rand);
    if (!_usedPersonalNames.contains(name)) {
      _usedPersonalNames.add(name);
      return name;
    }
  }

  _usedPersonalNames.clear();
  final name = _generateRawName(resolvedGender, rand);
  _usedPersonalNames.add(name);
  return name;
}
