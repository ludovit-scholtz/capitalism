import { describe, it, expect } from 'vitest'
import { FLAG_SVG_MAP, getFlagSvg, LOCALE_FLAG_MAP, getLocaleFlagCode } from '../countryFlags'

describe('countryFlags', () => {
  describe('FLAG_SVG_MAP', () => {
    it('contains all pre-loaded city country codes', () => {
      // Active seeded cities
      expect(FLAG_SVG_MAP).toHaveProperty('SK')
      expect(FLAG_SVG_MAP).toHaveProperty('CZ')
      expect(FLAG_SVG_MAP).toHaveProperty('AT')
    })

    it('contains all language-switcher country codes', () => {
      expect(FLAG_SVG_MAP).toHaveProperty('GB')
      expect(FLAG_SVG_MAP).toHaveProperty('DE')
    })

    it('contains anticipated roadmap city codes', () => {
      expect(FLAG_SVG_MAP).toHaveProperty('CN')
      expect(FLAG_SVG_MAP).toHaveProperty('IN')
      expect(FLAG_SVG_MAP).toHaveProperty('PL')
      expect(FLAG_SVG_MAP).toHaveProperty('US')
    })

    it('stores non-empty SVG strings', () => {
      for (const code of Object.keys(FLAG_SVG_MAP)) {
        const svg = FLAG_SVG_MAP[code]
        expect(svg).toBeTruthy()
        expect(typeof svg).toBe('string')
        expect(svg!.length).toBeGreaterThan(0)
      }
    })
  })

  describe('getFlagSvg', () => {
    it('returns SVG string for SK (Slovakia)', () => {
      const svg = getFlagSvg('SK')
      expect(svg).toBeTruthy()
      expect(typeof svg).toBe('string')
    })

    it('returns SVG string for CZ (Czech Republic)', () => {
      const svg = getFlagSvg('CZ')
      expect(svg).toBeTruthy()
    })

    it('returns SVG string for AT (Austria)', () => {
      const svg = getFlagSvg('AT')
      expect(svg).toBeTruthy()
    })

    it('returns SVG string for GB (United Kingdom)', () => {
      const svg = getFlagSvg('GB')
      expect(svg).toBeTruthy()
    })

    it('returns SVG string for DE (Germany)', () => {
      const svg = getFlagSvg('DE')
      expect(svg).toBeTruthy()
    })

    it('returns SVG string for US (United States)', () => {
      const svg = getFlagSvg('US')
      expect(svg).toBeTruthy()
    })

    it('returns SVG string for CN (China)', () => {
      const svg = getFlagSvg('CN')
      expect(svg).toBeTruthy()
    })

    it('returns SVG string for IN (India)', () => {
      const svg = getFlagSvg('IN')
      expect(svg).toBeTruthy()
    })

    it('returns null for unknown country code', () => {
      expect(getFlagSvg('ZZ')).toBeNull()
    })

    it('returns null for empty string', () => {
      expect(getFlagSvg('')).toBeNull()
    })

    it('is case-insensitive (lowercase input returns same as uppercase)', () => {
      expect(getFlagSvg('sk')).toBe(getFlagSvg('SK'))
      expect(getFlagSvg('cz')).toBe(getFlagSvg('CZ'))
      expect(getFlagSvg('at')).toBe(getFlagSvg('AT'))
      expect(getFlagSvg('gb')).toBe(getFlagSvg('GB'))
    })

    it('is case-insensitive (mixed case input)', () => {
      expect(getFlagSvg('Sk')).toBe(getFlagSvg('SK'))
      expect(getFlagSvg('De')).toBe(getFlagSvg('DE'))
    })

    it('returns null for unknown code regardless of case', () => {
      expect(getFlagSvg('zz')).toBeNull()
      expect(getFlagSvg('Xx')).toBeNull()
    })
  })

  describe('LOCALE_FLAG_MAP', () => {
    it('maps English locale to GB flag', () => {
      expect(LOCALE_FLAG_MAP['en']).toBe('GB')
    })

    it('maps Slovak locale to SK flag', () => {
      expect(LOCALE_FLAG_MAP['sk']).toBe('SK')
    })

    it('maps German locale to DE flag', () => {
      expect(LOCALE_FLAG_MAP['de']).toBe('DE')
    })

    it('maps French locale to FR flag', () => {
      expect(LOCALE_FLAG_MAP['fr']).toBe('FR')
    })

    it('covers all three primary supported locales (en, sk, de)', () => {
      for (const locale of ['en', 'sk', 'de']) {
        expect(LOCALE_FLAG_MAP).toHaveProperty(locale)
        expect(LOCALE_FLAG_MAP[locale]).toBeTruthy()
      }
    })

    it('all mapped country codes are available in FLAG_SVG_MAP', () => {
      for (const countryCode of Object.values(LOCALE_FLAG_MAP)) {
        // Every locale's flag code must have an actual SVG available
        expect(FLAG_SVG_MAP).toHaveProperty(countryCode)
        expect(FLAG_SVG_MAP[countryCode]).toBeTruthy()
      }
    })
  })

  describe('getLocaleFlagCode', () => {
    it('returns GB for English locale', () => {
      expect(getLocaleFlagCode('en')).toBe('GB')
    })

    it('returns SK for Slovak locale', () => {
      expect(getLocaleFlagCode('sk')).toBe('SK')
    })

    it('returns DE for German locale', () => {
      expect(getLocaleFlagCode('de')).toBe('DE')
    })

    it('returns FR for French locale', () => {
      expect(getLocaleFlagCode('fr')).toBe('FR')
    })

    it('returns null for unknown locale', () => {
      expect(getLocaleFlagCode('xx')).toBeNull()
    })

    it('returns null for empty string', () => {
      expect(getLocaleFlagCode('')).toBeNull()
    })

    it('all returned codes have SVG data in FLAG_SVG_MAP', () => {
      for (const locale of ['en', 'sk', 'de', 'fr']) {
        const code = getLocaleFlagCode(locale)
        expect(code).not.toBeNull()
        expect(getFlagSvg(code!)).not.toBeNull()
      }
    })
  })
})
