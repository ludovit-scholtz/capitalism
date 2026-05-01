<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { fetchMySupportTickets, type SupportTicketInfo } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const tickets = ref<SupportTicketInfo[]>([])

const filterType = ref('')
const filterStatus = ref('')
const searchTitle = ref('')
const includeFinished = ref(false)
const sortBy = ref<'CREATED_AT' | 'UPDATED_AT' | 'TITLE'>('CREATED_AT')
const sortDirection = ref<'ASC' | 'DESC'>('DESC')

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

const visibleTickets = computed(() => {
  if (includeFinished.value) {
    return tickets.value
  }

  return tickets.value.filter((ticket) => ticket.status !== 'FINISHED')
})

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function statusLabel(status: string): string {
  return status === 'IN_PROGRESS'
    ? t('common.inProgress')
    : status === 'FINISHED'
      ? t('common.finished')
      : t('common.submitted')
}

function typeLabel(type: string): string {
  return type === 'SUGGESTION'
    ? t('common.suggestion')
    : type === 'BUG'
      ? t('common.bug')
      : t('common.other')
}

function openTicket(ticketId: string) {
  void router.push(`/support/tickets/${ticketId}`)
}

async function loadTickets() {
  if (!auth.token) return
  loading.value = true
  errorMessage.value = ''

  try {
    tickets.value = await fetchMySupportTickets(auth.token, {
      ticketType: filterType.value || null,
      status: filterStatus.value || null,
      searchTitle: searchTitle.value || null,
      sortBy: sortBy.value,
      sortDirection: sortDirection.value,
      limit: 100,
      offset: 0,
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

  if (route.query.created === '1') {
    successMessage.value = t('support.submitted')
  }

  await loadTickets()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.support')"
      :title="t('support.myTickets')"
      :subtitle="t('support.listOnlySubtitle')"
      variant="support"
    />
    <ViewSubnav :items="navItems" aria-label="Support navigation" />

    <section class="container pb-16 pt-4 lg:pb-20 lg:pt-6">
      <section class="card p-6" aria-label="My support tickets">
        <div class="filters mb-4 grid gap-3 md:grid-cols-3 xl:grid-cols-6">
          <input
            v-model="searchTitle"
            type="search"
            :placeholder="t('common.filterByTitle')"
            :aria-label="t('common.filterByTitle')"
            class="form-input"
          />
          <select v-model="filterType" :aria-label="t('common.filterType')" class="form-input">
            <option value="">{{ t('common.allTypes') }}</option>
            <option value="SUGGESTION">{{ t('common.suggestion') }}</option>
            <option value="BUG">{{ t('common.bug') }}</option>
            <option value="OTHER">{{ t('common.other') }}</option>
          </select>
          <select v-model="filterStatus" :aria-label="t('common.filterStatus')" class="form-input">
            <option value="">{{ t('common.allStatuses') }}</option>
            <option value="SUBMITTED">{{ t('common.submitted') }}</option>
            <option value="IN_PROGRESS">{{ t('common.inProgress') }}</option>
            <option value="FINISHED">{{ t('common.finished') }}</option>
          </select>
          <select v-model="sortBy" :aria-label="t('common.sortBy')" class="form-input">
            <option value="CREATED_AT">{{ t('common.createdDate') }}</option>
            <option value="UPDATED_AT">{{ t('common.updatedDate') }}</option>
            <option value="TITLE">{{ t('common.title') }}</option>
          </select>
          <select
            v-model="sortDirection"
            :aria-label="t('common.sortDirection')"
            class="form-input"
          >
            <option value="DESC">{{ t('common.newestFirst') }}</option>
            <option value="ASC">{{ t('common.oldestFirst') }}</option>
          </select>
          <button type="button" class="btn btn-secondary" @click="loadTickets">
            {{ t('common.apply') }}
          </button>
        </div>

        <label class="mb-4 flex items-center gap-2 text-sm text-muted">
          <input v-model="includeFinished" type="checkbox" />
          {{ t('support.showFinishedTickets') }}
        </label>

        <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
        <p v-if="loading" class="state-message">{{ t('support.loading') }}</p>

        <p v-else-if="visibleTickets.length === 0" class="state-message">
          {{ t('common.noData') }}
        </p>

        <div v-else class="grid gap-4">
          <table class="tickets-table" aria-label="My support tickets table">
            <thead>
              <tr>
                <th>{{ t('support.created') }}</th>
                <th>{{ t('common.title') }}</th>
                <th>{{ t('support.type') }}</th>
                <th>{{ t('support.status') }}</th>
                <th>{{ t('support.updated') }}</th>
                <th>{{ t('common.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="ticket in visibleTickets" :key="ticket.id">
                <td>{{ formatDate(ticket.createdAtUtc) }}</td>
                <td>
                  <button type="button" class="ticket-link" @click="openTicket(ticket.id)">
                    {{ ticket.title }}
                  </button>
                </td>
                <td>{{ typeLabel(ticket.ticketType) }}</td>
                <td>{{ statusLabel(ticket.status) }}</td>
                <td>{{ formatDate(ticket.updatedAtUtc) }}</td>
                <td>
                  <button type="button" class="btn btn-secondary" @click="openTicket(ticket.id)">
                    {{ t('support.openTicket') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>
  </main>
</template>

<style scoped>
.tickets-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.92rem;
}

.tickets-table th,
.tickets-table td {
  border-bottom: 1px solid var(--color-border);
  padding: 0.55rem;
  text-align: left;
}

.ticket-link {
  color: var(--color-primary);
  text-decoration: underline;
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 0;
  font: inherit;
}

.state-error {
  color: var(--color-danger);
}

.state-success {
  color: var(--color-success);
}

.state-message {
  color: var(--color-text-secondary);
}
</style>
