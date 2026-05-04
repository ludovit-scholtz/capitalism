<script setup lang="ts">
import { onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import {
  useTutorialContext,
  ALL_MILESTONES,
  MILESTONE_FIRST_RESOURCE_SOLD,
  MILESTONE_FIRST_B2B_TRADE,
  MILESTONE_FIRST_LOAN_TAKEN,
  MILESTONE_FIRST_COMPETITOR_OBSERVED,
  MILESTONE_FIRST_BRAND_ESTABLISHED,
} from '@/composables/useTutorialContext'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const { milestones, loading, error, completedCount, fetchProgress } = useTutorialContext()

// ─── Milestone definitions ────────────────────────────────────────────────────

interface MilestoneDef {
  id: string
  icon: string
  titleKey: string
  descKey: string
  valueKey: string
  bountyPoints: number
  resumeRoute: string
}

const MILESTONE_DEFS: MilestoneDef[] = [
  {
    id: MILESTONE_FIRST_RESOURCE_SOLD,
    icon: '💰',
    titleKey: 'tutorial.milestones.firstResourceSold.title',
    descKey: 'tutorial.milestones.firstResourceSold.desc',
    valueKey: 'tutorial.milestones.firstResourceSold.value',
    bountyPoints: 50,
    resumeRoute: '/dashboard',
  },
  {
    id: MILESTONE_FIRST_B2B_TRADE,
    icon: '🤝',
    titleKey: 'tutorial.milestones.firstB2BTrade.title',
    descKey: 'tutorial.milestones.firstB2BTrade.desc',
    valueKey: 'tutorial.milestones.firstB2BTrade.value',
    bountyPoints: 75,
    resumeRoute: '/exchange',
  },
  {
    id: MILESTONE_FIRST_LOAN_TAKEN,
    icon: '🏦',
    titleKey: 'tutorial.milestones.firstLoanTaken.title',
    descKey: 'tutorial.milestones.firstLoanTaken.desc',
    valueKey: 'tutorial.milestones.firstLoanTaken.value',
    bountyPoints: 60,
    resumeRoute: '/banking',
  },
  {
    id: MILESTONE_FIRST_COMPETITOR_OBSERVED,
    icon: '🔭',
    titleKey: 'tutorial.milestones.firstCompetitorObserved.title',
    descKey: 'tutorial.milestones.firstCompetitorObserved.desc',
    valueKey: 'tutorial.milestones.firstCompetitorObserved.value',
    bountyPoints: 40,
    resumeRoute: '/market-intelligence',
  },
  {
    id: MILESTONE_FIRST_BRAND_ESTABLISHED,
    icon: '⭐',
    titleKey: 'tutorial.milestones.firstBrandEstablished.title',
    descKey: 'tutorial.milestones.firstBrandEstablished.desc',
    valueKey: 'tutorial.milestones.firstBrandEstablished.value',
    bountyPoints: 80,
    resumeRoute: '/dashboard',
  },
]

const totalPoints = computed(() => MILESTONE_DEFS.reduce((sum, m) => sum + m.bountyPoints, 0))
const earnedPoints = computed(() => {
  return MILESTONE_DEFS.filter((def) => isCompleted(def.id)).reduce((sum, m) => sum + m.bountyPoints, 0)
})
const progressPercent = computed(() => {
  const total = (ALL_MILESTONES as readonly string[]).length
  if (total === 0) return 0
  return Math.round((completedCount.value / total) * 100)
})

function isCompleted(milestoneId: string): boolean {
  return milestones.value.find((m) => m.milestone === milestoneId)?.isCompleted ?? false
}

async function handleResume(route: string) {
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }
  await router.push(route)
}

onMounted(async () => {
  if (auth.isAuthenticated) {
    await fetchProgress()
  }
})
</script>

