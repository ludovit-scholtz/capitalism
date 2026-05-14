import type { ProductType, Recipe, ResourceType } from '@/types'
import { translateSlug, getProductImageUrl } from '@/lib/catalogPresentation.Icons'
import { getResourceCatalogImageUrl } from '@/lib/productImages'

export { getProductImageUrl }

type SupportedLocale = 'en' | 'sk' | 'de'

type ResourceLike = Pick<ResourceType, 'name' | 'slug' | 'category' | 'unitName' | 'unitSymbol' | 'description' | 'imageUrl'>
type ProductLike = Pick<ProductType, 'name' | 'slug' | 'industry' | 'description' | 'outputQuantity' | 'energyConsumptionMwh' | 'unitName' | 'unitSymbol' | 'recipes'>
type ProductRefLike = { name: string; slug: string; unitName?: string; unitSymbol?: string }

const resourceNameTranslations: Record<Exclude<SupportedLocale, 'en'>, Record<string, string>> = {
  sk: {
    wood: 'Drevo',
    'iron-ore': 'Železná ruda',
    coal: 'Uhlie',
    gold: 'Zlato',
    'chemical-minerals': 'Chemické minerály',
    cotton: 'Bavlna',
    grain: 'Obilie',
    silicon: 'Kremík',
  },
  de: {
    wood: 'Holz',
    'iron-ore': 'Eisenerz',
    coal: 'Kohle',
    gold: 'Gold',
    'chemical-minerals': 'Chemische Mineralien',
    cotton: 'Baumwolle',
    grain: 'Getreide',
    silicon: 'Silizium',
  },
}

const industryTranslations: Record<SupportedLocale, Record<string, string>> = {
  en: {
    FURNITURE: 'Furniture',
    FOOD_PROCESSING: 'Food Processing',
    HEALTHCARE: 'Healthcare',
    ELECTRONICS: 'Electronics',
    CONSTRUCTION: 'Construction',
  },
  sk: {
    FURNITURE: 'Nábytok',
    FOOD_PROCESSING: 'Potravinárstvo',
    HEALTHCARE: 'Zdravotníctvo',
    ELECTRONICS: 'Elektronika',
    CONSTRUCTION: 'Stavebníctvo',
  },
  de: {
    FURNITURE: 'Möbel',
    FOOD_PROCESSING: 'Lebensmittelverarbeitung',
    HEALTHCARE: 'Gesundheitswesen',
    ELECTRONICS: 'Elektronik',
    CONSTRUCTION: 'Bauwesen',
  },
}

const categoryTranslations: Record<SupportedLocale, Record<string, string>> = {
  en: {
    ORGANIC: 'Organic',
    MINERAL: 'Mineral',
    RAW_MATERIAL: 'Raw material',
  },
  sk: {
    ORGANIC: 'Organická',
    MINERAL: 'Minerálna',
    RAW_MATERIAL: 'Surovina',
  },
  de: {
    ORGANIC: 'Organisch',
    MINERAL: 'Mineralisch',
    RAW_MATERIAL: 'Rohstoff',
  },
}

const unitNameTranslations: Record<SupportedLocale, Record<string, string>> = {
  en: {},
  sk: {
    Ton: 'tona',
    Kilogram: 'kilogram',
    Piece: 'kus',
    Chair: 'stolička',
    Table: 'stôl',
    Bed: 'posteľ',
    Plank: 'doska',
    Bag: 'vrece',
    Box: 'krabica',
    Bottle: 'fľaša',
    Pack: 'balenie',
    Kit: 'súprava',
    Roll: 'rolka',
    Wafer: 'wafer',
    Board: 'doska',
    Module: 'modul',
    Panel: 'panel',
    Beam: 'nosník',
    Ingot: 'ingot',
    Frame: 'rám',
    Section: 'sekcia',
    Column: 'stĺp',
    Door: 'dvere',
    Pallet: 'paleta',
    Set: 'sada',
    Cell: 'článok',
    Display: 'displej',
    Loaf: 'bochník',
    Tube: 'tuba',
    Window: 'okno',
    Rack: 'regál',
    Tile: 'škridla',
  },
  de: {
    Ton: 'Tonne',
    Kilogram: 'Kilogramm',
    Piece: 'Stück',
    Chair: 'Stuhl',
    Table: 'Tisch',
    Bed: 'Bett',
    Plank: 'Brett',
    Bag: 'Sack',
    Box: 'Box',
    Bottle: 'Flasche',
    Pack: 'Packung',
    Kit: 'Set',
    Roll: 'Rolle',
    Wafer: 'Wafer',
    Board: 'Platte',
    Module: 'Modul',
    Panel: 'Panel',
    Beam: 'Träger',
    Ingot: 'Ingot',
    Frame: 'Rahmen',
    Section: 'Abschnitt',
    Column: 'Säule',
    Door: 'Tür',
    Pallet: 'Palette',
    Set: 'Satz',
    Cell: 'Zelle',
    Display: 'Display',
    Loaf: 'Laib',
    Tube: 'Tube',
    Window: 'Fenster',
    Rack: 'Regal',
    Tile: 'Ziegel',
  },
}

