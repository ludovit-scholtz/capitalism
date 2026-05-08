<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
  fetchMyApiKeys,
  generateApiKey,
  revokeApiKey,
  type ApiKeyInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

// The game API GraphQL URL — same env var used by the game frontend.
const GAME_GRAPHQL_URL =
  import.meta.env.VITE_GAME_GRAPHQL_URL ||
  'https://capitalism.de-4.biatec.io/graphql'

const keys = ref<ApiKeyInfo[]>([])
const loading = ref(false)
const loadError = ref('')

// Generate modal
const showGenerateModal = ref(false)
const newKeyName = ref('')
const generatedKey = ref('')
const generatedKeyId = ref('')
const generating = ref(false)
const generateError = ref('')
const copied = ref(false)

// Revoke
const revoking = ref<string | null>(null)

function formatDate(iso: string | null): string {
  if (!iso) return t('apiKeys.never')
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso))
}

async function loadKeys() {
  if (!auth.token) return
  loading.value = true
  loadError.value = ''
  try {
    keys.value = await fetchMyApiKeys(GAME_GRAPHQL_URL, auth.token)
  } catch (e: unknown) {
    loadError.value = e instanceof Error ? e.message : t('apiKeys.loadError')
  } finally {
    loading.value = false
  }
}

function openGenerateModal() {
  newKeyName.value = ''
  generatedKey.value = ''
  generatedKeyId.value = ''
  generateError.value = ''
  copied.value = false
  showGenerateModal.value = true
}

function closeGenerateModal() {
  showGenerateModal.value = false
  if (generatedKey.value) {
    // Reload keys to reflect the new entry in the table.
    void loadKeys()
  }
}

async function handleGenerate() {
  if (!auth.token || !newKeyName.value.trim()) return
  generating.value = true
  generateError.value = ''
  try {
    const result = await generateApiKey(GAME_GRAPHQL_URL, auth.token, newKeyName.value.trim())
    generatedKey.value = result.plaintextKey
    generatedKeyId.value = result.apiKey.id
  } catch (e: unknown) {
    generateError.value = e instanceof Error ? e.message : 'Failed to generate key.'
  } finally {
    generating.value = false
  }
}

async function copyKey() {
  try {
    await navigator.clipboard.writeText(generatedKey.value)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch {
    // Fallback: silently ignore clipboard errors
  }
}

async function handleRevoke(key: ApiKeyInfo) {
  if (!auth.token) return
  if (!confirm(t('apiKeys.revokeConfirm'))) return
  revoking.value = key.id
  try {
    await revokeApiKey(GAME_GRAPHQL_URL, auth.token, key.id)
    await loadKeys()
  } catch {
    // Silently ignore — key list will refresh
  } finally {
    revoking.value = null
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }
  await loadKeys()
})
</script>

