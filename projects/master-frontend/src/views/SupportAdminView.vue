<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  fetchSupportTicketsAdmin,
  moderateSupportTicket,
  updateSupportTicketStatus,
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
const unsafeOnly = ref(false)

const statusNote = ref('')
const moderationNote = ref('')

const selectedTicket = computed(
  () => tickets.value.find((ticket) => ticket.id === selectedTicketId.value) ?? null,
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

async function loadTickets() {
  if (!auth.token) return
  loading.value = true
  errorMessage.value = ''
  try {
    tickets.value = await fetchSupportTicketsAdmin(auth.token, {
      ticketType: filterType.value || null,
      status: filterStatus.value || null,
      searchTitle: searchTitle.value || null,
      sortBy: sortBy.value,
      sortDirection: sortDirection.value,
      unsafeOnly: unsafeOnly.value,
      limit: 200,
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
    errorMessage.value =
      error instanceof Error ? error.message : 'Failed to load admin support tickets.'
  } finally {
    loading.value = false
  }
}

async function updateStatus(status: 'SUBMITTED' | 'IN_PROGRESS' | 'FINISHED') {
  if (!auth.token || !selectedTicket.value) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    const updated = await updateSupportTicketStatus(auth.token, {
      ticketId: selectedTicket.value.id,
      status,
      note: statusNote.value.trim() || undefined,
    })
    replaceTicket(updated)
    successMessage.value = `Status changed to ${status}.`
    statusNote.value = ''
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to update status.'
  }
}

async function moderate(approve: boolean) {
  if (!auth.token || !selectedTicket.value) return
  errorMessage.value = ''
  successMessage.value = ''

  try {
    const updated = await moderateSupportTicket(auth.token, {
      ticketId: selectedTicket.value.id,
      approve,
      note: moderationNote.value.trim() || undefined,
    })
    replaceTicket(updated)
    successMessage.value = approve ? 'Ticket moderation approved.' : 'Ticket moderation rejected.'
    moderationNote.value = ''
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to moderate ticket.'
  }
}

function replaceTicket(ticket: SupportTicketInfo) {
  const index = tickets.value.findIndex((item) => item.id === ticket.id)
  if (index >= 0) {
    tickets.value[index] = ticket
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
  <main class="support-admin-shell">
    <header class="support-admin-header">
      <h1>Support Admin</h1>
      <p>Moderate markdown, review attachments, and manage support ticket lifecycle.</p>
      <a href="/" class="nav-link">← Back to portal</a>
    </header>

    <section class="panel" aria-label="Support admin filters">
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
        <label class="unsafe-only-filter">
          <input v-model="unsafeOnly" type="checkbox" />
          Unsafe only
        </label>
        <button type="button" @click="loadTickets">Apply</button>
      </div>

      <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
    </section>

    <section class="panel" aria-label="Support admin ticket list">
      <p v-if="loading" class="state-message">Loading tickets…</p>
      <div v-else class="layout">
        <table class="tickets-table" aria-label="Admin support tickets table">
          <thead>
            <tr>
              <th>Created</th>
              <th>Title</th>
              <th>Type</th>
              <th>Status</th>
              <th>Moderation</th>
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
              <td>{{ ticket.ticketType }}</td>
              <td>{{ ticket.status }}</td>
              <td>{{ ticket.moderationState }}</td>
              <td>{{ formatDate(ticket.updatedAtUtc) }}</td>
            </tr>
          </tbody>
        </table>

        <article
          v-if="selectedTicket"
          class="ticket-detail"
          aria-label="Admin selected ticket detail"
        >
          <h2>{{ selectedTicket.title }}</h2>
          <p class="detail-meta">
            {{ selectedTicket.ticketType }} · {{ selectedTicket.status }} · Moderation:
            {{ selectedTicket.moderationState }}
          </p>
          <p class="detail-meta">
            Created by {{ selectedTicket.createdByDisplayName }} ({{
              selectedTicket.createdByEmail
            }})
          </p>

          <section class="admin-actions" aria-label="Admin moderation actions">
            <h3>Lifecycle</h3>
            <input
              v-model="statusNote"
              type="text"
              placeholder="Status note"
              aria-label="Status note"
            />
            <div class="action-row">
              <button type="button" @click="updateStatus('SUBMITTED')">Set Submitted</button>
              <button type="button" @click="updateStatus('IN_PROGRESS')">Set In Progress</button>
              <button type="button" @click="updateStatus('FINISHED')">Set Finished</button>
            </div>

            <h3>Moderation</h3>
            <textarea
              v-model="moderationNote"
              rows="3"
              placeholder="Moderation note"
              aria-label="Moderation note"
            ></textarea>
            <div class="action-row">
              <button type="button" @click="moderate(true)">Approve preview</button>
              <button type="button" @click="moderate(false)">Reject preview</button>
            </div>
          </section>

          <section class="raw-panel">
            <h3>Raw markdown</h3>
            <pre>{{ selectedTicket.markdownSource }}</pre>
          </section>

          <section class="raw-panel" aria-label="Extracted links and images">
            <h3>Extracted links</h3>
            <ul>
              <li v-for="url in selectedTicket.extractedUrls" :key="url">{{ url }}</li>
            </ul>
            <h3>Extracted images</h3>
            <ul>
              <li v-for="url in selectedTicket.extractedImages" :key="url">{{ url }}</li>
            </ul>
          </section>

          <section class="preview-panel">
            <h3>Sanitized preview</h3>
            <div
              v-if="selectedTicket.sanitizedPreviewHtml"
              class="preview-html"
              v-html="selectedTicket.sanitizedPreviewHtml"
            ></div>
            <p v-else class="state-message">Preview is currently moderation-gated.</p>
          </section>

          <section class="activity-panel">
            <h3>Audit activity</h3>
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
.support-admin-shell {
  max-width: 1400px;
  margin: 0 auto;
  padding: 2rem 1rem 4rem;
  color: #e8ecff;
}

.nav-link {
  color: #adbcff;
}

.panel {
  margin-top: 1rem;
  background: #13192e;
  border: 1px solid #313b63;
  border-radius: 12px;
  padding: 1rem;
}

.filters {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.6rem;
}

.layout {
  display: grid;
  grid-template-columns: 1.3fr 1fr;
  gap: 1rem;
}

input,
select,
textarea,
button {
  width: 100%;
  border: 1px solid #3a4777;
  border-radius: 8px;
  background: #0f152a;
  color: #eef2ff;
  padding: 0.55rem 0.7rem;
}

button {
  cursor: pointer;
}

.tickets-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.tickets-table th,
.tickets-table td {
  border-bottom: 1px solid #2e395e;
  padding: 0.45rem;
  text-align: left;
}

.tickets-table tr {
  cursor: pointer;
}

.tickets-table tr.selected {
  background: #223669;
}

.ticket-detail {
  border: 1px solid #2e395e;
  border-radius: 10px;
  padding: 0.8rem;
  background: #0d1327;
}

.detail-meta {
  color: #b8c5f5;
  margin: 0.15rem 0;
}

.action-row {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.5rem;
  margin-top: 0.5rem;
}

.raw-panel,
.preview-panel,
.activity-panel,
.admin-actions {
  margin-top: 0.9rem;
  border-top: 1px solid #2e395e;
  padding-top: 0.7rem;
}

pre {
  white-space: pre-wrap;
  background: #090f20;
  border: 1px solid #24345f;
  border-radius: 8px;
  padding: 0.7rem;
}

.state-error {
  color: #ff8e8e;
}

.state-success {
  color: #7df0ba;
}

.state-message {
  color: #ced6ff;
}

@media (max-width: 1060px) {
  .layout {
    grid-template-columns: 1fr;
  }

  .action-row {
    grid-template-columns: 1fr;
  }
}
</style>
