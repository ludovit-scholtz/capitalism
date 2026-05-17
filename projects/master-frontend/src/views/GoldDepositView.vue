<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import {
  createGoldDepositRequest,
  fetchMyGoldDepositRequests,
  type GoldDepositRequestInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const requests = ref<GoldDepositRequestInfo[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')
const network = ref<'VOI' | 'ALGORAND'>('ALGORAND')
const amount = ref('0.137')
const senderAddress = ref('')

const navItems = computed(() => [
  { label: t('nav.account'), to: '/account' },
  { label: t('account.depositNav'), to: '/account/deposit' },
  { label: t('account.withdrawNav'), to: '/account/withdraw' },
])

const latestRequest = computed(() => requests.value[0] ?? null)
const latestArc26Uri = computed(() => {
  if (!latestRequest.value) return ''
  const amountMicro = Math.round(Number(latestRequest.value.amount) * 1_000_000)
  const note = encodeURIComponent(`CAPITALISM_DEPOSIT:${latestRequest.value.id}`)
  return `algorand://${latestRequest.value.depositAddress}?asset=${latestRequest.value.assetId}&amount=${amountMicro}&note=${note}`
})
const latestArc26QrUrl = computed(() =>
  latestArc26Uri.value
    ? `https://quickchart.io/qr?size=240&text=${encodeURIComponent(latestArc26Uri.value)}`
    : '',
)

async function loadRequests() {
  if (!auth.token) return
  loading.value = true
  error.value = ''
  try {
    requests.value = await fetchMyGoldDepositRequests(auth.token)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('goldDeposit.loadError')
  } finally {
    loading.value = false
  }
}

async function submitDepositRequest() {
  if (!auth.token) return
  error.value = ''
  success.value = ''
  try {
    await createGoldDepositRequest(
      auth.token,
      network.value,
      Number(amount.value),
      senderAddress.value.trim() || undefined,
    )
    success.value = t('goldDeposit.createdSuccess')
    await loadRequests()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('goldDeposit.createError')
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
      :kicker="t('goldDeposit.kicker')"
      :title="t('goldDeposit.title')"
      :subtitle="t('goldDeposit.subtitle')"
      variant="gold"
    />
    <ViewSubnav :items="navItems" :aria-label="t('nav.account')" />

    <main class="container py-8 space-y-6">
      <section class="card p-6 space-y-4">
        <h2 class="text-xl font-semibold">{{ t('goldDeposit.newRequestTitle') }}</h2>
        <div class="grid gap-4 md:grid-cols-2">
          <label class="flex flex-col gap-1 text-sm">
            {{ t('goldDeposit.network') }}
            <select v-model="network" class="form-control">
              <option value="ALGORAND">{{ t('goldDeposit.algorandOption') }}</option>
              <option value="VOI">{{ t('goldDeposit.voiOption') }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            {{ t('goldDeposit.amount') }}
            <input v-model="amount" type="number" min="0.000001" step="0.000001" class="form-control" />
          </label>
        </div>
        <label class="flex flex-col gap-1 text-sm">
          {{ t('goldDeposit.senderAddress') }}
          <input v-model="senderAddress" type="text" class="form-control" />
        </label>
        <button class="btn btn-primary" type="button" @click="submitDepositRequest">
          {{ t('goldDeposit.createButton') }}
        </button>
        <p v-if="success" class="text-good">{{ success }}</p>
        <p v-if="error" class="text-bad" role="alert">{{ error }}</p>
      </section>

      <section v-if="latestRequest" class="card p-6 space-y-3">
        <h2 class="text-xl font-semibold">{{ t('goldDeposit.latestQrTitle') }}</h2>
        <p class="text-sm text-muted">
          {{ t('goldDeposit.assetAndNetwork', { assetId: latestRequest.assetId, network: latestRequest.network }) }}
        </p>
        <p class="text-sm text-muted break-all">
          {{ t('goldDeposit.depositAddressLabel') }}: {{ latestRequest.depositAddress }}
        </p>
        <img
          v-if="latestArc26QrUrl"
          :src="latestArc26QrUrl"
          :alt="t('goldDeposit.arc26QrAlt')"
          class="max-w-60"
        />
        <p class="text-xs break-all text-muted">{{ latestArc26Uri }}</p>
      </section>

      <section class="card p-6">
        <h2 class="text-xl font-semibold mb-4">{{ t('goldDeposit.myRequestsTitle') }}</h2>
        <p v-if="loading" class="text-muted">{{ t('common.loading') }}</p>
        <p v-else-if="requests.length === 0" class="text-muted">{{ t('goldDeposit.empty') }}</p>
        <div v-else class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-muted">
                <th class="py-2 pr-4">{{ t('goldDeposit.requested') }}</th>
                <th class="py-2 pr-4">{{ t('goldDeposit.network') }}</th>
                <th class="py-2 pr-4">{{ t('goldDeposit.amount') }}</th>
                <th class="py-2 pr-4">{{ t('goldDeposit.status') }}</th>
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
