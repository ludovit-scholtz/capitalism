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

  it('returns fallback image for unknown slugs', () => {
    const fallback = getCatalogFallbackImageUrl()
    expect(getProductCatalogImageUrl('missing-product-slug')).toBe(fallback)
    expect(getResourceCatalogImageUrl('missing-resource-slug', null)).toBe(fallback)
  })
})