export function normalizeCatalogLocale(locale: string): SupportedLocale {
  const short = locale.toLowerCase().slice(0, 2)
  if (short === 'sk' || short === 'de') return short
  return 'en'
}

export function getLocalizedIndustry(industry: string, locale: string): string {
  const normalized = normalizeCatalogLocale(locale)
  return industryTranslations[normalized][industry] ?? humanizeIdentifier(industry)
}

export function getLocalizedCategory(category: string, locale: string): string {
  const normalized = normalizeCatalogLocale(locale)
  return categoryTranslations[normalized][category] ?? humanizeIdentifier(category)
}

export function getLocalizedResourceName(resource: Pick<ResourceType, 'slug' | 'name'> | null | undefined, locale: string): string {
  if (!resource) return '—'
  const normalized = normalizeCatalogLocale(locale)
  if (normalized === 'en') return resource.name
  return resourceNameTranslations[normalized][resource.slug] ?? resource.name
}

export function getLocalizedProductName(product: Pick<ProductType, 'slug' | 'name'> | ProductRefLike | null | undefined, locale: string): string {
  if (!product) return '—'
  const normalized = normalizeCatalogLocale(locale)
  if (normalized === 'en') return product.name
  return translateSlug(product.slug, normalized)
}

export function getLocalizedUnitName(unitName: string | null | undefined, locale: string): string {
  if (!unitName) return ''
  const normalized = normalizeCatalogLocale(locale)
  return unitNameTranslations[normalized][unitName] ?? unitName
}

export function getLocalizedRecipeIngredientName(recipe: Recipe, locale: string): string {
  if (recipe.resourceType) {
    return getLocalizedResourceName(recipe.resourceType, locale)
  }
  if (recipe.inputProductType) {
    return getLocalizedProductName(recipe.inputProductType, locale)
  }
  return '—'
}

export function getLocalizedRecipeSummary(product: ProductLike, locale: string): string {
  return product.recipes
    .map((recipe) => {
      const unitSymbol = recipe.resourceType?.unitSymbol ?? recipe.inputProductType?.unitSymbol ?? ''
      return [recipe.quantity, unitSymbol, getLocalizedRecipeIngredientName(recipe, locale)].filter(Boolean).join(' ')
    })
    .join(' + ')
}

export function getLocalizedResourceDescription(resource: ResourceLike, locale: string): string {
  const normalized = normalizeCatalogLocale(locale)
  if (normalized === 'en') {
    return resource.description ?? ''
  }

  const name = getLocalizedResourceName(resource, locale)
  const category = getLocalizedCategory(resource.category, locale).toLowerCase()
  const unit = getLocalizedUnitName(resource.unitName, locale)

  if (normalized === 'sk') {
    return `${name} je ${category} surovina obchodovaná v jednotke ${unit} (${resource.unitSymbol}).`
  }

  return `${name} ist ein ${category} Rohstoff, der in ${unit} (${resource.unitSymbol}) gehandelt wird.`
}

export function getLocalizedProductDescription(product: ProductLike, locale: string): string {
  const normalized = normalizeCatalogLocale(locale)
  if (normalized === 'en') {
    return product.description ?? ''
  }

  const name = getLocalizedProductName(product, locale)
  const industry = getLocalizedIndustry(product.industry, locale).toLowerCase()
  const recipeSummary = getLocalizedRecipeSummary(product, locale)
  const unitName = getLocalizedUnitName(product.unitName, locale)
  const inputPhrase = recipeSummary || (normalized === 'sk' ? 'predpripravených vstupov' : 'vorbereiteten Eingängen')

  if (normalized === 'sk') {
    return `${name} je ${industry} produkt. Jedna dávka vyrobí ${product.outputQuantity} ${unitName} a spotrebuje ${product.energyConsumptionMwh} MW energie z ${inputPhrase}.`
  }

  return `${name} ist ein Produkt der Branche ${industry}. Eine Charge erzeugt ${product.outputQuantity} ${unitName} und verbraucht ${product.energyConsumptionMwh} MW Energie aus ${inputPhrase}.`
}

export function getResourceImageUrl(resource: ResourceLike): string {
  return getResourceCatalogImageUrl(resource.slug, resource.imageUrl)
}

function humanizeIdentifier(value: string): string {
  return value
    .toLowerCase()
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase())
}
