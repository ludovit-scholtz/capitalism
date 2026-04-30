import { describe, expect, it } from 'vitest'

import { NEWS_EDITOR_LOCALES, createEmptysDraft, createEmptysLocalizations, pickGamesLocalization, upsertsLocalization } from '../news'

describe('news helpers', () => {
  it('creates a draft with the expected locale scaffolding', () => {
    const draft = createEmptysDraft('CHANGELOG')

    expect(draft.entryType).toBe('CHANGELOG')
    expect(draft.status).toBe('DRAFT')
    expect(draft.localizations).toHaveLength(NEWS_EDITOR_LOCALES.length)
    expect(draft.localizations.map((localization) => localization.locale)).toEqual(['en', 'sk', 'de'])
  })

  it('falls back to english when the preferred locale is unavailable', () => {
    const localizations = createEmptysLocalizations().map((localization) => ({
      ...localization,
      title: localization.locale.toUpperCase(),
    }))

    expect(pickGamesLocalization(localizations, 'fr')?.title).toBe('EN')
  })

  it('returns the preferred locale when it exists', () => {
    const localizations = createEmptysLocalizations().map((localization) => ({
      ...localization,
      title: localization.locale.toUpperCase(),
    }))

    expect(pickGamesLocalization(localizations, 'sk')?.title).toBe('SK')
  })

  it('updates an existing localization in place', () => {
    const localizations = upsertsLocalization(createEmptysLocalizations(), 'en', {
      title: 'Patch notes',
      htmlContent: '<p>Hello</p>',
    })

    expect(localizations.find((localization) => localization.locale === 'en')).toEqual({
      locale: 'en',
      title: 'Patch notes',
      summary: '',
      htmlContent: '<p>Hello</p>',
    })
  })
})

