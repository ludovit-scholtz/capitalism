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
const saving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const ticket = ref<SupportTicketInfo | null>(null)

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

const canEditTicket = computed(() => ticket.value !== null && ticket.value.status !== 'FINISHED')

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

async function loadTicket() {
  if (!auth.token) return
  const ticketId = String(route.params.ticketId ?? '')

  loading.value = true
  errorMessage.value = ''

  try {
    const results = await fetchMySupportTickets(auth.token, {
      limit: 200,
      offset: 0,
      sortBy: 'UPDATED_AT',
      sortDirection: 'DESC',
    })

    ticket.value = results.find((item) => item.id === ticketId) ?? null
    if (!ticket.value) {
      errorMessage.value = t('support.ticketNotFound')
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.loadError')
  } finally {
    loading.value = false
  }
}

async function saveTicketEdits() {
  if (!auth.token || !ticket.value) return

  saving.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    const updated = await updateSupportTicketContent(auth.token, {
      ticketId: ticket.value.id,
      title: ticket.value.title,
      markdownSource: ticket.value.markdownSource,
    })

    ticket.value = updated
    successMessage.value = t('support.updateSuccess')
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.updateError')
  } finally {
    saving.value = false
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

  await loadTicket()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.support')"
      :title="t('support.ticketDetailTitle')"
      :subtitle="t('support.ticketDetailSubtitle')"
      variant="support"
    />
    <ViewSubnav :items="navItems" aria-label="Support navigation" />

    <section class="container pb-16 pt-4 lg:pb-20 lg:pt-6">
      <section class="card p-6" aria-label="Support ticket detail">
        <div class="mb-4 flex items-center justify-between gap-3">
          <button type="button" class="btn btn-secondary" @click="router.push('/support/tickets')">
            {{ t('support.backToTickets') }}
          </button>
        </div>

        <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
        <p v-if="loading" class="state-message">{{ t('support.loading') }}</p>

        <template v-else-if="ticket">
          <h2 class="text-2xl font-semibold text-body">{{ ticket.title }}</h2>
          <p class="ticket-meta mt-2">
            {{ typeLabel(ticket.ticketType) }} · {{ statusLabel(ticket.status) }}
          </p>
          <p class="ticket-meta">
            {{ t('support.moderation') }}: {{ ticket.moderationState }}
            <span v-if="ticket.moderationReason"> · {{ ticket.moderationReason }}</span>
          </p>

          <label class="mt-4 grid gap-1.5">
            <span class="text-sm text-muted">{{ t('common.title') }}</span>
            <input
              v-model="ticket.title"
              type="text"
              :disabled="!canEditTicket"
              aria-label="Edit ticket title"
              class="form-input"
            />
          </label>

          <div class="mt-4">
            <TicketMarkdownEditor v-model="ticket.markdownSource" :disabled="!canEditTicket" />
          </div>

          <div class="mt-4 flex justify-end">
            <button
              type="button"
              class="btn btn-primary"
              :disabled="!canEditTicket || saving"
              @click="saveTicketEdits"
            >
              {{ saving ? t('support.savingEdits') : t('support.saveEdits') }}
            </button>
          </div>

          <section class="mt-6 border-t border-divider pt-4">
            <h3 class="text-lg font-semibold text-body">{{ t('support.moderatedPreview') }}</h3>
            <div
              v-if="ticket.sanitizedPreviewHtml"
              class="preview-html mt-3"
              v-html="ticket.sanitizedPreviewHtml"
            ></div>
            <p v-else class="state-message mt-2">{{ t('support.previewHidden') }}</p>
          </section>

          <section class="mt-6 border-t border-divider pt-4">
            <h3 class="text-lg font-semibold text-body">{{ t('support.activityLog') }}</h3>
            <ul class="mt-3 grid gap-2">
              <li v-for="eventItem in ticket.activity" :key="eventItem.id" class="activity-item">
                <strong>{{ eventItem.eventType }}</strong> · {{ eventItem.actorEmail }} ·
                {{ formatDate(eventItem.createdAtUtc) }}
                <div>{{ eventItem.note }}</div>
              </li>
            </ul>
          </section>
        </template>
      </section>
    </section>
  </main>
</template>

<style scoped>
.ticket-meta {
  color: var(--color-text-secondary);
}

.preview-html :deep(a) {
  color: var(--color-primary);
}

.activity-item {
  border: 1px solid var(--color-border);
  border-radius: 0.65rem;
  padding: 0.6rem;
  background: color-mix(in srgb, var(--color-surface) 90%, #000 10%);
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
