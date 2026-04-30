import type { SubscriptionInfo } from './masterApi'

type TranslateFn = (key: string, params?: Record<string, unknown>) => string

/**
 * Returns a human-readable label for the subscription tier.
 */
export function formatTierLabel(tier: SubscriptionInfo['tier'], t?: TranslateFn): string {
  switch (tier) {
    case 'PRO':
      return t ? t('subscription.tierPro') : 'Pro'
    default:
      return t ? t('subscription.tierFree') : 'Free'
  }
}

/**
 * Returns a human-readable label for the subscription status.
 */
export function formatStatusLabel(sub: SubscriptionInfo, t?: TranslateFn): string {
  if (!sub.isActive && sub.status === 'NONE') {
    return t ? t('subscription.statusNoActive') : 'No active subscription'
  }
  if (sub.status === 'EXPIRED') return t ? t('subscription.statusExpired') : 'Expired'
  if (sub.isActive) return t ? t('subscription.statusActive') : 'Active'
  return t ? t('subscription.statusInactive') : 'Inactive'
}

/**
 * Returns a description of days remaining or when the subscription expires.
 */
export function formatRenewalNote(sub: SubscriptionInfo, t?: TranslateFn): string {
  if (!sub.isActive || sub.expiresAtUtc === null) return ''
  const days = sub.daysRemaining ?? 0
  if (days <= 0) return t ? t('subscription.expiresToday') : 'Expires today'
  if (days === 1) return t ? t('subscription.expiresTomorrow') : 'Expires tomorrow'
  if (days <= 30) {
    return t ? t('subscription.expiresInDays', { days }) : `Expires in ${days} days`
  }
  const expiry = new Date(sub.expiresAtUtc)
  const locale = typeof navigator !== 'undefined' ? navigator.language : 'en-US'
  const formattedDate = expiry.toLocaleDateString(locale, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
  return t ? t('subscription.renewsOn', { date: formattedDate }) : `Renews on ${formattedDate}`
}

/**
 * Returns the primary CTA label for a subscription state.
 */
export function formatProlongLabel(sub: SubscriptionInfo, t?: TranslateFn): string {
  if (!sub.isActive || sub.status === 'EXPIRED') {
    return t ? t('subscription.subscribeToPro') : 'Subscribe to Pro'
  }
  const days = sub.daysRemaining ?? 0
  if (days <= 30) return t ? t('subscription.extendSubscription') : 'Extend subscription'
  return t ? t('subscription.prolongSubscription') : 'Prolong subscription'
}
