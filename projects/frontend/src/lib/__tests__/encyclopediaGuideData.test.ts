import { describe, expect, it } from 'vitest'
import { localizeGuideImageUrl } from '../encyclopediaGuideData'

describe('localizeGuideImageUrl', () => {
  it('inserts the selected locale folder into guide image paths', () => {
    expect(localizeGuideImageUrl('/onboarding-help/step-1-city.png', 'sk')).toBe('/onboarding-help/sk/step-1-city.png')
  })

  it('falls back to english for unsupported locales', () => {
    expect(localizeGuideImageUrl('/forex-help/step-1-swap-overview-1920x1080.png', 'fr')).toBe('/forex-help/en/step-1-swap-overview-1920x1080.png')
  })

  it('leaves non-guide paths unchanged', () => {
    expect(localizeGuideImageUrl('/fallback.png', 'de')).toBe('/fallback.png')
  })
})
