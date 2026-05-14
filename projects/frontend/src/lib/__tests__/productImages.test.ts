import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import {
  getCatalogFallbackImageUrl,
  getProductCatalogImageUrl,
  getResourceCatalogImageUrl,
  hasProductCatalogImage,
  hasResourceCatalogImage,
  PRODUCT_IMAGE_SLUGS,
  RESOURCE_IMAGE_SLUGS,
} from '../productImages'

const assetDir = new URL('../../assets/products/', import.meta.url)

function expectSvgImageUrl(url: string) {
  expect(url).toMatch(/\.svg|data:image\/svg\+xml/i)
}

describe('productImages mapping', () => {
  it('contains image mappings for all seeded resources', () => {
    for (const slug of RESOURCE_IMAGE_SLUGS) {
      expect(hasResourceCatalogImage(slug)).toBe(true)
      expectSvgImageUrl(getResourceCatalogImageUrl(slug, null))
    }
  })

  it('contains image mappings for all seeded products', () => {
    for (const slug of PRODUCT_IMAGE_SLUGS) {
      expect(hasProductCatalogImage(slug)).toBe(true)
      expectSvgImageUrl(getProductCatalogImageUrl(slug))
    }
  })

  it('assigns distinct images to each seeded resource and product slug', () => {
    const resourceUrls = RESOURCE_IMAGE_SLUGS.map((slug) => getResourceCatalogImageUrl(slug, null))
    const productUrls = PRODUCT_IMAGE_SLUGS.map((slug) => getProductCatalogImageUrl(slug))
    const allUrls = [...resourceUrls, ...productUrls]
    expect(new Set(allUrls).size).toBe(allUrls.length)
  })

  it('keeps resource and product catalog slug lists unique and non-overlapping', () => {
    const resourceSlugs = [...RESOURCE_IMAGE_SLUGS]
    const productSlugs = [...PRODUCT_IMAGE_SLUGS]
    const allSlugs = [...resourceSlugs, ...productSlugs]

    expect(new Set(resourceSlugs).size).toBe(resourceSlugs.length)
    expect(new Set(productSlugs).size).toBe(productSlugs.length)
    expect(new Set(allSlugs).size).toBe(allSlugs.length)
  })

  it('keeps the fallback asset reserved outside catalog slug mappings', () => {
    expect(RESOURCE_IMAGE_SLUGS).not.toContain('fallback')
    expect(PRODUCT_IMAGE_SLUGS).not.toContain('fallback')
    expect(hasResourceCatalogImage('fallback')).toBe(false)
    expect(hasProductCatalogImage('fallback')).toBe(false)
    expectSvgImageUrl(getCatalogFallbackImageUrl())
  })

  it('keeps the fallback image URL distinct from every dedicated catalog image', () => {
    const fallback = getCatalogFallbackImageUrl()
    const dedicatedUrls = [
      ...RESOURCE_IMAGE_SLUGS.map((slug) => getResourceCatalogImageUrl(slug, null)),
      ...PRODUCT_IMAGE_SLUGS.map((slug) => getProductCatalogImageUrl(slug)),
    ]

    expect(dedicatedUrls).not.toContain(fallback)
    expect(new Set([...dedicatedUrls, fallback]).size).toBe(dedicatedUrls.length + 1)
  })

  it('returns fallback image for unknown slugs', () => {
    const fallback = getCatalogFallbackImageUrl()
    expect(hasProductCatalogImage('missing-product-slug')).toBe(false)
    expect(hasResourceCatalogImage('missing-resource-slug')).toBe(false)
    expect(getProductCatalogImageUrl('missing-product-slug')).toBe(fallback)
    expect(getResourceCatalogImageUrl('missing-resource-slug', null)).toBe(fallback)
  })

  it('uses existing resource image when slug is unknown and keeps mapped image precedence when known', () => {
    const existing = 'https://example.com/existing-resource-image.webp'
    const knownSlug = RESOURCE_IMAGE_SLUGS[0]
    const knownMappedImage = getResourceCatalogImageUrl(knownSlug, null)

    expect(getResourceCatalogImageUrl('missing-resource-slug', existing)).toBe(existing)
    expect(getResourceCatalogImageUrl(knownSlug, existing)).toBe(knownMappedImage)
  })

  it('never returns global fallback for seeded slugs', () => {
    const fallback = getCatalogFallbackImageUrl()

    for (const slug of RESOURCE_IMAGE_SLUGS) {
      expect(getResourceCatalogImageUrl(slug, null)).not.toBe(fallback)
    }

    for (const slug of PRODUCT_IMAGE_SLUGS) {
      expect(getProductCatalogImageUrl(slug)).not.toBe(fallback)
    }
  })

  it('keeps one dedicated file per seeded slug', () => {
    for (const slug of RESOURCE_IMAGE_SLUGS) {
      expect(existsSync(new URL(`${slug}.svg`, assetDir))).toBe(true)
    }

    for (const slug of PRODUCT_IMAGE_SLUGS) {
      expect(existsSync(new URL(`${slug}.svg`, assetDir))).toBe(true)
    }
  })

  it('keeps dedicated subject files for the specifically reported product regressions', () => {
    expect(existsSync(new URL('analgesic-syrup.svg', assetDir))).toBe(true)
    expect(existsSync(new URL('antibiotic.svg', assetDir))).toBe(true)
    expect(existsSync(new URL('antiseptic-gel.svg', assetDir))).toBe(true)
    expect(existsSync(new URL('aspirin.svg', assetDir))).toBe(true)
    expect(existsSync(new URL('assembly-pallet.svg', assetDir))).toBe(true)
    expect(existsSync(new URL('wooden-bed.svg', assetDir))).toBe(true)
  })

  it('keeps the specifically reported regressions classified as product-only mappings', () => {
    const reportedProductSlugs = [
      'analgesic-syrup',
      'antibiotic',
      'antiseptic-gel',
      'aspirin',
      'assembly-pallet',
      'wooden-bed',
    ]

    for (const slug of reportedProductSlugs) {
      expect(PRODUCT_IMAGE_SLUGS).toContain(slug)
      expect(RESOURCE_IMAGE_SLUGS).not.toContain(slug)
      expect(hasProductCatalogImage(slug)).toBe(true)
    }
  })

  it('keeps the specifically reported product regressions out of resource classification while preserving slug asset resolution', () => {
    const reportedProductSlugs = [
      'analgesic-syrup',
      'antibiotic',
      'antiseptic-gel',
      'aspirin',
      'assembly-pallet',
      'wooden-bed',
    ]

    for (const slug of reportedProductSlugs) {
      expect(hasResourceCatalogImage(slug)).toBe(false)
      expect(getResourceCatalogImageUrl(slug, null)).toBe(getProductCatalogImageUrl(slug))
    }
  })

  it('keeps catalog SVG artwork free of embedded visible text elements', () => {
    const svgFiles = readdirSync(assetDir).filter((file) => file.endsWith('.svg'))
    expect(svgFiles.length).toBeGreaterThan(0)

    for (const file of svgFiles) {
      const source = readFileSync(new URL(file, assetDir), 'utf8')
      expect(source).not.toMatch(/<text[\\s>]/i)
      expect(source).not.toMatch(/<textPath[\\s>]/i)
      expect(source).not.toMatch(/<title[\\s>]/i)
    }
  })

  it('keeps the catalog asset directory SVG-only with one mapped asset plus fallback', () => {
    const files = readdirSync(assetDir)
    const svgFiles = files.filter((file) => file.endsWith('.svg')).sort()
    const legacyWebpFiles = files.filter((file) => file.endsWith('.webp'))
    const expectedSvgFiles = [...RESOURCE_IMAGE_SLUGS, ...PRODUCT_IMAGE_SLUGS, 'fallback']
      .map((slug) => `${slug}.svg`)
      .sort()

    expect(legacyWebpFiles).toEqual([])
    expect(svgFiles).toEqual(expectedSvgFiles)
  })
})