<template>
  <main class="container max-w-4xl pb-16 pt-6 lg:pb-20 lg:pt-8">
    <div class="mb-8 flex items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-body">{{ t('apiKeys.title') }}</h1>
        <p class="mt-1 text-sm text-muted">{{ t('apiKeys.subtitle') }}</p>
      </div>
      <button class="btn btn-primary shrink-0" @click="openGenerateModal">
        {{ t('apiKeys.generateButton') }}
      </button>
    </div>

    <!-- Auth instructions -->
    <div class="mb-6 rounded-lg border border-brand/20 bg-brand/5 px-4 py-3 text-sm text-muted">
      <code class="break-all font-mono">{{ t('apiKeys.authInstructions') }}</code>
    </div>

    <!-- Error state -->
    <div v-if="loadError" class="mb-4 rounded-md border border-bad/30 bg-bad/10 px-4 py-3 text-sm text-bad" role="alert">
      {{ loadError }}
    </div>

    <!-- Loading state -->
    <div v-if="loading" class="py-8 text-center text-muted">{{ t('common.loading') }}</div>

    <!-- Empty state -->
    <div v-else-if="!loading && keys.length === 0 && !loadError" class="py-12 text-center text-muted">
      {{ t('apiKeys.noKeys') }}
    </div>

    <!-- Keys table -->
    <div v-else-if="keys.length > 0" class="overflow-x-auto rounded-xl border border-divider">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-divider bg-surface text-left">
            <th class="px-4 py-3 font-medium text-muted">{{ t('apiKeys.tableKeyName') }}</th>
            <th class="px-4 py-3 font-medium text-muted">{{ t('apiKeys.tableCreated') }}</th>
            <th class="px-4 py-3 font-medium text-muted">{{ t('apiKeys.tableLastUsed') }}</th>
            <th class="px-4 py-3 font-medium text-muted">{{ t('apiKeys.tableCalls') }}</th>
            <th class="px-4 py-3 font-medium text-muted">{{ t('apiKeys.tableStatus') }}</th>
            <th class="px-4 py-3 font-medium text-muted">{{ t('apiKeys.tableActions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="key in keys" :key="key.id" class="border-b border-divider last:border-0">
            <td class="px-4 py-3 font-medium text-body">{{ key.name }}</td>
            <td class="px-4 py-3 text-muted">{{ formatDate(key.createdAtUtc) }}</td>
            <td class="px-4 py-3 text-muted">{{ formatDate(key.lastUsedAtUtc) }}</td>
            <td class="px-4 py-3 text-muted">{{ key.totalCallCount.toLocaleString() }}</td>
            <td class="px-4 py-3">
              <span
                :class="key.revokedAtUtc ? 'text-bad' : 'text-good'"
                class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
              >
                {{ key.revokedAtUtc ? t('apiKeys.statusRevoked') : t('apiKeys.statusActive') }}
              </span>
            </td>
            <td class="px-4 py-3">
              <button
                v-if="!key.revokedAtUtc"
                class="btn btn-secondary btn-sm text-xs text-bad hover:bg-bad/10"
                :disabled="revoking === key.id"
                @click="handleRevoke(key)"
              >
                {{ revoking === key.id ? '…' : t('apiKeys.revokeButton') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Generate modal -->
    <Teleport to="body">
      <div
        v-if="showGenerateModal"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
        @click.self="closeGenerateModal"
      >
        <div class="w-full max-w-md rounded-2xl border border-divider bg-card p-6 shadow-2xl">
          <h2 class="mb-4 text-lg font-bold text-body">{{ t('apiKeys.generateModalTitle') }}</h2>

          <!-- Step 1: name input -->
          <template v-if="!generatedKey">
            <div v-if="generateError" class="mb-3 rounded-md bg-bad/10 px-3 py-2 text-sm text-bad" role="alert">
              {{ generateError }}
            </div>
            <label for="apiKeyName" class="mb-1 block text-sm font-medium text-muted">
              {{ t('apiKeys.generateModalNameLabel') }}
            </label>
            <input
              id="apiKeyName"
              v-model="newKeyName"
              type="text"
              class="form-input mb-4 w-full"
              :placeholder="t('apiKeys.generateModalNamePlaceholder')"
              maxlength="80"
              @keyup.enter="handleGenerate"
            />
            <div class="flex justify-end gap-3">
              <button class="btn btn-secondary" @click="closeGenerateModal">{{ t('common.cancel') }}</button>
              <button
                class="btn btn-primary"
                :disabled="generating || !newKeyName.trim()"
                @click="handleGenerate"
              >
                {{ generating ? t('common.loading') : t('apiKeys.generateModalSubmit') }}
              </button>
            </div>
          </template>

          <!-- Step 2: show generated key once -->
          <template v-else>
            <p class="mb-1 font-semibold text-good">{{ t('apiKeys.generatedSuccessTitle') }}</p>
            <p class="mb-3 text-sm text-muted">{{ t('apiKeys.generatedSuccessWarning') }}</p>
            <div class="mb-4 flex items-center gap-2 rounded-lg border border-divider bg-surface px-3 py-2">
              <code class="flex-1 break-all text-xs text-body">{{ generatedKey }}</code>
              <button
                class="btn btn-secondary btn-sm shrink-0 text-xs"
                @click="copyKey"
              >
                {{ copied ? t('apiKeys.copiedButton') : t('apiKeys.copyButton') }}
              </button>
            </div>
            <div class="flex justify-end">
              <button class="btn btn-primary" @click="closeGenerateModal">{{ t('apiKeys.closeModal') }}</button>
            </div>
          </template>
        </div>
      </div>
    </Teleport>
  </main>
</template>