<template>
  <div class="tutorial-view">
    <!-- Page header -->
    <div class="tutorial-header">
      <div class="tutorial-header__inner">
        <h1 class="tutorial-header__title">
          <span class="tutorial-header__icon" aria-hidden="true">🎓</span>
          {{ t('tutorial.title') }}
        </h1>
        <p class="tutorial-header__subtitle">{{ t('tutorial.subtitle') }}</p>

        <!-- Progress bar -->
        <div class="tutorial-progress" aria-label="Tutorial progress">
          <div class="tutorial-progress__bar-wrap">
            <div
              class="tutorial-progress__bar"
              :style="{ width: `${progressPercent}%` }"
              :aria-valuenow="completedCount"
              :aria-valuemax="ALL_MILESTONES.length"
              aria-valuemin="0"
              role="progressbar"
            />
          </div>
          <span class="tutorial-progress__label">
            {{ t('tutorial.progressLabel', { done: completedCount, total: ALL_MILESTONES.length }) }}
          </span>
          <span v-if="auth.isAuthenticated" class="tutorial-progress__points">
            {{ t('tutorial.pointsEarned', { earned: earnedPoints, total: totalPoints }) }}
          </span>
        </div>
      </div>
    </div>

    <!-- Loading / error -->
    <div v-if="loading" class="tutorial-loading" aria-live="polite">
      {{ t('common.loading') }}
    </div>
    <div v-else-if="error" class="tutorial-error" role="alert">
      {{ error }}
    </div>

    <!-- Milestone list -->
    <div v-else class="tutorial-milestones">
      <article
        v-for="def in MILESTONE_DEFS"
        :key="def.id"
        :class="['milestone-card', { 'milestone-card--completed': isCompleted(def.id) }]"
        :aria-label="t(def.titleKey)"
      >
        <div class="milestone-card__icon-col">
          <span class="milestone-card__emoji" aria-hidden="true">{{ def.icon }}</span>
          <span
            :class="['milestone-card__status-icon', isCompleted(def.id) ? 'milestone-card__status-icon--done' : 'milestone-card__status-icon--pending']"
            :aria-label="isCompleted(def.id) ? t('tutorial.completedAria') : t('tutorial.pendingAria')"
          >
            {{ isCompleted(def.id) ? '✓' : '○' }}
          </span>
        </div>

        <div class="milestone-card__content">
          <h2 class="milestone-card__title">{{ t(def.titleKey) }}</h2>
          <p class="milestone-card__desc">{{ t(def.descKey) }}</p>
          <p class="milestone-card__value">{{ t(def.valueKey) }}</p>
        </div>

        <div class="milestone-card__aside">
          <span class="milestone-card__bounty">+{{ def.bountyPoints }} pts</span>
          <button
            v-if="auth.isAuthenticated && !isCompleted(def.id)"
            class="milestone-card__resume-btn"
            type="button"
            @click="handleResume(def.resumeRoute)"
          >
            {{ t('tutorial.resume') }}
          </button>
          <span v-else class="milestone-card__done-badge">
            {{ t('tutorial.done') }}
          </span>
        </div>
      </article>
    </div>

    <!-- Unauthenticated notice -->
    <div v-if="!auth.isAuthenticated" class="tutorial-auth-notice">
      <p>{{ t('tutorial.authNotice') }}</p>
      <RouterLink to="/login" class="tutorial-auth-notice__link">{{ t('tutorial.signIn') }}</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.tutorial-view {
  min-height: 100vh;
  padding-bottom: 80px;
}

