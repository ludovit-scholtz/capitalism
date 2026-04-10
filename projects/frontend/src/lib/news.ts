import type { GameNewsEntry, GameNewsLocalization } from '@/types'

export const NEWS_EDITOR_LOCALES = ['en', 'sk', 'de'] as const

export type NewsEditorLocale = (typeof NEWS_EDITOR_LOCALES)[number]

export function createEmptyNewsLocalizations(): GameNewsLocalization[] {
  return NEWS_EDITOR_LOCALES.map((locale) => ({
    locale,
    title: '',
    summary: '',
    htmlContent: '',
  }))
}

export function createEmptyNewsDraft(entryType: GameNewsEntry['entryType'] = 'NEWS') {
  return {
    entryId: null as string | null,
    entryType,
    status: 'DRAFT' as GameNewsEntry['status'],
    localizations: createEmptyNewsLocalizations(),
  }
}

export function pickGameNewsLocalization(
  localizations: readonly GameNewsLocalization[],
  preferredLocale: string,
): GameNewsLocalization | null {
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

export function upsertNewsLocalization(
  localizations: readonly GameNewsLocalization[],
  locale: string,
  patch: Partial<GameNewsLocalization>,
): GameNewsLocalization[] {
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