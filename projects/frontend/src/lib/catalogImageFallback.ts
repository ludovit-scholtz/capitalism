import { getCatalogFallbackImageUrl } from '@/lib/productImages'

export function onCatalogImageError(event: Event): void {
  const image = event.currentTarget as HTMLImageElement | null
  if (!image) return
  if (image.dataset.catalogFallbackApplied === '1') return
  image.dataset.catalogFallbackApplied = '1'
  image.src = getCatalogFallbackImageUrl()
}