/* ─── Header ─────────────────────────────────────────────────────────────── */
.tutorial-header {
  background: linear-gradient(135deg, var(--color-accent, #6366f1) 0%, var(--color-accent-secondary, #4f46e5) 100%);
  padding: 48px 16px 40px;
}

.tutorial-header__inner {
  max-width: 800px;
  margin: 0 auto;
}

.tutorial-header__title {
  font-size: 2rem;
  font-weight: 700;
  color: #fff;
  margin: 0 0 8px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.tutorial-header__icon {
  font-size: 2rem;
}

.tutorial-header__subtitle {
  color: rgba(255, 255, 255, 0.85);
  font-size: 1rem;
  margin: 0 0 24px;
}

/* ─── Progress bar ───────────────────────────────────────────────────────── */
.tutorial-progress {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

.tutorial-progress__bar-wrap {
  flex: 1 1 200px;
  background: rgba(255, 255, 255, 0.25);
  border-radius: 999px;
  height: 10px;
  overflow: hidden;
  min-width: 120px;
}

.tutorial-progress__bar {
  height: 100%;
  background: #fff;
  border-radius: 999px;
  transition: width 0.4s ease;
}

.tutorial-progress__label {
  color: #fff;
  font-size: 0.9rem;
  font-weight: 600;
  white-space: nowrap;
}

.tutorial-progress__points {
  color: rgba(255, 255, 255, 0.8);
  font-size: 0.82rem;
  white-space: nowrap;
}

/* ─── Milestone cards ────────────────────────────────────────────────────── */
.tutorial-milestones {
  max-width: 800px;
  margin: 32px auto;
  padding: 0 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.milestone-card {
  background: var(--color-card, #1e293b);
  border: 1px solid var(--color-divider, #334155);
  border-radius: 12px;
  padding: 20px;
  display: flex;
  gap: 16px;
  align-items: flex-start;
  transition: border-color 0.2s;
}

.milestone-card--completed {
  border-color: var(--color-success, #22c55e);
  opacity: 0.85;
}

.milestone-card__icon-col {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  padding-top: 2px;
}

.milestone-card__emoji {
  font-size: 1.6rem;
}

.milestone-card__status-icon {
  font-size: 1rem;
  font-weight: 700;
  width: 22px;
  height: 22px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
}

.milestone-card__status-icon--done {
  background: var(--color-success, #22c55e);
  color: #fff;
}

.milestone-card__status-icon--pending {
  background: var(--color-divider, #334155);
  color: var(--color-text-secondary, #94a3b8);
}

.milestone-card__content {
  flex: 1;
  min-width: 0;
}

.milestone-card__title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text-primary, #f1f5f9);
  margin: 0 0 6px;
}

.milestone-card__desc {
  font-size: 0.85rem;
  color: var(--color-text-secondary, #94a3b8);
  margin: 0 0 4px;
  line-height: 1.5;
}

.milestone-card__value {
  font-size: 0.8rem;
  color: var(--color-accent, #6366f1);
  margin: 0;
  font-style: italic;
}

.milestone-card__aside {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 10px;
  flex-shrink: 0;
}

.milestone-card__bounty {
  font-size: 0.82rem;
  font-weight: 700;
  color: var(--color-success, #22c55e);
  white-space: nowrap;
}

.milestone-card__resume-btn {
  padding: 7px 16px;
  font-size: 0.82rem;
  font-weight: 600;
  background: var(--color-accent, #6366f1);
  color: #fff;
  border: none;
  border-radius: 7px;
  cursor: pointer;
  white-space: nowrap;
  transition: opacity 0.15s;
}

.milestone-card__resume-btn:hover {
  opacity: 0.85;
}

.milestone-card__done-badge {
  font-size: 0.78rem;
  padding: 4px 10px;
  border-radius: 6px;
  background: var(--color-success-bg, rgba(34, 197, 94, 0.15));
  color: var(--color-success, #22c55e);
  font-weight: 600;
  white-space: nowrap;
}

/* ─── Auth notice ────────────────────────────────────────────────────────── */
.tutorial-auth-notice {
  max-width: 800px;
  margin: 0 auto 32px;
  padding: 16px;
  background: var(--color-card, #1e293b);
  border: 1px solid var(--color-divider, #334155);
  border-radius: 10px;
  text-align: center;
  color: var(--color-text-secondary, #94a3b8);
  font-size: 0.9rem;
}

.tutorial-auth-notice__link {
  display: inline-block;
  margin-top: 8px;
  font-weight: 600;
  color: var(--color-accent, #6366f1);
  text-decoration: none;
}

.tutorial-auth-notice__link:hover {
  text-decoration: underline;
}

/* ─── Loading / error ────────────────────────────────────────────────────── */
.tutorial-loading,
.tutorial-error {
  max-width: 800px;
  margin: 32px auto;
  padding: 0 16px;
  font-size: 0.9rem;
  color: var(--color-text-secondary, #94a3b8);
}

.tutorial-error {
  color: var(--color-error, #ef4444);
}

/* ─── Responsive ─────────────────────────────────────────────────────────── */
@media (max-width: 640px) {
  .milestone-card {
    flex-wrap: wrap;
  }

  .milestone-card__aside {
    flex-direction: row;
    width: 100%;
    justify-content: space-between;
    align-items: center;
  }

  .tutorial-header__title {
    font-size: 1.5rem;
  }
}
</style>
