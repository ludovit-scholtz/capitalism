<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Company } from '@/types'

interface Props {
  company: Company
  revenue: number
  /** Backend-authoritative net income (after tax). Used for profitability decisions. */
  netIncome: number
}

const props = defineProps<Props>()
const { t } = useI18n()

const hasBuildings = computed(() => props.company.buildings.length > 0)
const hasFactory = computed(() => props.company.buildings.some((b) => b.type === 'FACTORY'))
const hasShop = computed(() => props.company.buildings.some((b) => b.type === 'SALES_SHOP'))
const isStarter = computed(() => hasFactory.value && hasShop.value && props.company.buildings.length <= 2)
const hasRevenue = computed(() => props.revenue > 0)
/** Profitability is determined by the backend's netIncome (includes taxes, not a frontend estimate). */
const isProfitable = computed(() => props.netIncome > 0)

interface GuidanceItem {
  icon: string
  title: string
  body: string
  linkTo?: string
  linkLabel?: string
}

const items = computed<GuidanceItem[]>(() => {
  const result: GuidanceItem[] = []

  if (!hasBuildings.value) {
    result.push({
      icon: '🏗️',
      title: t('starterGuidance.noBuildings.title'),
      body: t('starterGuidance.noBuildings.body'),
      linkTo: `/buy-building/${props.company.id}`,
      linkLabel: t('starterGuidance.noBuildings.action'),
    })
    return result
  }

  if (!hasRevenue.value) {
    result.push({
      icon: '⏳',
      title: t('starterGuidance.awaitingRevenue.title'),
      body: t('starterGuidance.awaitingRevenue.body'),
    })
  } else if (!isProfitable.value) {
    result.push({
      icon: '📉',
      title: t('starterGuidance.unprofitable.title'),
      body: t('starterGuidance.unprofitable.body'),
    })
  } else {
    result.push({
      icon: '📈',
      title: t('starterGuidance.profitable.title'),
      body: t('starterGuidance.profitable.body'),
    })
  }

  if (hasFactory.value) {
    const factory = props.company.buildings.find((b) => b.type === 'FACTORY')
    if (factory) {
      result.push({
        icon: '🏭',
        title: t('starterGuidance.checkFactory.title'),
        body: t('starterGuidance.checkFactory.body'),
        linkTo: `/building/${factory.id}`,
        linkLabel: t('starterGuidance.checkFactory.action'),
      })
    }
  }

  if (hasShop.value) {
    const shop = props.company.buildings.find((b) => b.type === 'SALES_SHOP')
    if (shop) {
      result.push({
        icon: '🏪',
        title: t('starterGuidance.checkShop.title'),
        body: t('starterGuidance.checkShop.body'),
        linkTo: `/building/${shop.id}`,
        linkLabel: t('starterGuidance.checkShop.action'),
      })
    }
  }

  if (isProfitable.value && isStarter.value) {
    result.push({
      icon: '🚀',
      title: t('starterGuidance.expand.title'),
      body: t('starterGuidance.expand.body'),
      linkTo: `/buy-building/${props.company.id}`,
      linkLabel: t('starterGuidance.expand.action'),
    })
  }

  // Limit to 3 items to avoid overwhelming the player with too many action items at once.
  return result.slice(0, 3)
})
</script>

<template>
  <div class="starter-guidance rounded-md border border-divider bg-white/5 px-5 py-4" aria-labelledby="starter-guidance-title">
    <h3 id="starter-guidance-title" class="starter-guidance-title mb-3 text-[0.8125rem] font-bold uppercase tracking-[0.06em] text-muted">
      {{ t('starterGuidance.title') }}
    </h3>
    <ul class="guidance-list flex list-none flex-col gap-3 p-0 m-0">
      <li v-for="(item, i) in items" :key="i" class="guidance-item flex items-start gap-3">
        <span class="guidance-icon mt-0.5 shrink-0 text-xl" aria-hidden="true">{{ item.icon }}</span>
        <div class="guidance-content flex-1">
          <strong class="guidance-item-title mb-0.5 block text-sm font-semibold">{{ item.title }}</strong>
          <p class="guidance-item-body mb-1 text-[0.8125rem] leading-[1.45] text-muted">{{ item.body }}</p>
          <RouterLink v-if="item.linkTo && item.linkLabel" :to="item.linkTo" class="guidance-link text-[0.8125rem] font-medium text-brand hover:underline">
            {{ item.linkLabel }} →
          </RouterLink>
        </div>
      </li>
    </ul>
  </div>
</template>
