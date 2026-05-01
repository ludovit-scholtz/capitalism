<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import TicketMarkdownEditor from '@/components/support/TicketMarkdownEditor.vue'
import {
  fetchMySupportTickets,
  updateSupportTicketContent,
  type SupportTicketInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const tickets = ref<SupportTicketInfo[]>([])
const selectedTicketId = ref<string | null>(null)

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

const selectedTicket = computed(
  () => visibleTickets.value.find((ticket) => ticket.id === selectedTicketId.value) ?? null,
)

const canEditSelected = computed(
  () => selectedTicket.value !== null && selectedTicket.value.status !== 'FINISHED',
)

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

function ensureSelectedTicket() {
  if (!selectedTicketId.value && visibleTickets.value.length > 0) {
    selectedTicketId.value = visibleTickets.value[0]?.id ?? null
  }

  if (
    selectedTicketId.value &&
    visibleTickets.value.every((ticket) => ticket.id !== selectedTicketId.value)
  ) {
    selectedTicketId.value = visibleTickets.value[0]?.id ?? null
  }
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

    if (typeof route.query.ticket === 'string') {
      selectedTicketId.value = route.query.ticket
    }

    ensureSelectedTicket()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.loadError')
  } finally {
    loading.value = false
  }
}

async function saveTicketEdits() {
  if (!auth.token || !selectedTicket.value) return

  errorMessage.value = ''
  successMessage.value = ''

  try {
    const updated = await updateSupportTicketContent(auth.token, {
      ticketId: selectedTicket.value.id,
      title: selectedTicket.value.title,
      markdownSource: selectedTicket.value.markdownSource,
    })

    successMessage.value = t('support.updateSuccess')
    const index = tickets.value.findIndex((ticket) => ticket.id === updated.id)
    if (index >= 0) {
      tickets.value[index] = updated
    }
    ensureSelectedTicket()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.updateError')
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
      :subtitle="t('support.listSubtitle')"
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

        <div v-else class="grid gap-4 lg:grid-cols-[1.25fr_1fr]">
          <table class="tickets-table" aria-label="My support tickets table">
            <thead>
              <tr>
                <th>{{ t('support.created') }}</th>
                <th>{{ t('common.title') }}</th>
                <th>{{ t('support.type') }}</th>
                <th>{{ t('support.status') }}</th>
                <th>{{ t('support.updated') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="ticket in visibleTickets"
                :key="ticket.id"
                :class="{ selected: ticket.id === selectedTicketId }"
                @click="selectedTicketId = ticket.id"
              >
                <td>{{ formatDate(ticket.createdAtUtc) }}</td>
                <td>{{ ticket.title }}</td>
                <td>{{ typeLabel(ticket.ticketType) }}</td>
                <td>{{ statusLabel(ticket.status) }}</td>
                <td>{{ formatDate(ticket.updatedAtUtc) }}</td>
              </tr>
            </tbody>
          </table>

          <article v-if="selectedTicket" class="ticket-detail" aria-label="Selected ticket detail">
            <h3>{{ selectedTicket.title }}</h3>
            <p class="ticket-meta">
              {{ typeLabel(selectedTicket.ticketType) }} · {{ statusLabel(selectedTicket.status) }}
            </p>
            <p class="ticket-meta">
              {{ t('support.moderation') }}: {{ selectedTicket.moderationState }}
              <span v-if="selectedTicket.moderationReason">
                · {{ selectedTicket.moderationReason }}</span
              >
            </p>

            <label class="mt-3 grid gap-1.5">
              <span class="text-sm text-muted">{{ t('common.title') }}</span>
              <input
                v-model="selectedTicket.title"
                type="text"
                :disabled="!canEditSelected"
                aria-label="Edit ticket title"
                class="form-input"
              />
            </label>

            <div class="mt-3">
              <TicketMarkdownEditor
                v-model="selectedTicket.markdownSource"
                :disabled="!canEditSelected"
              />
            </div>

            <div class="mt-3 flex justify-end">
              <button
                type="button"
                class="btn btn-primary"
                :disabled="!canEditSelected"
                @click="saveTicketEdits"
              >
                {{ t('support.saveEdits') }}
              </button>
            </div>

            <section class="mt-4 border-t border-divider pt-3">
              <h4>{{ t('support.moderatedPreview') }}</h4>
              <div
                v-if="selectedTicket.sanitizedPreviewHtml"
                class="preview-html mt-2"
                v-html="selectedTicket.sanitizedPreviewHtml"
              ></div>
              <p v-else class="state-message mt-2">{{ t('support.previewHidden') }}</p>
            </section>

            <section class="mt-4 border-t border-divider pt-3">
              <h4>{{ t('support.activityLog') }}</h4>
              <ul class="mt-2 grid gap-2">
                <li v-for="eventItem in selectedTicket.activity" :key="eventItem.id">
                  <strong>{{ eventItem.eventType }}</strong> · {{ eventItem.actorEmail }} ·
                  {{ formatDate(eventItem.createdAtUtc) }}
                  <div>{{ eventItem.note }}</div>
                </li>
              </ul>
            </section>
          </article>
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

.tickets-table tr {
  cursor: pointer;
}

.tickets-table tr.selected {
  background: color-mix(in srgb, var(--color-primary-light) 22%, transparent);
}

.ticket-detail {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 0.85rem;
  background: color-mix(in srgb, var(--color-surface) 86%, #000 14%);
}

.ticket-meta {
  margin: 0.2rem 0;
  color: var(--color-text-secondary);
}

.preview-html :deep(a) {
  color: var(--color-primary);
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
