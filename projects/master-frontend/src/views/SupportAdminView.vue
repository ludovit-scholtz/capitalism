<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
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
    errorMessage.value = error instanceof Error ? error.message : t('supportAdmin.loadError')
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
    successMessage.value = t('supportAdmin.statusChanged', { status })
    statusNote.value = ''
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t('supportAdmin.updateStatusError')
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
    successMessage.value = approve ? t('supportAdmin.approved') : t('supportAdmin.rejected')
    moderationNote.value = ''
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('supportAdmin.moderateError')
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
      <h1>{{ t('supportAdmin.title') }}</h1>
      <p>{{ t('supportAdmin.subtitle') }}</p>
      <a href="/" class="nav-link">← {{ t('common.backToPortal') }}</a>
    </header>

    <section class="panel" :aria-label="t('supportAdmin.filtersAria')">
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
        <label class="unsafe-only-filter">
          <input v-model="unsafeOnly" type="checkbox" />
          {{ t('supportAdmin.unsafeOnly') }}
        </label>
        <button type="button" @click="loadTickets">{{ t('common.apply') }}</button>
      </div>

      <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
    </section>

    <section class="panel" :aria-label="t('supportAdmin.listAria')">
      <p v-if="loading" class="state-message">{{ t('supportAdmin.loading') }}</p>
      <div v-else class="layout">
        <table class="tickets-table" :aria-label="t('supportAdmin.adminTableAria')">
          <thead>
            <tr>
              <th>{{ t('support.created') }}</th>
              <th>{{ t('common.title') }}</th>
              <th>{{ t('support.type') }}</th>
              <th>{{ t('support.status') }}</th>
              <th>{{ t('supportAdmin.moderationState') }}</th>
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
          :aria-label="t('supportAdmin.detailAria')"
        >
          <h2>{{ selectedTicket.title }}</h2>
          <p class="detail-meta">
            {{ selectedTicket.ticketType }} · {{ selectedTicket.status }} ·
            {{ t('supportAdmin.moderationState') }}: {{ selectedTicket.moderationState }}
          </p>
          <p class="detail-meta">
            {{
              t('supportAdmin.createdBy', {
                name: selectedTicket.createdByDisplayName,
                email: selectedTicket.createdByEmail,
              })
            }}
          </p>

          <section class="admin-actions" :aria-label="t('supportAdmin.moderation')">
            <h3>{{ t('supportAdmin.lifecycle') }}</h3>
            <input
              v-model="statusNote"
              type="text"
              :placeholder="t('supportAdmin.statusNote')"
              :aria-label="t('supportAdmin.statusNote')"
            />
            <div class="action-row">
              <button type="button" @click="updateStatus('SUBMITTED')">
                {{ t('supportAdmin.setSubmitted') }}
              </button>
              <button type="button" @click="updateStatus('IN_PROGRESS')">
                {{ t('supportAdmin.setInProgress') }}
              </button>
              <button type="button" @click="updateStatus('FINISHED')">
                {{ t('supportAdmin.setFinished') }}
              </button>
            </div>

            <h3>{{ t('supportAdmin.moderation') }}</h3>
            <textarea
              v-model="moderationNote"
              rows="3"
              :placeholder="t('supportAdmin.moderationNote')"
              :aria-label="t('supportAdmin.moderationNote')"
            ></textarea>
            <div class="action-row">
              <button type="button" @click="moderate(true)">
                {{ t('supportAdmin.approvePreview') }}
              </button>
              <button type="button" @click="moderate(false)">
                {{ t('supportAdmin.rejectPreview') }}
              </button>
            </div>
          </section>

          <section class="raw-panel">
            <h3>{{ t('supportAdmin.rawMarkdown') }}</h3>
            <pre>{{ selectedTicket.markdownSource }}</pre>
          </section>

          <section class="raw-panel" aria-label="Extracted links and images">
            <h3>{{ t('supportAdmin.extractedLinks') }}</h3>
            <ul>
              <li v-for="url in selectedTicket.extractedUrls" :key="url">{{ url }}</li>
            </ul>
            <h3>{{ t('supportAdmin.extractedImages') }}</h3>
            <ul>
              <li v-for="url in selectedTicket.extractedImages" :key="url">{{ url }}</li>
            </ul>
          </section>

          <section class="preview-panel">
            <h3>{{ t('supportAdmin.sanitizedPreview') }}</h3>
            <div
              v-if="selectedTicket.sanitizedPreviewHtml"
              class="preview-html"
              v-html="selectedTicket.sanitizedPreviewHtml"
            ></div>
            <p v-else class="state-message">{{ t('supportAdmin.moderationGated') }}</p>
          </section>

          <section class="activity-panel">
            <h3>{{ t('supportAdmin.auditActivity') }}</h3>
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
