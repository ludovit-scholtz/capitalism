<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

const { t } = useI18n()

const activeTopic = ref<
  'getting-started' | 'buildings-guide' | 'economy-overview' | 'email-system'
>('getting-started')

const topics = computed(() => [
  { key: 'getting-started' as const, label: t('docs.topicGettingStarted') },
  { key: 'buildings-guide' as const, label: t('docs.topicBuildingsGuide') },
  { key: 'economy-overview' as const, label: t('docs.topicEconomyOverview') },
  { key: 'email-system' as const, label: t('docs.topicEmailSystem') },
])

const navItems = computed(() => [
  { label: t('nav.home'), to: '/' },
  { label: t('nav.gameServers'), to: '/game-servers' },
])
</script>

<template>
  <div>
    <ViewJumbotron
      :kicker="t('docs.kicker')"
      :title="t('docs.title')"
      :subtitle="t('docs.subtitle')"
      variant="default"
    />
    <ViewSubnav :items="navItems" aria-label="Documentation navigation" />

    <main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
      <div class="grid gap-6 lg:grid-cols-[240px_minmax(0,1fr)]">
        <!-- Sidebar navigation -->
        <nav
          class="sticky top-20 self-start rounded-xl border border-divider bg-card p-4"
          aria-label="Documentation topics"
        >
          <p class="mb-3 text-xs font-semibold uppercase tracking-[0.12em] text-muted">
            {{ t('docs.topicsLabel') }}
          </p>
          <ul class="flex flex-col gap-1">
            <li v-for="topic in topics" :key="topic.key">
              <button
                type="button"
                class="w-full rounded-lg px-3 py-2 text-left text-sm transition-colors"
                :class="
                  activeTopic === topic.key
                    ? 'bg-accent/15 font-semibold text-accent'
                    : 'text-body hover:bg-overlay/40'
                "
                @click="activeTopic = topic.key"
              >
                {{ topic.label }}
              </button>
            </li>
          </ul>
        </nav>

        <!-- Content area -->
        <article class="card prose-doc p-6 lg:p-8">
          <!-- Getting Started -->
          <template v-if="activeTopic === 'getting-started'">
            <h1 class="mb-4 text-2xl font-bold text-body">{{ t('docs.topicGettingStarted') }}</h1>

            <section class="doc-section">
              <h2>{{ t('docs.gs.welcomeTitle') }}</h2>
              <p>{{ t('docs.gs.welcomeBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.gs.accountTitle') }}</h2>
              <p>{{ t('docs.gs.accountBody') }}</p>
              <ol class="doc-list doc-ordered">
                <li>{{ t('docs.gs.step1') }}</li>
                <li>{{ t('docs.gs.step2') }}</li>
                <li>{{ t('docs.gs.step3') }}</li>
                <li>{{ t('docs.gs.step4') }}</li>
              </ol>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.gs.firstStepsTitle') }}</h2>
              <p>{{ t('docs.gs.firstStepsBody') }}</p>
              <ul class="doc-list">
                <li>{{ t('docs.gs.tip1') }}</li>
                <li>{{ t('docs.gs.tip2') }}</li>
                <li>{{ t('docs.gs.tip3') }}</li>
                <li>{{ t('docs.gs.tip4') }}</li>
              </ul>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.gs.proTitle') }}</h2>
              <p>{{ t('docs.gs.proBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.gs.deleteTitle') }}</h2>
              <p>{{ t('docs.gs.deleteBody') }}</p>
              <ul class="doc-list">
                <li>{{ t('docs.gs.deleteList1') }}</li>
                <li>{{ t('docs.gs.deleteList2') }}</li>
                <li>{{ t('docs.gs.deleteList3') }}</li>
                <li>{{ t('docs.gs.deleteList4') }}</li>
              </ul>
              <p>{{ t('docs.gs.deleteEmails') }}</p>
            </section>
          </template>

          <!-- Buildings Guide -->
          <template v-else-if="activeTopic === 'buildings-guide'">
            <h1 class="mb-4 text-2xl font-bold text-body">{{ t('docs.topicBuildingsGuide') }}</h1>

            <section class="doc-section">
              <h2>{{ t('docs.bg.overviewTitle') }}</h2>
              <p>{{ t('docs.bg.overviewBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.bg.typesTitle') }}</h2>
              <dl class="building-type-list">
                <div class="building-type-item">
                  <dt>{{ t('docs.bg.mine') }}</dt>
                  <dd>{{ t('docs.bg.mineDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.bg.factory') }}</dt>
                  <dd>{{ t('docs.bg.factoryDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.bg.salesShop') }}</dt>
                  <dd>{{ t('docs.bg.salesShopDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.bg.bank') }}</dt>
                  <dd>{{ t('docs.bg.bankDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.bg.powerPlant') }}</dt>
                  <dd>{{ t('docs.bg.powerPlantDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.bg.mediaHouse') }}</dt>
                  <dd>{{ t('docs.bg.mediaHouseDesc') }}</dd>
                </div>
              </dl>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.bg.unitsTitle') }}</h2>
              <p>{{ t('docs.bg.unitsBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.bg.upgradeTitle') }}</h2>
              <p>{{ t('docs.bg.upgradeBody') }}</p>
            </section>
          </template>

          <!-- Economy Overview -->
          <template v-else-if="activeTopic === 'economy-overview'">
            <h1 class="mb-4 text-2xl font-bold text-body">{{ t('docs.topicEconomyOverview') }}</h1>

            <section class="doc-section">
              <h2>{{ t('docs.eo.introTitle') }}</h2>
              <p>{{ t('docs.eo.introBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.eo.tickTitle') }}</h2>
              <p>{{ t('docs.eo.tickBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.eo.supplyTitle') }}</h2>
              <p>{{ t('docs.eo.supplyBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.eo.currencyTitle') }}</h2>
              <p>{{ t('docs.eo.currencyBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.eo.goldTitle') }}</h2>
              <p>{{ t('docs.eo.goldBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.eo.stocksTitle') }}</h2>
              <p>{{ t('docs.eo.stocksBody') }}</p>
            </section>
          </template>

          <!-- Email System -->
          <template v-else-if="activeTopic === 'email-system'">
            <h1 class="mb-4 text-2xl font-bold text-body">{{ t('docs.topicEmailSystem') }}</h1>

            <section class="doc-section">
              <h2>{{ t('docs.em.introTitle') }}</h2>
              <p>{{ t('docs.em.introBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.em.typesTitle') }}</h2>
              <dl class="building-type-list">
                <div class="building-type-item">
                  <dt>{{ t('docs.em.registration') }}</dt>
                  <dd>{{ t('docs.em.registrationDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.em.weekly') }}</dt>
                  <dd>{{ t('docs.em.weeklyDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.em.support') }}</dt>
                  <dd>{{ t('docs.em.supportDesc') }}</dd>
                </div>
                <div class="building-type-item">
                  <dt>{{ t('docs.em.deletion') }}</dt>
                  <dd>{{ t('docs.em.deletionDesc') }}</dd>
                </div>
              </dl>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.em.unsubscribeTitle') }}</h2>
              <p>{{ t('docs.em.unsubscribeBody') }}</p>
            </section>

            <section class="doc-section">
              <h2>{{ t('docs.em.privacyTitle') }}</h2>
              <p>{{ t('docs.em.privacyBody') }}</p>
            </section>
          </template>
        </article>
      </div>
    </main>
  </div>
</template>

<style scoped>
.doc-section {
  margin-top: 1.75rem;
}

.doc-section h2 {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--color-body);
  margin-bottom: 0.625rem;
}

.doc-section p {
  font-size: 0.9375rem;
  line-height: 1.7;
  color: var(--color-muted);
}

.doc-list {
  margin-top: 0.75rem;
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  font-size: 0.9375rem;
  color: var(--color-muted);
  line-height: 1.6;
}

.doc-ordered {
  list-style-type: decimal;
}

.doc-list:not(.doc-ordered) {
  list-style-type: disc;
}

.building-type-list {
  display: flex;
  flex-direction: column;
  gap: 0.875rem;
  margin-top: 0.75rem;
}

.building-type-item {
  border-left: 3px solid var(--color-accent);
  padding-left: 0.875rem;
}

.building-type-item dt {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--color-body);
}

.building-type-item dd {
  font-size: 0.875rem;
  color: var(--color-muted);
  margin-top: 0.2rem;
}
</style>
