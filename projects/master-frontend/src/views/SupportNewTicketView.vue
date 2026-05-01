<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import TicketMarkdownEditor from '@/components/support/TicketMarkdownEditor.vue'
import { createSupportTicket } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const newType = ref<'SUGGESTION' | 'BUG' | 'OTHER'>('SUGGESTION')
const newTitle = ref('')
const newMarkdown = ref('')
const createLoading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

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
    await router.push(`/support/tickets?ticket=${created.id}&created=1`)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('support.submitError')
  } finally {
    createLoading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.push('/login')
  }
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.support')"
      :title="t('support.createSection')"
      :subtitle="t('support.createSubtitle')"
      variant="support"
    />
    <ViewSubnav :items="navItems" aria-label="Support navigation" />

    <section class="container pb-16 pt-4 lg:pb-20 lg:pt-6">
      <section class="card p-6" aria-label="Create support ticket">
        <div class="grid gap-4 md:grid-cols-2">
          <label class="grid gap-1.5">
            <span class="text-sm text-muted">{{ t('support.ticketType') }}</span>
            <select v-model="newType" aria-label="Ticket type" class="form-input">
              <option value="SUGGESTION">{{ t('common.suggestion') }}</option>
              <option value="BUG">{{ t('common.bug') }}</option>
              <option value="OTHER">{{ t('common.other') }}</option>
            </select>
          </label>
          <label class="grid gap-1.5">
            <span class="text-sm text-muted">{{ t('support.ticketTitle') }}</span>
            <input v-model="newTitle" type="text" aria-label="Ticket title" class="form-input" />
          </label>
        </div>

        <div class="mt-4">
          <TicketMarkdownEditor v-model="newMarkdown" />
        </div>

        <p v-if="errorMessage" class="state-error mt-4" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="state-success mt-4" role="status">{{ successMessage }}</p>

        <div class="mt-5 flex justify-end">
          <button
            type="button"
            class="btn btn-primary"
            :disabled="createLoading"
            @click="submitTicket"
          >
            {{ createLoading ? t('support.submitting') : t('support.submit') }}
          </button>
        </div>
      </section>
    </section>
  </main>
</template>
