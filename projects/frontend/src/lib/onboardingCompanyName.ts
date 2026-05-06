import { uniqueNamesGenerator } from 'unique-names-generator'

/**
 * Industry-themed first-word lists for professional company name generation.
 * Uses the unique-names-generator package for the combination engine, with
 * hand-curated thematic words per industry for an economic simulation context.
 * Exported for testing and introspection.
 */
export const industryWords: Record<string, string[]> = {
  FURNITURE: [
    'Oak', 'Timber', 'Cedar', 'Maple', 'Walnut', 'Birch', 'Pine', 'Teak',
    'Redwood', 'Ironwood', 'Crafted', 'Craftwood', 'Laurelwood', 'Classic', 'Cherrywood',
    'Ashwood', 'Elmwood', 'Mahogany', 'Rosewood', 'Sandalwood',
  ],
  FOOD_PROCESSING: [
    'Harvest', 'Golden', 'Granary', 'Valley', 'Meadow', 'Brewer', 'Savorex',
    'Fresh', 'Prime', 'Grain', 'Sunrise', 'Orchard', 'Bloom', 'Pastoral',
    'Crofton', 'Millstone', 'Croftsman', 'Barley', 'Amber', 'Verdant',
  ],
  HEALTHCARE: [
    'Vital', 'Nova', 'Remedy', 'Shield', 'Helix', 'Apex', 'Synapse',
    'Curative', 'Lumina', 'Medic', 'Regen', 'Clarity', 'Salus', 'Biovance',
    'Correx', 'Nuvarix', 'Lifespan', 'Vitalix', 'Pharmax', 'Zenova',
  ],
  ELECTRONICS: [
    'Circuit', 'Silicon', 'Quantum', 'Pixel', 'Pulse', 'Nano', 'Logic',
    'Voltaic', 'CoreTech', 'Axiom', 'Nexon', 'Syntek', 'Byte', 'Lattice',
    'Photon', 'Hexagon', 'Vertex', 'Microcore', 'Orbis', 'Telaris',
  ],
  CONSTRUCTION: [
    'Bedrock', 'Summit', 'Keystone', 'Ironclad', 'Pillar', 'Granite',
    'Crest', 'Foundry', 'Basalt', 'Bulwark', 'Stronghold', 'Arcstone',
    'Trident', 'Rampart', 'Citadel', 'Solida', 'Terrafirm', 'Pinnacle',
    'Masonry', 'Cornerstone',
  ],
  PHARMACEUTICALS: [
    'Scienta', 'Vita', 'Novex', 'Biospan', 'Pharmex', 'Clineva', 'Lumena',
    'Apharma', 'Zoria', 'GenoPlex', 'Theravo', 'Biovanta', 'Cellex',
    'Therapeutix', 'Vitalora', 'Genexis', 'Pharmasol', 'Clinex', 'Medivance',
    'Revitare',
  ],
  ENERGY: [
    'Solaris', 'Kinetic', 'Radiant', 'Dynamo', 'Fusion', 'Horizon', 'Ignition',
    'Wattcore', 'Wattex', 'GridTech', 'PowerCo', 'Voltan', 'Lumex',
    'Ampere', 'Therma', 'Photona', 'Polaris', 'Celero', 'Novagen', 'Electra',
  ],
  LOGISTICS: [
    'Meridian', 'Nexus', 'Convoy', 'Swift', 'Relay', 'FluxNet', 'TransMax',
    'Harborcroft', 'Cargotek', 'Linker', 'Freightco', 'Pathfinder', 'Vectus',
    'Transito', 'Velocity', 'Expedio', 'Bridgeport', 'Portside', 'Xpedite',
    'Trailblazer',
  ],
}

/** Fallback word list for unknown industries. Exported for testing. */
export const fallbackWords = [
  'Prime', 'Atlas', 'Summit', 'Nexus', 'Apex', 'Vanguard', 'Pinnacle',
  'Core', 'Titan', 'Zenith', 'Crest', 'Orion', 'Solace', 'Fortis',
  'Verdant', 'Axiom', 'Crestline', 'Triton', 'Halcyon', 'Meridian',
]

/** Business-type second words shared across all industries — professional and economy-simulation themed. */
export const businessSuffixes = [
  'Industries',
  'Ventures',
  'Capital',
  'Works',
  'Corp',
  'Solutions',
  'Group',
  'Holdings',
  'Dynamics',
  'Partners',
  'Collective',
  'House',
  'Enterprises',
  'Trading',
  'Global',
]

/** Total number of distinct suffixes; used by tests to verify full coverage. */
export const NAME_GENERATOR_CYCLE_LENGTH = businessSuffixes.length

// Session state: track names already shown to avoid repeats within one regeneration session.
let _sessionKey = ''
let _usedNames = new Set<string>()

/**
 * Resets the in-session uniqueness tracker when the player changes their industry or city,
 * so the next call to {@link generateOnboardingCompanyName} starts fresh.
 */
export function resetNameSession(key: string): void {
  if (key !== _sessionKey) {
    _sessionKey = key
    _usedNames = new Set()
  }
}

/**
 * Generates a professional two-word company name using the `unique-names-generator`
 * package with curated industry-specific word lists for an economic simulation game.
 *
 * Each call picks a fresh random combination; names already shown in this session are
 * skipped so the player sees distinct suggestions on every regeneration.
 *
 * The caller is responsible for calling {@link resetNameSession} when the industry or
 * city changes, so the uniqueness set is cleared for a fresh sequence.
 *
 * @param industry  - Selected industry key (e.g. 'FURNITURE', 'HEALTHCARE').
 */
export function generateOnboardingCompanyName(industry: string): string {
  const words = industryWords[industry] ?? fallbackWords

  // Attempt up to 50 random picks to find a name not yet shown this session.
  let name = ''
  for (let i = 0; i < 50; i++) {
    name = uniqueNamesGenerator({
      dictionaries: [words, businessSuffixes],
      separator: ' ',
      style: 'capital',
    })
    if (!_usedNames.has(name)) break
  }

  // If all 50 attempts produced already-seen names (session near-exhaustion, ~300 combinations),
  // clear the session and restart so the player always receives a usable suggestion.
  if (_usedNames.has(name)) {
    _usedNames = new Set()
    name = uniqueNamesGenerator({
      dictionaries: [words, businessSuffixes],
      separator: ' ',
      style: 'capital',
    })
  }

  _usedNames.add(name)
  return name
}
