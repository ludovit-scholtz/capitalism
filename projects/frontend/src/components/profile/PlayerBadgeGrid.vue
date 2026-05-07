<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { getProfileBadgeIcon, profileBadgeCatalog } from '@/lib/profileBadges'

const { t } = useI18n()

export interface PlayerBadge {
  id: string
  badgeType: string
  rarity: string
  unlockCondition: string
  unlockedAtUtc: string
  unlockedAtTick: number
}

const props = defineProps<{
  badges: PlayerBadge[]
  loading?: boolean
}>()

// ── Rarity helpers ──────────────────────────────────────────────────────────

const rarityClass = (rarity: string) => {
  switch (rarity) {
    case 'LEGENDARY':
      return 'badge-legendary'
    case 'EPIC':
      return 'badge-epic'
    case 'RARE':
      return 'badge-rare'
    default:
      return 'badge-common'
  }
}

const rarityEmoji = (rarity: string) => {
  switch (rarity) {
    case 'LEGENDARY':
      return '🔴'
    case 'EPIC':
      return '🟡'
    case 'RARE':
      return '🟣'
    default:
      return '🔵'
  }
}

const badgeLabel = (badgeType: string) =>
  t(`playerProfile.badges.${badgeType}`, badgeType.replace(/_/g, ' '))

function formatUnlockDate(utc: string): string {
  return new Date(utc).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

const sortedBadges = computed(() => {
  const earned = [...props.badges].sort((a, b) => {
    const rarityOrder = { LEGENDARY: 0, EPIC: 1, RARE: 2, COMMON: 3 }
    const ra = rarityOrder[a.rarity as keyof typeof rarityOrder] ?? 4
    const rb = rarityOrder[b.rarity as keyof typeof rarityOrder] ?? 4
    if (ra !== rb) return ra - rb
    return new Date(a.unlockedAtUtc).getTime() - new Date(b.unlockedAtUtc).getTime()
  })

  const earnedByType = new Map(earned.map((badge) => [badge.badgeType, badge]))
  const locked = profileBadgeCatalog
    .filter((item) => !earnedByType.has(item.badgeType))
    .map((item) => ({
      id: `locked-${item.badgeType}`,
      badgeType: item.badgeType,
      rarity: 'LOCKED',
      unlockCondition: t(
        `playerProfile.badgeUnlockConditions.${item.badgeType}`,
        t('playerProfile.badgeLockedHint'),
      ),
      unlockedAtUtc: '',
      unlockedAtTick: 0,
      locked: true,
    }))

  return [
    ...earned.map((badge) => ({ ...badge, locked: false })),
    ...locked,
  ]
})
</script>

<template>
  <div class="badge-grid-container">
    <!-- Loading skeleton -->
    <div v-if="loading" class="badge-grid">
      <div v-for="i in 8" :key="i" class="badge-skeleton" aria-hidden="true" />
    </div>

    <!-- Empty state -->
    <!-- Badge grid -->
    <div v-else class="badge-grid" role="list" :aria-label="t('playerProfile.badgeGridLabel')">
      <div
        v-for="badge in sortedBadges"
        :key="badge.id"
        class="badge-card"
        :class="[badge.locked ? 'badge-locked' : rarityClass(badge.rarity)]"
        role="listitem"
        :aria-label="`${badgeLabel(badge.badgeType)} (${badge.locked ? t('playerProfile.locked') : badge.rarity})`"
      >
        <!-- Tooltip wrapper -->
        <div class="badge-tooltip-anchor">
          <div class="badge-icon" aria-hidden="true">{{ getProfileBadgeIcon(badge.badgeType) }}</div>
          <div class="badge-name">{{ badgeLabel(badge.badgeType) }}</div>
          <div class="badge-rarity-label">
            <template v-if="badge.locked">🔒 {{ t('playerProfile.locked') }}</template>
            <template v-else>{{ rarityEmoji(badge.rarity) }} {{ badge.rarity }}</template>
          </div>

          <!-- Tooltip -->
          <div class="badge-tooltip" role="tooltip">
            <strong>{{ badgeLabel(badge.badgeType) }}</strong>
            <p class="badge-tooltip-condition">{{ badge.unlockCondition }}</p>
            <p v-if="!badge.locked" class="badge-tooltip-date">
              {{ t('playerProfile.unlockedOn') }}: {{ formatUnlockDate(badge.unlockedAtUtc) }}
            </p>
            <p v-if="!badge.locked" class="badge-tooltip-tick">
              {{ t('playerProfile.atTick') }}: {{ badge.unlockedAtTick.toLocaleString() }}
            </p>
          </div>
        </div>
      </div>
    </div>

    <!-- Badge count summary -->
    <p class="badge-summary">
      {{ t('playerProfile.badgeCount', { count: badges.length }) }}
    </p>
  </div>
</template>

<style scoped>
.badge-grid-container {
  width: 100%;
}

.badge-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-bottom: 8px;
}

