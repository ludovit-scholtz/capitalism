<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import TicketMarkdownEditor from '@/components/support/TicketMarkdownEditor.vue'
import {
  createSupportTicket,
  fetchMySupportTickets,
  updateSupportTicketContent,
  type SupportTicketInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const tickets = ref<SupportTicketInfo[]>([])
const selectedTicketId = ref<string | null>(null)

const filterType = ref('')
const filterStatus = ref('')
const searchTitle = ref('')
const sortBy = ref<'CREATED_AT' | 'UPDATED_AT' | 'TITLE'>('CREATED_AT')
const sortDirection = ref<'ASC' | 'DESC'>('DESC')

const newType = ref<'SUGGESTION' | 'BUG' | 'OTHER'>('SUGGESTION')
const newTitle = ref('')
const newMarkdown = ref('')
const createLoading = ref(false)

const selectedTicket = computed(
  () => tickets.value.find((ticket) => ticket.id === selectedTicketId.value) ?? null,
)

const navItems = computed(() => {
  const items = [
    { label: t('home.referralDashboard'), to: '/referrals/dashboard' },
    { label: t('common.backToPortal'), to: '/' },
  ]

  if (auth.isGameAdmin) {
    items.unshift({ label: t('home.supportAdmin'), to: '/support/admin' })
  }

  return items
})

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

    if (!selectedTicketId.value && tickets.value.length > 0) {
      selectedTicketId.value = tickets.value[0]?.id ?? null
    }

    if (
      selectedTicketId.value &&
      tickets.value.every((ticket) => ticket.id !== selectedTicketId.value)
    ) {
      selectedTicketId.value = tickets.value[0]?.id ?? null
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.loadError')
  } finally {
    loading.value = false
  }
}

