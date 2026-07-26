// Ported from `projects/frontend/src/lib/onboardingCompanyName.ts`. Web uses
// the `unique-names-generator` package purely as a "pick one of dict A + one
// of dict B" combiner — reproduced here with `Random` directly, no package
// needed for that part.

import 'dart:math';

/// Industry-themed first-word lists, mirroring web's `industryWords` exactly.
const Map<String, List<String>> industryWords = {
  'FURNITURE': [
    'Oak', 'Timber', 'Cedar', 'Maple', 'Walnut', 'Birch', 'Pine', 'Teak',
    'Redwood', 'Ironwood', 'Crafted', 'Craftwood', 'Laurelwood', 'Classic', 'Cherrywood',
    'Ashwood', 'Elmwood', 'Mahogany', 'Rosewood', 'Sandalwood',
  ],
  'FOOD_PROCESSING': [
    'Harvest', 'Golden', 'Granary', 'Valley', 'Meadow', 'Brewer', 'Savorex',
    'Fresh', 'Prime', 'Grain', 'Sunrise', 'Orchard', 'Bloom', 'Pastoral',
    'Crofton', 'Millstone', 'Croftsman', 'Barley', 'Amber', 'Verdant',
  ],
  'HEALTHCARE': [
    'Vital', 'Nova', 'Remedy', 'Shield', 'Helix', 'Apex', 'Synapse',
    'Curative', 'Lumina', 'Medic', 'Regen', 'Clarity', 'Salus', 'Biovance',
    'Correx', 'Nuvarix', 'Lifespan', 'Vitalix', 'Pharmax', 'Zenova',
  ],
  'ELECTRONICS': [
    'Circuit', 'Silicon', 'Quantum', 'Pixel', 'Pulse', 'Nano', 'Logic',
    'Voltaic', 'CoreTech', 'Axiom', 'Nexon', 'Syntek', 'Byte', 'Lattice',
    'Photon', 'Hexagon', 'Vertex', 'Microcore', 'Orbis', 'Telaris',
  ],
  'CONSTRUCTION': [
    'Bedrock', 'Summit', 'Keystone', 'Ironclad', 'Pillar', 'Granite',
    'Crest', 'Foundry', 'Basalt', 'Bulwark', 'Stronghold', 'Arcstone',
    'Trident', 'Rampart', 'Citadel', 'Solida', 'Terrafirm', 'Pinnacle',
    'Masonry', 'Cornerstone',
  ],
  'PHARMACEUTICALS': [
    'Scienta', 'Vita', 'Novex', 'Biospan', 'Pharmex', 'Clineva', 'Lumena',
    'Apharma', 'Zoria', 'GenoPlex', 'Theravo', 'Biovanta', 'Cellex',
    'Therapeutix', 'Vitalora', 'Genexis', 'Pharmasol', 'Clinex', 'Medivance',
    'Revitare',
  ],
  'ENERGY': [
    'Solaris', 'Kinetic', 'Radiant', 'Dynamo', 'Fusion', 'Horizon', 'Ignition',
    'Wattcore', 'Wattex', 'GridTech', 'PowerCo', 'Voltan', 'Lumex',
    'Ampere', 'Therma', 'Photona', 'Polaris', 'Celero', 'Novagen', 'Electra',
  ],
  'LOGISTICS': [
    'Meridian', 'Nexus', 'Convoy', 'Swift', 'Relay', 'FluxNet', 'TransMax',
    'Harborcroft', 'Cargotek', 'Linker', 'Freightco', 'Pathfinder', 'Vectus',
    'Transito', 'Velocity', 'Expedio', 'Bridgeport', 'Portside', 'Xpedite',
    'Trailblazer',
  ],
};

/// Fallback word list for unknown/unmapped industries, mirroring web's `fallbackWords`.
const List<String> fallbackWords = [
  'Prime', 'Atlas', 'Summit', 'Nexus', 'Apex', 'Vanguard', 'Pinnacle',
  'Core', 'Titan', 'Zenith', 'Crest', 'Orion', 'Solace', 'Fortis',
  'Verdant', 'Axiom', 'Crestline', 'Triton', 'Halcyon', 'Meridian',
];

/// Shared business-type suffixes, mirroring web's `businessSuffixes`.
const List<String> businessSuffixes = [
  'Industries', 'Ventures', 'Capital', 'Works', 'Corp', 'Solutions', 'Group',
  'Holdings', 'Dynamics', 'Partners', 'Collective', 'House', 'Enterprises',
  'Trading', 'Global',
];

String _sessionKey = '';
final Set<String> _usedCompanyNames = {};

/// Resets the in-session uniqueness tracker when the player changes industry
/// or city, so the next [generateOnboardingCompanyName] call starts fresh.
void resetCompanyNameSession(String key) {
  if (key != _sessionKey) {
    _sessionKey = key;
    _usedCompanyNames.clear();
  }
}

/// Generates a "Word Suffix" company name for [industry], retrying up to 50
/// times to avoid repeating a name already shown in this session (cleared via
/// [resetCompanyNameSession]). Mirrors `generateOnboardingCompanyName`.
String generateOnboardingCompanyName(String industry, {Random? random}) {
  final rand = random ?? Random();
  final words = industryWords[industry] ?? fallbackWords;

  String name = '';
  for (var i = 0; i < 50; i++) {
    name = '${words[rand.nextInt(words.length)]} ${businessSuffixes[rand.nextInt(businessSuffixes.length)]}';
    if (!_usedCompanyNames.contains(name)) break;
  }

  if (_usedCompanyNames.contains(name)) {
    _usedCompanyNames.clear();
    name = '${words[rand.nextInt(words.length)]} ${businessSuffixes[rand.nextInt(businessSuffixes.length)]}';
  }

  _usedCompanyNames.add(name);
  return name;
}
