<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { fetchMySupportTickets, type SupportTicketInfo } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const tickets = ref<SupportTicketInfo[]>([])

const navItems = computed(() => {
  const items = [
    { label: t('support.dashboardNav'), to: '/support' },
    { label: t('support.createSection'), to: '/support/new' },
    { label: t('support.myTickets'), to: '/support/tickets' },
  ]

  if (auth.isGameAdmin) {
    items.push({ label: t('home.supportAdmin'), to: '/support/admin' })
  }

  return items
})

const activeTicketsCount = computed(
  () => tickets.value.filter((ticket) => ticket.status !== 'FINISHED').length,
)

const finishedTicketsCount = computed(
  () => tickets.value.filter((ticket) => ticket.status === 'FINISHED').length,
)

async function loadTickets() {
  if (!auth.token) return
  loading.value = true
  errorMessage.value = ''

  try {
    tickets.value = await fetchMySupportTickets(auth.token, {
      limit: 100,
      offset: 0,
      sortBy: 'UPDATED_AT',
      sortDirection: 'DESC',
    })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.loadError')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }

  await loadTickets()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.support')"
      :title="t('support.title')"
      :subtitle="t('support.subtitle')"
      variant="support"
    />
    <ViewSubnav :items="navItems" aria-label="Support navigation" />

    <section class="container pb-16 pt-4 lg:pb-20 lg:pt-6">
      <div class="grid gap-4 md:grid-cols-3" aria-label="Support dashboard summary">
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('support.activeTickets') }}</p>
          <strong class="mt-2 block text-3xl">{{ activeTicketsCount }}</strong>
        </article>
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('support.finishedTickets') }}</p>
          <strong class="mt-2 block text-3xl">{{ finishedTicketsCount }}</strong>
        </article>
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('support.totalTickets') }}</p>
          <strong class="mt-2 block text-3xl">{{ tickets.length }}</strong>
        </article>
      </div>

      <p v-if="errorMessage" class="state-error mt-4" role="alert">{{ errorMessage }}</p>
      <p v-if="loading" class="state-message mt-4">{{ t('support.loading') }}</p>

      <section class="mt-5 grid gap-4 md:grid-cols-2" aria-label="Support dashboard actions">
        <RouterLink class="card block p-6 no-underline" to="/support/new">
          <h2 class="text-xl font-semibold text-body">{{ t('support.createSection') }}</h2>
          <p class="mt-2 text-sm text-muted">{{ t('support.createTeaser') }}</p>
        </RouterLink>
        <RouterLink class="card block p-6 no-underline" to="/support/tickets">
          <h2 class="text-xl font-semibold text-body">{{ t('support.myTickets') }}</h2>
          <p class="mt-2 text-sm text-muted">{{ t('support.listTeaser') }}</p>
        </RouterLink>
      </section>
    </section>
  </main>
</template>
