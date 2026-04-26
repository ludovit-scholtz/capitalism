const industryPrefixes: Record<string, string[]> = {
  FURNITURE: ['Oak', 'Carpenter', 'Timber', 'Crafted'],
  FOOD_PROCESSING: ['Harvest', 'Golden', 'Granary', 'Daily'],
  HEALTHCARE: ['Vital', 'Care', 'Remedy', 'Nova'],
}

const citySuffixes = ['Works', 'Collective', 'Industries', 'House']

function hashSeed(value: string): number {
  let hash = 0
  for (const char of value) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  }
  return hash
}

function pickStable<T>(items: T[], seed: number): T {
  return items[seed % items.length] ?? items[0]!
}

export function generateOnboardingCompanyName(industry: string, cityName?: string | null): string {
  const normalizedIndustry = industry || 'GENERAL'
  const normalizedCity = cityName?.trim() || 'Capital'
  const seed = hashSeed(`${normalizedIndustry}:${normalizedCity}`)
  const prefixes = industryPrefixes[normalizedIndustry] ?? ['Foundry', 'Atlas', 'Prime', 'Summit']

  const prefix = pickStable(prefixes, seed)
  const cityPart = normalizedCity.split(/\s+/)[0] || 'Capital'
  const suffix = pickStable(citySuffixes, seed >> 3)

  return `${prefix} ${cityPart} ${suffix}`
}
