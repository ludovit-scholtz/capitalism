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

describe('productImages mapping', () => {
  it('contains image mappings for all seeded resources', () => {
    for (const slug of RESOURCE_IMAGE_SLUGS) {
      expect(hasResourceCatalogImage(slug)).toBe(true)
      expect(getResourceCatalogImageUrl(slug, null)).toContain('.webp')
    }
  })

  it('contains image mappings for all seeded products', () => {
    for (const slug of PRODUCT_IMAGE_SLUGS) {
      expect(hasProductCatalogImage(slug)).toBe(true)
      expect(getProductCatalogImageUrl(slug)).toContain('.webp')
    }
  })

  it('assigns distinct images to each seeded resource and product slug', () => {
    const resourceUrls = RESOURCE_IMAGE_SLUGS.map((slug) => getResourceCatalogImageUrl(slug, null))
    const productUrls = PRODUCT_IMAGE_SLUGS.map((slug) => getProductCatalogImageUrl(slug))
    const allUrls = [...resourceUrls, ...productUrls]
    expect(new Set(allUrls).size).toBe(allUrls.length)
  })

  it('returns fallback image for unknown slugs', () => {
    const fallback = getCatalogFallbackImageUrl()
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
})
