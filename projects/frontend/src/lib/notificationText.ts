import type { PlayerNotificationItem } from '@/types'

type TranslateFn = (key: string, params?: Record<string, unknown>) => string
type HasKeyFn = (key: string) => boolean

function parseNotificationParams(bodyParamsJson?: string | null): Record<string, unknown> | null {
  if (!bodyParamsJson?.trim()) {
    return {}
  }

  try {
    const parsed = JSON.parse(bodyParamsJson) as unknown
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>
    }
  } catch {
    return null
  }

  return null
}

function resolveNotificationField(
  fallbackValue: string,
  key: string | null | undefined,
  bodyParamsJson: string | null | undefined,
  translate: TranslateFn,
  hasKey: HasKeyFn,
): string {
  const fallback = fallbackValue.trim()
  if (!key || !hasKey(key)) {
    return fallback
  }

  const params = parseNotificationParams(bodyParamsJson)
  if (params === null) {
    return fallback
  }

  return translate(key, params)
}

export function resolveNotificationCopy(
  item: PlayerNotificationItem,
  translate: TranslateFn,
  hasKey: HasKeyFn,
): { title: string; message: string } {
  return {
    title: resolveNotificationField(item.title, item.titleKey, item.bodyParamsJson, translate, hasKey),
    message: resolveNotificationField(item.message, item.bodyKey, item.bodyParamsJson, translate, hasKey),
  }
}