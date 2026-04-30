<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
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
  return status === 'IN_PROGRESS' ? 'In Progress' : status === 'FINISHED' ? 'Finished' : 'Submitted'
}

function typeLabel(type: string): string {
  return type === 'SUGGESTION' ? 'Suggestion' : type === 'BUG' ? 'Bug' : 'Other'
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
    errorMessage.value = error instanceof Error ? error.message : 'Failed to load support tickets.'
  } finally {
    loading.value = false
  }
}

async function submitTicket() {
  if (!auth.token) return
  errorMessage.value = ''
  successMessage.value = ''

  if (newTitle.value.trim().length < 5) {
    errorMessage.value = 'Title must be at least 5 characters long.'
    return
  }

  if (newMarkdown.value.trim().length < 20) {
    errorMessage.value = 'Ticket content must be at least 20 characters long.'
    return
  }

  createLoading.value = true
  try {
    const created = await createSupportTicket(auth.token, {
      ticketType: newType.value,
      title: newTitle.value,
      markdownSource: newMarkdown.value,
    })
    successMessage.value = 'Support ticket submitted.'
    newTitle.value = ''
    newMarkdown.value = ''
    await loadTickets()
    selectedTicketId.value = created.id
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to submit support ticket.'
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
    successMessage.value = 'Ticket updated. Moderation review is now pending.'
    const index = tickets.value.findIndex((ticket) => ticket.id === updated.id)
    if (index >= 0) {
      tickets.value[index] = updated
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to update support ticket.'
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
  <main class="support-shell">
    <header class="support-header">
      <h1>Support Tickets</h1>
      <p>Create suggestions, bug reports, or other support requests.</p>
      <a href="/" class="nav-link">← Back to portal</a>
    </header>

    <section class="support-card" aria-label="Create support ticket">
      <h2>Create ticket</h2>
      <div class="form-grid">
        <label>
          Type
          <select v-model="newType" aria-label="Ticket type">
            <option value="SUGGESTION">Suggestion</option>
            <option value="BUG">Bug</option>
            <option value="OTHER">Other</option>
          </select>
        </label>
        <label>
          Title
          <input v-model="newTitle" type="text" aria-label="Ticket title" />
        </label>
      </div>

      <TicketMarkdownEditor v-model="newMarkdown" />

      <div class="form-actions">
        <button type="button" :disabled="createLoading" @click="submitTicket">
          {{ createLoading ? 'Submitting…' : 'Submit ticket' }}
        </button>
      </div>
    </section>

    <section class="support-card" aria-label="My support tickets">
      <h2>My tickets</h2>
      <div class="filters">
        <input
          v-model="searchTitle"
          type="search"
          placeholder="Filter by title"
          aria-label="Filter by title"
        />
        <select v-model="filterType" aria-label="Filter type">
          <option value="">All types</option>
          <option value="SUGGESTION">Suggestion</option>
          <option value="BUG">Bug</option>
          <option value="OTHER">Other</option>
        </select>
        <select v-model="filterStatus" aria-label="Filter status">
          <option value="">All statuses</option>
          <option value="SUBMITTED">Submitted</option>
          <option value="IN_PROGRESS">In Progress</option>
          <option value="FINISHED">Finished</option>
        </select>
        <select v-model="sortBy" aria-label="Sort by">
          <option value="CREATED_AT">Created date</option>
          <option value="UPDATED_AT">Updated date</option>
          <option value="TITLE">Title</option>
        </select>
        <select v-model="sortDirection" aria-label="Sort direction">
          <option value="DESC">Newest first</option>
          <option value="ASC">Oldest first</option>
        </select>
        <button type="button" @click="loadTickets">Apply</button>
      </div>

      <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
      <p v-if="loading" class="state-message">Loading tickets…</p>

      <div v-else class="ticket-layout">
        <table class="tickets-table" aria-label="My support tickets table">
          <thead>
            <tr>
              <th>Created</th>
              <th>Title</th>
              <th>Type</th>
              <th>Status</th>
              <th>Updated</th>
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
            Moderation: {{ selectedTicket.moderationState }}
            <span v-if="selectedTicket.moderationReason">
              · {{ selectedTicket.moderationReason }}</span
            >
          </p>

          <label>
            Title
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
              Save edits
            </button>
          </div>

          <section class="preview-panel">
            <h4>Moderated preview</h4>
            <div
              v-if="selectedTicket.sanitizedPreviewHtml"
              class="preview-html"
              v-html="selectedTicket.sanitizedPreviewHtml"
            ></div>
            <p v-else class="state-message">
              Preview is hidden until an administrator approves moderation.
            </p>
          </section>

          <section class="activity-panel">
            <h4>Activity log</h4>
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
  </main>
</template>

<style scoped>
.support-shell {
  max-width: 1280px;
  margin: 0 auto;
  padding: 2rem 1rem 5rem;
  color: #ececff;
}

.support-header h1 {
  margin: 0;
}

.nav-link {
  color: #a7b6ff;
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
