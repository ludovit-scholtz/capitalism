import type { GamesEntry, GamesLocalization } from '@/types'

export const NEWS_EDITOR_LOCALES = ['en', 'sk', 'de'] as const

export type sEditorLocale = (typeof NEWS_EDITOR_LOCALES)[number]

export function createEmptysLocalizations(): GamesLocalization[] {
  return NEWS_EDITOR_LOCALES.map((locale) => ({
    locale,
    title: '',
    summary: '',
    htmlContent: '',
  }))
}

export function createEmptysDraft(entryType: GamesEntry['entryType'] = 'NEWS') {
  return {
    entryId: null as string | null,
    entryType,
    status: 'DRAFT' as GamesEntry['status'],
    localizations: createEmptysLocalizations(),
  }
}

export function pickGamesLocalization(localizations: readonly GamesLocalization[], preferredLocale: string): GamesLocalization | null {
  if (localizations.length === 0) {
    return null
  }

  const normalizedLocale = preferredLocale.toLowerCase()

  return (
    localizations.find((localization) => localization.locale.toLowerCase() === normalizedLocale) ??
    localizations.find((localization) => localization.locale.toLowerCase() === 'en') ??
    localizations[0] ??
    null
  )
}

export function upsertsLocalization(localizations: readonly GamesLocalization[], locale: string, patch: Partial<GamesLocalization>): GamesLocalization[] {
  const normalizedLocale = locale.toLowerCase()
  const existing = localizations.find((localization) => localization.locale.toLowerCase() === normalizedLocale)

  if (!existing) {
    return [
      ...localizations,
      {
        locale: normalizedLocale,
        title: patch.title ?? '',
        summary: patch.summary ?? '',
        htmlContent: patch.htmlContent ?? '',
      },
    ]
  }

  return localizations.map((localization) => {
    if (localization.locale.toLowerCase() !== normalizedLocale) {
      return localization
    }

    return {
      ...localization,
      ...patch,
      locale: normalizedLocale,
    }
  })
}

