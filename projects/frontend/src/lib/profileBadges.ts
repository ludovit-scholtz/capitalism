export interface ProfileBadgeCatalogItem {
  badgeType: string
  icon: string
}

export const profileBadgeCatalog: ProfileBadgeCatalogItem[] = [
  { badgeType: 'FIRST_B2B_TRADE', icon: '🤝' },
  { badgeType: 'LOAN_MASTER', icon: '🏦' },
  { badgeType: 'MEDIA_MOGUL', icon: '📺' },
  { badgeType: 'BANK_BARON', icon: '💳' },
  { badgeType: 'MARKET_DOMINATOR_V2', icon: '👑' },
  { badgeType: 'TOP_RANK', icon: '🥇' },
  { badgeType: 'WEALTH_MILESTONE', icon: '💰' },
  { badgeType: 'FIRST_MILLION', icon: '💰' },
  { badgeType: 'MONOPOLIST', icon: '🏛️' },
  { badgeType: 'MASTER_TRADER', icon: '📈' },
  { badgeType: 'POWER_MAGNATE', icon: '⚡' },
  { badgeType: 'CITY_PIONEER', icon: '🌆' },
  { badgeType: 'EXPORT_CHAMPION', icon: '🚢' },
  { badgeType: 'INDUSTRY_LEADER', icon: '🏭' },
  { badgeType: 'MARKET_DOMINATOR', icon: '👑' },
  { badgeType: 'RANK_CLIMBER', icon: '🚀' },
  { badgeType: 'LEGENDARY_TYCOON', icon: '💎' },
]

const iconByBadgeType = Object.fromEntries(
  profileBadgeCatalog.map((item) => [item.badgeType, item.icon]),
) as Record<string, string>

export function getProfileBadgeIcon(badgeType: string): string {
  return iconByBadgeType[badgeType] ?? '🏅'
}
