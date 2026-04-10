import { describe, expect, it } from 'vitest'

import {
  NEWS_EDITOR_LOCALES,
  createEmptyNewsDraft,
  createEmptyNewsLocalizations,
  pickGameNewsLocalization,
  upsertNewsLocalization,
} from '../news'

describe('news helpers', () => {
  it('creates a draft with the expected locale scaffolding', () => {
    const draft = createEmptyNewsDraft('CHANGELOG')

    expect(draft.entryType).toBe('CHANGELOG')
    expect(draft.status).toBe('DRAFT')
    expect(draft.localizations).toHaveLength(NEWS_EDITOR_LOCALES.length)
    expect(draft.localizations.map((localization) => localization.locale)).toEqual([
      'en',
      'sk',
      'de',
    ])
  })

  it('falls back to english when the preferred locale is unavailable', () => {
    const localizations = createEmptyNewsLocalizations().map((localization) => ({
      ...localization,
      title: localization.locale.toUpperCase(),
    }))

    expect(pickGameNewsLocalization(localizations, 'fr')?.title).toBe('EN')
  })

  it('returns the preferred locale when it exists', () => {
    const localizations = createEmptyNewsLocalizations().map((localization) => ({
      ...localization,
      title: localization.locale.toUpperCase(),
    }))

    expect(pickGameNewsLocalization(localizations, 'sk')?.title).toBe('SK')
  })

  it('updates an existing localization in place', () => {
    const localizations = upsertNewsLocalization(createEmptyNewsLocalizations(), 'en', {
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