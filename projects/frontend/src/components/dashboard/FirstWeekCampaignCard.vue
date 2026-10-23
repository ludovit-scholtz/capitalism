<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useFirstWeekCampaign } from '@/composables/useFirstWeekCampaign'
import { CAMPAIGN_DAYS, buildDayStrip, currentGoal, goalActionPath } from '@/lib/firstWeekCampaign'
import { formatMoney } from '@/lib/currencyFormat'

const props = defineProps<{
  /** The company that receives rewards and that building goals link to. */
  companyId: string | null
}>()

const { t, locale } = useI18n()
const { campaign, visible, claimingCode, claimError, lastClaim, claim } = useFirstWeekCampaign()

const goal = computed(() => currentGoal(campaign.value))
const strip = computed(() => buildDayStrip(campaign.value))
const actionPath = computed(() => (goal.value ? goalActionPath(goal.value.code, props.companyId) : null))
const nextLockedDay = computed(() => campaign.value?.goals.find((g) => !g.isUnlocked)?.day ?? null)

function money(amount: number, currencyCode: string): string {
  return formatMoney(amount, currencyCode, locale.value)
}

function goalText(code: string, part: 'title' | 'description'): string {
  return t(`firstWeekCampaign.goals.${code}.${part}` as Parameters<typeof t>[0])
}

function dayLabel(day: number, state: string): string {
  return `${t('firstWeekCampaign.dayLabel', { day })}: ${t(`firstWeekCampaign.dayState.${state}` as Parameters<typeof t>[0])}`
}

async function claimCurrent() {
  if (!goal.value) return
  await claim(goal.value.code)
}
</script>

<template>
  <section
    v-if="visible && campaign"
    class="first-week-campaign mb-6 rounded-md border border-divider bg-white/5 px-6 py-4"
    aria-labelledby="first-week-campaign-title"
  >
    <header class="mb-4 flex flex-wrap items-baseline justify-between gap-2">
      <h2 id="first-week-campaign-title" class="text-base font-bold">{{ t('firstWeekCampaign.title') }}</h2>
      <span class="text-[0.8125rem] text-muted">{{ t('firstWeekCampaign.subtitle', { day: campaign.dayNumber, total: CAMPAIGN_DAYS }) }}</span>
    </header>

    <ol class="campaign-strip m-0 mb-4 grid list-none grid-cols-7 gap-2 p-0" :aria-label="t('firstWeekCampaign.stripLabel')">
      <li
        v-for="entry in strip"
        :key="entry.code"
        class="campaign-day flex h-8 items-center justify-center rounded text-xs font-semibold"
        :class="{
          'campaign-day--claimed bg-green-500/20 text-[var(--color-secondary)]': entry.state === 'claimed',
          'campaign-day--claimable bg-brand/25 text-brand': entry.state === 'claimable',
          'campaign-day--open bg-white/10': entry.state === 'open',
          'campaign-day--locked bg-white/[0.04] text-muted': entry.state === 'locked',
          'ring-1 ring-brand': entry.isToday,
        }"
        :aria-label="dayLabel(entry.day, entry.state)"
        :aria-current="entry.isToday ? 'step' : undefined"
      >
        <span aria-hidden="true">{{ entry.state === 'claimed' ? '✓' : entry.day }}</span>
      </li>
    </ol>

    <div v-if="goal" class="campaign-goal flex flex-wrap items-start justify-between gap-4">
      <div class="flex-1 basis-64">
        <strong class="mb-1 block text-sm font-semibold">{{ goalText(goal.code, 'title') }}</strong>
        <p class="mb-2 text-[0.8125rem] leading-[1.45] text-muted">{{ goalText(goal.code, 'description') }}</p>
        <p v-if="goal.target > 1" class="mb-2 text-[0.8125rem]">{{ t('firstWeekCampaign.progress', { progress: goal.progress, target: goal.target }) }}</p>
        <RouterLink v-if="actionPath && !goal.isComplete" :to="actionPath" class="text-[0.8125rem] font-medium text-brand hover:underline">
          {{ t('firstWeekCampaign.goThere') }} →
        </RouterLink>
      </div>
      <div class="flex flex-col items-end gap-2">
        <span class="campaign-reward text-[0.8125rem] font-semibold">{{ t('firstWeekCampaign.reward', { amount: money(goal.rewardAmount, goal.rewardCurrencyCode) }) }}</span>
        <button type="button" class="btn btn-primary" :disabled="!goal.canClaim || claimingCode !== null" @click="claimCurrent">
          {{ claimingCode ? t('firstWeekCampaign.claiming') : t('firstWeekCampaign.claim') }}
        </button>
      </div>
    </div>
    <p v-else class="text-[0.8125rem] text-muted">
      {{ nextLockedDay ? t('firstWeekCampaign.comeBack', { day: nextLockedDay }) : t('firstWeekCampaign.allDone') }}
    </p>

    <p v-if="lastClaim?.claimed" class="campaign-claimed mt-3 text-[0.8125rem] text-[var(--color-secondary)]" role="status">
      {{ t('firstWeekCampaign.claimed', { amount: money(lastClaim.amount, lastClaim.currencyCode) }) }}
    </p>
    <p v-if="claimError" class="mt-3 text-[0.8125rem] text-bad" role="alert">{{ t('firstWeekCampaign.claimFailed') }}</p>
  </section>
</template>
