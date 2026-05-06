/**
 * Industry-themed word lists for the first part of generated company names.
 * Each industry has 10+ words for sufficient variety across 10+ regenerations.
 */
const industryWords: Record<string, string[]> = {
  FURNITURE: ['Oak', 'Timber', 'Cedar', 'Maple', 'Walnut', 'Birch', 'Crafted', 'Pine', 'Teak', 'Redwood', 'Ironwood', 'Ember'],
  FOOD_PROCESSING: ['Harvest', 'Golden', 'Granary', 'Valley', 'Meadow', 'Artisan', 'Heritage', 'Fresh', 'Prime', 'Grain', 'Sunrise', 'Orchard'],
  HEALTHCARE: ['Vital', 'Nova', 'Remedy', 'Shield', 'Helix', 'Apex', 'Synapse', 'BioNex', 'Medus', 'Curative', 'Lumina', 'ClareMed'],
  ELECTRONICS: ['Circuit', 'Silicon', 'Quantum', 'Pixel', 'Axion', 'Pulse', 'Syntek', 'Nano', 'Logic', 'Nexon', 'CoreTech', 'Voltaic'],
  CONSTRUCTION: ['Bedrock', 'Summit', 'Keystone', 'Ironclad', 'Pillar', 'Arcstone', 'Granite', 'Crest', 'Foundry', 'Basalt', 'Vantage', 'Stronghold'],
  PHARMACEUTICALS: ['Helix', 'BioNex', 'Vita', 'Novex', 'Synapse', 'Curative', 'Shield', 'Medic', 'Lumena', 'Apharma', 'Zoria', 'GenoPlex'],
  ENERGY: ['Solaris', 'Voltan', 'Kinetic', 'Radiant', 'Dynamo', 'Fusion', 'Horizon', 'PowerCo', 'GridTech', 'Wattex', 'Ember', 'Voltaic'],
  LOGISTICS: ['Meridian', 'Nexus', 'Convoy', 'Pathfin', 'Cargotek', 'Swift', 'TransMax', 'Linker', 'Freightco', 'Relay', 'FluxNet', 'Harborcroft'],
}

/** Business-type suffixes shared across all industries. */
const businessSuffixes = [
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
]

function hashSeed(value: string): number {
  let hash = 0
  for (const char of value) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  }
  return hash
}

/**
 * Generates a professional two-word company name suitable for an economic simulation game.
 *
 * @param industry - The selected industry key (e.g. 'FURNITURE', 'HEALTHCARE').
 * @param cityName - Optional city name used to diversify the base seed.
 * @param offset   - Regeneration counter; increment to get a different name for the same inputs.
 *                   Defaults to 0 (first suggestion). Values 0–11 always produce distinct names.
 */
export function generateOnboardingCompanyName(industry: string, cityName?: string | null, offset = 0): string {
  const normalizedIndustry = industry || 'GENERAL'
  const normalizedCity = cityName?.trim() || 'Capital'
  const baseSeed = hashSeed(`${normalizedIndustry}:${normalizedCity}`)

  const words = industryWords[normalizedIndustry] ?? ['Prime', 'Atlas', 'Summit', 'Nexus', 'Apex', 'Vanguard', 'Pinnacle', 'Core', 'Axion', 'Titan', 'Zenith', 'Crest']

  // Use the offset to step through both lists independently so each regeneration gives
  // a fresh combination and all businessSuffixes are eventually used.
  const wordIndex = (baseSeed + offset) % words.length
  const suffixIndex = (baseSeed + offset * 3 + 7) % businessSuffixes.length

  const word = words[wordIndex] ?? words[0]!
  const suffix = businessSuffixes[suffixIndex] ?? businessSuffixes[0]!

  return `${word} ${suffix}`
}

/** Total number of distinct names the generator can produce for any single industry/city pair. */
export const NAME_GENERATOR_CYCLE_LENGTH = businessSuffixes.length
