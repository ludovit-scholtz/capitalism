import { resolveApiBaseUrl, resolveGameGraphqlUrl } from './runtimeGraphqlUrl'

// Catalog artwork is hosted by the game API (see `Api/wwwroot/images/products`)
// instead of being bundled with the frontend, so every deployment shares one
// set of pictures and the API can control caching for them.
const CATALOG_IMAGE_PATH = '/images/products'

function resolveCatalogImageBaseUrl(): string {
  return resolveApiBaseUrl(resolveGameGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL))
}

function buildCatalogImageUrl(slug: string): string {
  return `${resolveCatalogImageBaseUrl()}${CATALOG_IMAGE_PATH}/${slug}.svg`
}

export const RESOURCE_IMAGE_SLUGS = ['chemical-minerals', 'coal', 'cotton', 'gold', 'grain', 'iron-ore', 'silicon', 'wood'] as const

export const PRODUCT_IMAGE_SLUGS = [
  'allergy-tablets',
  'analgesic-syrup',
  'animal-feed',
  'antibiotic',
  'antiseptic',
  'antiseptic-gel',
  'aspirin',
  'assembly-pallet',
  'bakery-premix',
  'bandages',
  'basic-electronics',
  'basic-medicine',
  'battery-cell',
  'battery-pack',
  'biscuit-pack',
  'bookshelf',
  'bread',
  'bran-bags',
  'breadcrumbs',
  'bunk-bed',
  'cake-mix',
  'cable-duct',
  'calculator',
  'cargo-pack',
  'cereal-flakes',
  'charcoal-pack',
  'circuit-board',
  'coal-briquette',
  'coffee-table',
  'coke-block',
  'cold-pack',
  'compressed-gas-bottle',
  'compression-wrap',
  'control-panel',
  'cotton-swabs',
  'cotton-wrap',
  'cough-suppressant',
  'cough-syrup',
  'courier-bag',
  'crackers',
  'crib',
  'desk-speaker',
  'diagnostic-kit',
  'dining-bench',
  'dining-set',
  'disinfectant-wipes',
  'door-frame',
  'dresser',
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
  'grain-bars',
  'heating-oil',
  'heavy-duty-sack',
  'healing-ointment',
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
  'mineral-supplement',
  'nasal-spray',
  'network-router',
  'nightstand',
  'noodles',
  'office-desk',
  'padded-envelope',
  'pain-relief-tablets',
  'pallet-cover',
  'pancake-mix',
  'paracetamol-pack',
  'pasta',
  'pasta-kit',
  'patio-chair',
  'patio-table',
  'pharmaceutical-capsule',
  'pipe-section',
  'porridge-mix',
  'power-adapter',
  'power-pellet',
  'radio-set',
  'refined-kerosene',
  'roofing-sheet',
  'safety-railing',
  'saline-kit',
  'sandwich-bread',
  'scaffold-kit',
  'screws-box',
  'semolina',
  'sensor-module',
  'shipping-bag',
  'signal-amplifier',
  'silicon-wafer',
  'smart-home-hub',
  'sofa-frame',
  'snack-crackers',
  'solar-cell',
  'solar-roof-tile',
  'steel-beam',
  'steel-door',
  'steel-ingot',
  'steel-panel',
  'sterile-gauze',
  'storage-sack',
  'support-column',
  'surgical-masks',
  'surgical-tape',
  'toast-bread',
  'tote-bag',
  'touch-display',
  'turbine-oil',
  'tv-stand',
  'vaccine-vial',
  'ventilation-grille',
  'vitamin-capsule',
  'vitamin-pack',
  'wall-panel',
  'wardrobe',
  'warehouse-rack',
  'window-frame',
  'wood-panel',
  'wood-planks',
  'wooden-bed',
  'wooden-chair',
  'wooden-stool',
  'wooden-table',
  'wound-dressing-kit',
] as const

const RESOURCE_IMAGE_SLUG_SET = new Set<string>(RESOURCE_IMAGE_SLUGS)
const PRODUCT_IMAGE_SLUG_SET = new Set<string>(PRODUCT_IMAGE_SLUGS)
// Both categories are served from the same backend folder by convention, so a
// slug from either list resolves to a real file regardless of which lookup
// function is called with it (mirrors the pre-migration bundled-asset lookup).
const ALL_IMAGE_SLUG_SET = new Set<string>([...RESOURCE_IMAGE_SLUGS, ...PRODUCT_IMAGE_SLUGS])

export function getCatalogFallbackImageUrl(): string {
  return buildCatalogImageUrl('fallback')
}

export function hasResourceCatalogImage(slug: string): boolean {
  return RESOURCE_IMAGE_SLUG_SET.has(slug)
}

export function hasProductCatalogImage(slug: string): boolean {
  return PRODUCT_IMAGE_SLUG_SET.has(slug)
}

export function getResourceCatalogImageUrl(slug: string, existingImageUrl: string | null): string {
  if (ALL_IMAGE_SLUG_SET.has(slug)) {
    return buildCatalogImageUrl(slug)
  }

  return existingImageUrl ?? getCatalogFallbackImageUrl()
}

export function getProductCatalogImageUrl(slug: string): string {
  if (ALL_IMAGE_SLUG_SET.has(slug)) {
    return buildCatalogImageUrl(slug)
  }

  return getCatalogFallbackImageUrl()
}
