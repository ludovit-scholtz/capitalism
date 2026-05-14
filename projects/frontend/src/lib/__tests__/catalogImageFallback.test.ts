import { describe, expect, it, vi } from 'vitest'

vi.mock('@/lib/productImages', () => ({
  getCatalogFallbackImageUrl: () => '/assets/products/fallback.webp',
}))

import { onCatalogImageError } from '@/lib/catalogImageFallback'

describe('onCatalogImageError', () => {
  it('applies fallback src and marks fallback as applied', () => {
    const image = {
      dataset: {},
      src: '/assets/products/missing.webp',
    } as unknown as HTMLImageElement

    onCatalogImageError({ currentTarget: image } as unknown as Event)

    expect(image.dataset.catalogFallbackApplied).toBe('1')
    expect(image.src).toBe('/assets/products/fallback.webp')
  })

  it('does not overwrite src when fallback already applied', () => {
    const image = {
      dataset: { catalogFallbackApplied: '1' },
      src: '/assets/products/already-fallback.webp',
    } as unknown as HTMLImageElement

    onCatalogImageError({ currentTarget: image } as unknown as Event)

    expect(image.src).toBe('/assets/products/already-fallback.webp')
  })

  it('is a no-op when event has no currentTarget image', () => {
    expect(() => onCatalogImageError({ currentTarget: null } as unknown as Event)).not.toThrow()
  })
})
