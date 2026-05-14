const catalogImageModules = import.meta.glob('../assets/products/*.svg', { eager: true, import: 'default' }) as Record<string, string>

const catalogImagesBySlug = Object.fromEntries(
  Object.entries(catalogImageModules).map(([path, url]) => [
    path.split('/').pop()?.replace(/\.svg$/i, '') ?? path,
    url,
  ]),
) as Record<string, string>

const INLINE_FALLBACK_IMAGE = `data:image/svg+xml;utf8,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 256 256'%3E%3Crect width='256' height='256' rx='34' fill='%231f2937'/%3E%3Cpath d='M62 91l66-38 66 38v74l-66 38-66-38z' fill='%23e5e7eb'/%3E%3Cpath d='M62 91l66 38 66-38M128 129v74' stroke='%2364748b' stroke-width='9' fill='none'/%3E%3Ccircle cx='128' cy='129' r='18' fill='%2394a3b8'/%3E%3C/svg%3E`
const CATALOG_FALLBACK_IMAGE = catalogImagesBySlug.fallback ?? INLINE_FALLBACK_IMAGE

export const RESOURCE_IMAGE_SLUGS = [
  'chemical-minerals',
  'coal',
  'cotton',
  'gold',
  'grain',
  'iron-ore',
  'silicon',
  'wood',
] as const

export const PRODUCT_IMAGE_SLUGS = [
  'analgesic-syrup',
  'antibiotic',
  'antiseptic-gel',
  'aspirin',
  'assembly-pallet',
  'bandages',
  'basic-electronics',
  'basic-medicine',
  'battery-cell',
  'battery-pack',
  'bread',
  'breadcrumbs',
  'cable-duct',
  'calculator',
  'cargo-pack',
  'charcoal-pack',
  'circuit-board',
  'coal-briquette',
  'coke-block',
  'compressed-gas-bottle',
  'control-panel',
  'cotton-wrap',
  'cough-suppressant',
  'courier-bag',
  'desk-speaker',
  'diagnostic-kit',
  'dining-set',
  'disinfectant-wipes',
  'electronic-components',
  'electronic-table',
  'energy-tablet',
  'eye-drops',
  'fabric-label',
  'filing-cabinet',
  'first-aid-kit',
  'flat-pack-box',
  'flour',
  'fuel-rod',
  'gas-canister',
  'gate-frame',
  'glass-pane',
  'glass-window',
  'gold-contact',
  'heating-oil',
  'heavy-duty-sack',
  'industrial-fuel',
  'industrial-relay',
  'insulated-liner',
  'insulation-roll',
  'insulin-pen',
  'iron-fasteners',
  'iron-nails',
  'junction-box',
  'led-bulb-pack',
  'led-lamp',
  'led-screen',
  'medical-cream',
  'medical-gloves',
  'meter-module',
  'nasal-spray',
  'network-router',
  'padded-envelope',
  'pallet-cover',
  'paracetamol-pack',
  'pasta-kit',
  'pharmaceutical-capsule',
  'pipe-section',
  'power-adapter',
  'power-pellet',
  'radio-set',
  'refined-kerosene',
  'roofing-sheet',
  'safety-railing',
  'scaffold-kit',
  'screws-box',
  'sensor-module',
  'shipping-bag',
  'signal-amplifier',
  'silicon-wafer',
  'smart-home-hub',
  'snack-crackers',
  'solar-cell',
  'solar-roof-tile',
  'steel-beam',
  'steel-door',
  'steel-ingot',
  'steel-panel',
  'storage-sack',
  'support-column',
  'tote-bag',
  'touch-display',
  'turbine-oil',
  'vaccine-vial',
  'ventilation-grille',
  'vitamin-capsule',
  'wall-panel',
  'warehouse-rack',
  'window-frame',
  'wood-panel',
  'wood-planks',
  'wooden-bed',
  'wooden-chair',
  'wooden-table',
  'wound-dressing-kit',
] as const


const RESOURCE_IMAGE_SLUG_SET = new Set<string>(RESOURCE_IMAGE_SLUGS)
const PRODUCT_IMAGE_SLUG_SET = new Set<string>(PRODUCT_IMAGE_SLUGS)

export function getCatalogFallbackImageUrl(): string {
  return CATALOG_FALLBACK_IMAGE
}

export function hasResourceCatalogImage(slug: string): boolean {
  return RESOURCE_IMAGE_SLUG_SET.has(slug) && Boolean(catalogImagesBySlug[slug])
}

export function hasProductCatalogImage(slug: string): boolean {
  return PRODUCT_IMAGE_SLUG_SET.has(slug) && Boolean(catalogImagesBySlug[slug])
}

export function getResourceCatalogImageUrl(slug: string, existingImageUrl: string | null): string {
  return catalogImagesBySlug[slug] ?? existingImageUrl ?? CATALOG_FALLBACK_IMAGE
}

export function getProductCatalogImageUrl(slug: string): string {
  return catalogImagesBySlug[slug] ?? CATALOG_FALLBACK_IMAGE
}