async function submitTicket() {
  if (!auth.token) return
  errorMessage.value = ''
  successMessage.value = ''

  if (newTitle.value.trim().length < 5) {
    errorMessage.value = t('support.titleValidation')
    return
  }

  if (newMarkdown.value.trim().length < 20) {
    errorMessage.value = t('support.contentValidation')
    return
  }

  createLoading.value = true
  try {
    const created = await createSupportTicket(auth.token, {
      ticketType: newType.value,
      title: newTitle.value,
      markdownSource: newMarkdown.value,
    })
    successMessage.value = t('support.submitted')
    newTitle.value = ''
    newMarkdown.value = ''
    await loadTickets()
    selectedTicketId.value = created.id
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.submitError')
  } finally {
    createLoading.value = false
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
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.updateError')
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
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

    <section class="support-shell">
      <section class="support-card" aria-label="Create support ticket">
        <h2>{{ t('support.createSection') }}</h2>
        <div class="form-grid">
          <label>
            {{ t('support.ticketType') }}
            <select v-model="newType" aria-label="Ticket type">
              <option value="SUGGESTION">{{ t('common.suggestion') }}</option>
              <option value="BUG">{{ t('common.bug') }}</option>
              <option value="OTHER">{{ t('common.other') }}</option>
            </select>
          </label>
          <label>
            {{ t('support.ticketTitle') }}
            <input v-model="newTitle" type="text" aria-label="Ticket title" />
          </label>
        </div>

        <TicketMarkdownEditor v-model="newMarkdown" />

        <div class="form-actions">
          <button type="button" :disabled="createLoading" @click="submitTicket">
            {{ createLoading ? t('support.submitting') : t('support.submit') }}
          </button>
        </div>
      </section>

      <section class="support-card" aria-label="My support tickets">
        <h2>{{ t('support.myTickets') }}</h2>
        <div class="filters">
          <input
            v-model="searchTitle"
            type="search"
            :placeholder="t('common.filterByTitle')"
            :aria-label="t('common.filterByTitle')"
          />
          <select v-model="filterType" :aria-label="t('common.filterType')">
            <option value="">{{ t('common.allTypes') }}</option>
            <option value="SUGGESTION">{{ t('common.suggestion') }}</option>
            <option value="BUG">{{ t('common.bug') }}</option>
            <option value="OTHER">{{ t('common.other') }}</option>
          </select>
          <select v-model="filterStatus" :aria-label="t('common.filterStatus')">
            <option value="">{{ t('common.allStatuses') }}</option>
            <option value="SUBMITTED">{{ t('common.submitted') }}</option>
            <option value="IN_PROGRESS">{{ t('common.inProgress') }}</option>
            <option value="FINISHED">{{ t('common.finished') }}</option>
          </select>
          <select v-model="sortBy" :aria-label="t('common.sortBy')">
            <option value="CREATED_AT">{{ t('common.createdDate') }}</option>
            <option value="UPDATED_AT">{{ t('common.updatedDate') }}</option>
            <option value="TITLE">{{ t('common.title') }}</option>
          </select>
          <select v-model="sortDirection" :aria-label="t('common.sortDirection')">
            <option value="DESC">{{ t('common.newestFirst') }}</option>
            <option value="ASC">{{ t('common.oldestFirst') }}</option>
          </select>
          <button type="button" @click="loadTickets">{{ t('common.apply') }}</button>
        </div>

        <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
        <p v-if="loading" class="state-message">{{ t('support.loading') }}</p>

        <p v-else-if="tickets.length === 0" class="state-message">{{ t('common.noData') }}</p>

        <div v-else class="ticket-layout">
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
                v-for="ticket in tickets"
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

            <label>
              {{ t('common.title') }}
              <input
                v-model="selectedTicket.title"
                type="text"
                :disabled="!canEditSelected"
                aria-label="Edit ticket title"
              />
            </label>
            <TicketMarkdownEditor
              v-model="selectedTicket.markdownSource"
              :disabled="!canEditSelected"
            />

            <div class="form-actions">
              <button type="button" :disabled="!canEditSelected" @click="saveTicketEdits">
                {{ t('support.saveEdits') }}
              </button>
            </div>

            <section class="preview-panel">
              <h4>{{ t('support.moderatedPreview') }}</h4>
              <div
                v-if="selectedTicket.sanitizedPreviewHtml"
                class="preview-html"
                v-html="selectedTicket.sanitizedPreviewHtml"
              ></div>
              <p v-else class="state-message">
                {{ t('support.previewHidden') }}
              </p>
            </section>

            <section class="activity-panel">
              <h4>{{ t('support.activityLog') }}</h4>
              <ul>
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
.support-shell {
  max-width: 1280px;
  margin: 0 auto;
  padding: 2rem 1rem 5rem;
  color: #ececff;
}

.support-card {
  margin-top: 1.5rem;
  padding: 1rem;
  border-radius: 12px;
  border: 1px solid #323650;
  background: #161a2b;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 0.8rem;
  margin-bottom: 0.8rem;
}

label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

input,
select,
button {
  padding: 0.55rem 0.7rem;
  border-radius: 8px;
  border: 1px solid #3a4062;
  background: #0d1122;
  color: #ececff;
}

button {
  cursor: pointer;
}

.form-actions {
  margin-top: 0.8rem;
  display: flex;
  justify-content: flex-end;
}

.filters {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.6rem;
  margin-bottom: 1rem;
}

.ticket-layout {
  display: grid;
  grid-template-columns: 1.2fr 1fr;
  gap: 1rem;
}

.tickets-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.92rem;
}

.tickets-table th,
.tickets-table td {
  border-bottom: 1px solid #2f3453;
  padding: 0.45rem;
  text-align: left;
}

.tickets-table tr {
  cursor: pointer;
}

.tickets-table tr.selected {
  background: #27325f;
}

.ticket-detail {
  border: 1px solid #2f3453;
  border-radius: 10px;
  padding: 0.75rem;
  background: #101426;
}

.ticket-meta {
  margin: 0.2rem 0;
  color: #b8c0ea;
}

.preview-panel,
.activity-panel {
  margin-top: 0.8rem;
  border-top: 1px solid #2f3453;
  padding-top: 0.7rem;
}

.preview-html :deep(a) {
  color: #9eb3ff;
}

.state-error {
  color: #ff8686;
}

.state-success {
  color: #7ef0b4;
}

.state-message {
  color: #c8d0f5;
}

@media (max-width: 960px) {
  .ticket-layout {
    grid-template-columns: 1fr;
  }
}
</style>
