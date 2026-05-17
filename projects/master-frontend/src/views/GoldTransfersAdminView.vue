<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import {
  fetchGoldDepositRequests,
  fetchGoldWithdrawalRequests,
  processGoldDepositRequest,
  processGoldWithdrawalRequest,
  type GoldDepositRequestInfo,
  type GoldWithdrawalRequestInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const deposits = ref<GoldDepositRequestInfo[]>([])
const withdrawals = ref<GoldWithdrawalRequestInfo[]>([])
const loading = ref(false)
const error = ref('')

const navItems = computed(() => [
  { label: t('home.goldAdmin'), to: '/gold-admin' },
  { label: t('goldAdmin.transferOps'), to: '/gold-transfers-admin' },
])

async function loadAll() {
  if (!auth.token) return
  loading.value = true
  error.value = ''
  try {
    const [depositRows, withdrawalRows] = await Promise.all([
      fetchGoldDepositRequests(auth.token),
      fetchGoldWithdrawalRequests(auth.token),
    ])
    deposits.value = depositRows
    withdrawals.value = withdrawalRows
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('goldTransfersAdmin.loadError')
  } finally {
    loading.value = false
  }
}

async function processDeposit(requestId: string) {
  if (!auth.token) return
  await processGoldDepositRequest(auth.token, requestId)
  await loadAll()
}

async function processWithdrawal(requestId: string) {
  if (!auth.token) return
  await processGoldWithdrawalRequest(auth.token, requestId)
  await loadAll()
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }
  if (!auth.isGameAdmin) {
    await router.push('/')
    return
  }
  await loadAll()
})
</script>

<template>
  <div>
    <ViewJumbotron
      :kicker="t('goldTransfersAdmin.kicker')"
      :title="t('goldTransfersAdmin.title')"
      :subtitle="t('goldTransfersAdmin.subtitle')"
      variant="gold"
    />
    <ViewSubnav :items="navItems" :aria-label="t('goldAdmin.transferOps')" />

    <main class="container py-8 space-y-6">
      <p v-if="loading" class="text-muted">{{ t('common.loading') }}</p>
      <p v-else-if="error" class="text-bad" role="alert">{{ error }}</p>

      <section class="card p-6">
        <h2 class="text-xl font-semibold mb-4">{{ t('goldTransfersAdmin.depositsTitle') }}</h2>
        <p v-if="deposits.length === 0" class="text-muted">{{ t('goldTransfersAdmin.emptyDeposits') }}</p>
        <div v-else class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-muted">
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.player') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.network') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.amount') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.status') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="request in deposits" :key="request.id" class="border-t border-divider">
                <td class="py-2 pr-4">{{ request.playerEmail }}</td>
                <td class="py-2 pr-4">{{ request.network }}</td>
                <td class="py-2 pr-4">{{ request.amount.toFixed(4) }} g</td>
                <td class="py-2 pr-4">{{ request.status }}</td>
                <td class="py-2 pr-4">
                  <button
                    v-if="request.status === 'PENDING'"
                    class="btn btn-secondary"
                    type="button"
                    @click="processDeposit(request.id)"
                  >
                    {{ t('goldTransfersAdmin.markProcessed') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="card p-6">
        <h2 class="text-xl font-semibold mb-4">{{ t('goldTransfersAdmin.withdrawalsTitle') }}</h2>
        <p v-if="withdrawals.length === 0" class="text-muted">
          {{ t('goldTransfersAdmin.emptyWithdrawals') }}
        </p>
        <div v-else class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-muted">
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.player') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.network') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.amount') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.status') }}</th>
                <th class="py-2 pr-4">{{ t('goldTransfersAdmin.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="request in withdrawals" :key="request.id" class="border-t border-divider">
                <td class="py-2 pr-4">{{ request.playerEmail }}</td>
                <td class="py-2 pr-4">{{ request.network }}</td>
                <td class="py-2 pr-4">{{ request.amount.toFixed(4) }} g</td>
                <td class="py-2 pr-4">{{ request.status }}</td>
                <td class="py-2 pr-4">
                  <button
                    v-if="request.status === 'PENDING'"
                    class="btn btn-secondary"
                    type="button"
                    @click="processWithdrawal(request.id)"
                  >
                    {{ t('goldTransfersAdmin.markProcessed') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped></style>