@media (max-width: 640px) {
  .badge-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

.badge-card {
  border-radius: 12px;
  padding: 14px 10px;
  text-align: center;
  border: 2px solid transparent;
  transition:
    transform 0.15s ease,
    box-shadow 0.15s ease;
  position: relative;
  cursor: default;
}

.badge-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

/* Rarity tiers */
.badge-common {
  background: linear-gradient(135deg, #1a2a4a 0%, #0d1b35 100%);
  border-color: #2a5bc4;
}
.badge-rare {
  background: linear-gradient(135deg, #2a1a4a 0%, #1a0d35 100%);
  border-color: #7c3aed;
}
.badge-epic {
  background: linear-gradient(135deg, #3a2a0a 0%, #251a05 100%);
  border-color: #ca8a04;
}
.badge-legendary {
  background: linear-gradient(135deg, #3a0a0a 0%, #250505 100%);
  border-color: #dc2626;
  box-shadow: 0 0 12px rgba(220, 38, 38, 0.25);
}

.badge-icon {
  font-size: 48px;
  margin-bottom: 6px;
  display: block;
  filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.5));
}

.badge-locked {
  background: linear-gradient(135deg, #161b22 0%, #0f1319 100%);
  border-color: #374151;
  opacity: 0.7;
}

.badge-name {
  font-size: 11px;
  font-weight: 600;
  color: var(--color-text-primary, #f1f5f9);
  line-height: 1.3;
  margin-bottom: 2px;
  word-break: break-word;
}

.badge-rarity-label {
  font-size: 10px;
  color: var(--color-text-muted, #94a3b8);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

/* Skeleton loading */
.badge-skeleton {
  border-radius: 12px;
  height: 100px;
  background: linear-gradient(90deg, #1e293b 25%, #2d3f5e 50%, #1e293b 75%);
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

/* Tooltip */
.badge-tooltip-anchor {
  position: relative;
}

.badge-tooltip {
  display: none;
  position: absolute;
  bottom: calc(100% + 10px);
  left: 50%;
  transform: translateX(-50%);
  background: var(--color-surface-elevated, #1e293b);
  border: 1px solid var(--color-border, #334155);
  border-radius: 8px;
  padding: 10px 12px;
  min-width: 200px;
  max-width: 260px;
  z-index: 100;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.5);
  pointer-events: none;
  text-align: left;
}

.badge-tooltip strong {
  font-size: 13px;
  color: var(--color-text-primary, #f1f5f9);
  display: block;
  margin-bottom: 4px;
}

.badge-tooltip-condition {
  font-size: 12px;
  color: var(--color-text-secondary, #cbd5e1);
  margin: 0 0 6px;
  line-height: 1.4;
}

.badge-tooltip-date,
.badge-tooltip-tick {
  font-size: 11px;
  color: var(--color-text-muted, #94a3b8);
  margin: 0;
}

.badge-card:hover .badge-tooltip {
  display: block;
}

/* Empty state */
.badge-empty-state {
  text-align: center;
  padding: 24px;
  color: var(--color-text-muted, #94a3b8);
  border: 1px dashed var(--color-border, #334155);
  border-radius: 12px;
}

.badge-empty-icon {
  font-size: 32px;
  display: block;
  margin-bottom: 8px;
  opacity: 0.5;
}

/* Summary */
.badge-summary {
  font-size: 12px;
  color: var(--color-text-muted, #94a3b8);
  text-align: right;
  margin: 4px 0 0;
}
</style>
