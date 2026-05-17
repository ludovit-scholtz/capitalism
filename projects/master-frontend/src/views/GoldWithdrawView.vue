<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import {
  createGoldWithdrawalRequest,
  fetchMyGoldWithdrawalRequests,
  type GoldWithdrawalRequestInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const requests = ref<GoldWithdrawalRequestInfo[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')
const network = ref<'VOI' | 'ALGORAND'>('ALGORAND')
const amount = ref('0.137')
const destinationAddress = ref('')

const navItems = computed(() => [
  { label: t('nav.account'), to: '/account' },
  { label: t('account.depositNav'), to: '/account/deposit' },
  { label: t('account.withdrawNav'), to: '/account/withdraw' },
])

async function loadRequests() {
  if (!auth.token) return
  loading.value = true
  error.value = ''
  try {
    requests.value = await fetchMyGoldWithdrawalRequests(auth.token)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('goldWithdraw.loadError')
  } finally {
    loading.value = false
  }
}

async function submitWithdrawalRequest() {
  if (!auth.token) return
  error.value = ''
  success.value = ''
  try {
    await createGoldWithdrawalRequest(
      auth.token,
      network.value,
      Number(amount.value),
      destinationAddress.value.trim(),
    )
    success.value = t('goldWithdraw.createdSuccess')
    destinationAddress.value = ''
    await loadRequests()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('goldWithdraw.createError')
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }
  await loadRequests()
})
</script>

<template>
  <div>
    <ViewJumbotron
      :kicker="t('goldWithdraw.kicker')"
      :title="t('goldWithdraw.title')"
      :subtitle="t('goldWithdraw.subtitle')"
      variant="gold"
    />
    <ViewSubnav :items="navItems" :aria-label="t('nav.account')" />

    <main class="container py-8 space-y-6">
      <section class="card p-6 space-y-4">
        <h2 class="text-xl font-semibold">{{ t('goldWithdraw.newRequestTitle') }}</h2>
        <div class="grid gap-4 md:grid-cols-2">
          <label class="flex flex-col gap-1 text-sm">
            {{ t('goldWithdraw.network') }}
            <select v-model="network" class="form-control">
              <option value="ALGORAND">{{ t('goldWithdraw.algorandOption') }}</option>
              <option value="VOI">{{ t('goldWithdraw.voiOption') }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            {{ t('goldWithdraw.amount') }}
            <input v-model="amount" type="number" min="0.000001" step="0.000001" class="form-control" />
          </label>
        </div>
        <label class="flex flex-col gap-1 text-sm">
          {{ t('goldWithdraw.destinationAddress') }}
          <input v-model="destinationAddress" type="text" class="form-control" />
        </label>
        <button class="btn btn-primary" type="button" @click="submitWithdrawalRequest">
          {{ t('goldWithdraw.createButton') }}
        </button>
        <p v-if="success" class="text-good">{{ success }}</p>
        <p v-if="error" class="text-bad" role="alert">{{ error }}</p>
      </section>

      <section class="card p-6">
        <h2 class="text-xl font-semibold mb-4">{{ t('goldWithdraw.myRequestsTitle') }}</h2>
        <p v-if="loading" class="text-muted">{{ t('common.loading') }}</p>
        <p v-else-if="requests.length === 0" class="text-muted">{{ t('goldWithdraw.empty') }}</p>
        <div v-else class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-muted">
                <th class="py-2 pr-4">{{ t('goldWithdraw.requested') }}</th>
                <th class="py-2 pr-4">{{ t('goldWithdraw.network') }}</th>
                <th class="py-2 pr-4">{{ t('goldWithdraw.amount') }}</th>
                <th class="py-2 pr-4">{{ t('goldWithdraw.status') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="request in requests" :key="request.id" class="border-t border-divider">
                <td class="py-2 pr-4">{{ new Date(request.requestedAtUtc).toLocaleString() }}</td>
                <td class="py-2 pr-4">{{ request.network }}</td>
                <td class="py-2 pr-4">{{ request.amount.toFixed(4) }} g</td>
                <td class="py-2 pr-4">{{ request.status }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped>
.form-control {
  border: 1px solid var(--color-divider);
  background: var(--color-card);
  color: var(--color-text);
  border-radius: 0.5rem;
  padding: 0.55rem 0.7rem;
}
</style>
