<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
  fetchAdminApiKeyAuditLog,
  fetchAdminApiKeys,
  fetchMyApiKeyAuditLog,
  fetchMyApiKeys,
  fetchMyCompaniesForApiKeys,
  forceRevokeApiKey,
  generateApiKey,
  revokeAllPlayerApiKeys,
  revokeApiKey,
  type AdminApiKeyInfo,
  type ApiKeyAuditLogInfo,
  type ApiKeyInfo,
  type GameCompanySummary,
} from '@/lib/masterApi'
import ApiKeyAuditTable from '@/components/api-keys/ApiKeyAuditTable.vue'
import ApiKeyScopeBadges from '@/components/api-keys/ApiKeyScopeBadges.vue'
import { useAuthStore } from '@/stores/auth'

const GAME_GRAPHQL_URL =
  import.meta.env.VITE_GAME_GRAPHQL_URL ||
  'https://capitalism.de-4.biatec.io/graphql'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const keys = ref<ApiKeyInfo[]>([])
const companies = ref<GameCompanySummary[]>([])
const auditEntries = ref<ApiKeyAuditLogInfo[]>([])
const loading = ref(false)
const loadError = ref('')
const notice = ref('')

const showGenerateModal = ref(false)
const newKeyName = ref('')
const selectedScopes = ref<string[]>(['read-only'])
const selectedCompanyIds = ref<string[]>([])
const generatedKey = ref('')
const generating = ref(false)
const generateError = ref('')
const copied = ref(false)
const revoking = ref<string | null>(null)

const adminKeys = ref<AdminApiKeyInfo[]>([])
const adminAuditEntries = ref<ApiKeyAuditLogInfo[]>([])
const adminFilter = ref('')
const adminLoading = ref(false)
const adminError = ref('')
const adminForceRevoking = ref<string | null>(null)
const adminBulkRevokingPlayerId = ref<string | null>(null)

const scopeOptions = computed(() => [
  { value: 'read-only', label: t('apiKeys.scopes.readOnly'), description: t('apiKeys.scopeDescriptions.readOnly') },
  { value: 'bot-only', label: t('apiKeys.scopes.botOnly'), description: t('apiKeys.scopeDescriptions.botOnly') },
  { value: 'trading-only', label: t('apiKeys.scopes.tradingOnly'), description: t('apiKeys.scopeDescriptions.tradingOnly') },
  { value: 'company-bound', label: t('apiKeys.scopes.companyBound'), description: t('apiKeys.scopeDescriptions.companyBound') },
])

const hasCompanyBoundScope = computed(() => selectedScopes.value.includes('company-bound'))
const companyLookup = computed(() => new Map(companies.value.map((company) => [company.id, company.name])))
const isAdminVisible = computed(() => auth.isGameAdmin)

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

function formatCompanyIds(companyIds: string[]): string {
  if (companyIds.length === 0) return t('apiKeys.companyScopeAllCompanies')
  return companyIds
    .map((companyId) => companyLookup.value.get(companyId) ?? companyId)
    .join(', ')
}

function setNotice(message: string) {
  notice.value = message
  setTimeout(() => {
    if (notice.value === message) notice.value = ''
  }, 2500)
}

async function loadKeys() {
  if (!auth.token) return
  loading.value = true
  loadError.value = ''
  try {
    const [nextKeys, nextCompanies, nextAudit] = await Promise.all([
      fetchMyApiKeys(GAME_GRAPHQL_URL, auth.token),
      fetchMyCompaniesForApiKeys(GAME_GRAPHQL_URL, auth.token),
      fetchMyApiKeyAuditLog(GAME_GRAPHQL_URL, auth.token, 25),
    ])
    keys.value = nextKeys
    companies.value = nextCompanies
    auditEntries.value = nextAudit
  } catch (error: unknown) {
    loadError.value = error instanceof Error ? error.message : t('apiKeys.loadError')
  } finally {
    loading.value = false
  }
}

async function loadAdminData() {
  if (!auth.token || !isAdminVisible.value) return
  adminLoading.value = true
  adminError.value = ''
  try {
    const [nextAdminKeys, nextAdminAudit] = await Promise.all([
      fetchAdminApiKeys(GAME_GRAPHQL_URL, auth.token, adminFilter.value.trim(), true, 100),
      fetchAdminApiKeyAuditLog(GAME_GRAPHQL_URL, auth.token, adminFilter.value.trim(), undefined, 50),
    ])
    adminKeys.value = nextAdminKeys
    adminAuditEntries.value = nextAdminAudit
  } catch (error: unknown) {
    adminError.value = error instanceof Error ? error.message : t('apiKeys.adminLoadError')
  } finally {
    adminLoading.value = false
  }
}

function openGenerateModal() {
  newKeyName.value = ''
  selectedScopes.value = ['read-only']
  selectedCompanyIds.value = []
  generatedKey.value = ''
  generateError.value = ''
  copied.value = false
  showGenerateModal.value = true
}

function closeGenerateModal() {
  showGenerateModal.value = false
  if (generatedKey.value) {
    setNotice(t('apiKeys.generateSuccess'))
    void loadKeys()
  }
}

function toggleScope(scope: string, checked: boolean) {
  if (checked) {
    if (!selectedScopes.value.includes(scope)) {
      selectedScopes.value = [...selectedScopes.value, scope]
    }
    return
  }

  selectedScopes.value = selectedScopes.value.filter((value) => value !== scope)
  if (scope === 'company-bound') {
    selectedCompanyIds.value = []
  }
}

function handleScopeCheckboxChange(scope: string, event: Event) {
  const target = event.target as HTMLInputElement | null
  toggleScope(scope, !!target?.checked)
}

async function handleGenerate() {
  if (!auth.token || !newKeyName.value.trim()) return
  generating.value = true
  generateError.value = ''
  try {
    const result = await generateApiKey(
      GAME_GRAPHQL_URL,
      auth.token,
      newKeyName.value.trim(),
      selectedScopes.value,
      hasCompanyBoundScope.value ? selectedCompanyIds.value : [],
    )
    generatedKey.value = result.plaintextKey
  } catch (error: unknown) {
    generateError.value = error instanceof Error ? error.message : t('apiKeys.generateError')
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
    // Ignore clipboard issues in unsupported browsers.
  }
}

async function handleRevoke(key: ApiKeyInfo) {
  if (!auth.token || !confirm(t('apiKeys.revokeConfirm'))) return
  revoking.value = key.id
  try {
    await revokeApiKey(GAME_GRAPHQL_URL, auth.token, key.id)
    setNotice(t('apiKeys.revokeSuccess'))
    await loadKeys()
    if (isAdminVisible.value) await loadAdminData()
  } finally {
    revoking.value = null
  }
}

async function handleForceRevoke(keyId: string) {
  if (!auth.token || !confirm(t('apiKeys.adminRevokeConfirm'))) return
  adminForceRevoking.value = keyId
  try {
    await forceRevokeApiKey(GAME_GRAPHQL_URL, auth.token, keyId)
    setNotice(t('apiKeys.adminRevokeSuccess'))
    await Promise.all([loadKeys(), loadAdminData()])
  } finally {
    adminForceRevoking.value = null
  }
}

async function handleRevokeAll(playerId: string) {
  if (!auth.token || !confirm(t('apiKeys.revokeAllConfirm'))) return
  adminBulkRevokingPlayerId.value = playerId
  try {
    const revokedCount = await revokeAllPlayerApiKeys(GAME_GRAPHQL_URL, auth.token, playerId)
    setNotice(t('apiKeys.revokeAllSuccess', { count: revokedCount }))
    await Promise.all([loadKeys(), loadAdminData()])
  } finally {
    adminBulkRevokingPlayerId.value = null
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  if (!auth.gameAdminChecked) {
    await auth.refreshGameAdminAccess()
  }

  await loadKeys()
  if (isAdminVisible.value) {
    await loadAdminData()
  }
})
</script>

<template>
  <main class="container max-w-6xl pb-16 pt-6 lg:pb-20 lg:pt-8">
    <div class="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-body">{{ t('apiKeys.title') }}</h1>
        <p class="mt-1 text-sm text-muted">{{ t('apiKeys.subtitle') }}</p>
      </div>
      <button class="btn btn-primary shrink-0" @click="openGenerateModal">
        {{ t('apiKeys.generateButton') }}
      </button>
    </div>

    <div class="mb-6 rounded-lg border border-brand/20 bg-brand/5 px-4 py-3 text-sm text-muted">
      <code class="break-all font-mono">{{ t('apiKeys.authInstructions') }}</code>
    </div>

    <p v-if="notice" class="mb-4 rounded-md border border-good/20 bg-good/10 px-4 py-3 text-sm text-good" role="status">
      {{ notice }}
    </p>
    <p v-if="loadError" class="mb-4 rounded-md border border-bad/20 bg-bad/10 px-4 py-3 text-sm text-bad" role="alert">
      {{ loadError }}
    </p>

    <section class="card p-6" aria-label="Player API keys">
      <div class="mb-4 flex items-center justify-between gap-3">
        <h2 class="text-xl font-semibold text-body">{{ t('apiKeys.playerSectionTitle') }}</h2>
        <button class="btn btn-secondary" @click="loadKeys">{{ t('common.refresh') }}</button>
      </div>

      <p v-if="loading" class="state-message">{{ t('common.loading') }}</p>
      <p v-else-if="keys.length === 0" class="state-message">{{ t('apiKeys.noKeys') }}</p>
      <div v-else class="overflow-x-auto rounded-xl border border-divider">
        <table class="min-w-full text-sm">
          <thead>
            <tr class="bg-surface text-left text-xs uppercase tracking-[0.08em] text-muted">
              <th class="px-4 py-3">{{ t('apiKeys.tableKeyName') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableScopes') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableCompanyScope') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableCreated') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableLastUsed') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableStatus') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableActions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="key in keys" :key="key.id" class="border-t border-divider/70 align-top">
              <td class="px-4 py-3 font-medium text-body">{{ key.name }}</td>
              <td class="px-4 py-3"><ApiKeyScopeBadges :scopes="key.scopes" /></td>
              <td class="px-4 py-3 text-muted">{{ formatCompanyIds(key.companyIds) }}</td>
              <td class="px-4 py-3 text-muted">{{ formatDate(key.createdAtUtc) }}</td>
              <td class="px-4 py-3 text-muted">{{ formatDate(key.lastUsedAtUtc) }}</td>
              <td class="px-4 py-3 text-muted">
                {{ key.revokedAtUtc ? t('apiKeys.statusRevoked') : t('apiKeys.statusActive') }}
              </td>
              <td class="px-4 py-3">
                <button
                  v-if="!key.revokedAtUtc"
                  class="btn btn-secondary text-bad"
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
    </section>

    <section class="card mt-6 p-6" aria-label="Player API key audit log">
      <h2 class="text-xl font-semibold text-body">{{ t('apiKeys.auditTitle') }}</h2>
      <p v-if="auditEntries.length === 0" class="state-message mt-4">{{ t('common.noData') }}</p>
      <ApiKeyAuditTable v-else class="mt-4" :entries="auditEntries" />
    </section>

    <section v-if="isAdminVisible" class="card mt-6 p-6" aria-label="Admin API key tooling">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 class="text-xl font-semibold text-body">{{ t('apiKeys.adminTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('apiKeys.adminSubtitle') }}</p>
        </div>
        <div class="flex flex-wrap items-end gap-3">
          <label class="text-sm text-muted">
            {{ t('apiKeys.adminFilterLabel') }}
            <input v-model="adminFilter" class="form-input mt-1 w-72" type="text" />
          </label>
          <button class="btn btn-secondary" @click="loadAdminData">{{ t('common.refresh') }}</button>
        </div>
      </div>

      <p v-if="adminError" class="state-error mt-4" role="alert">{{ adminError }}</p>
      <p v-if="adminLoading" class="state-message mt-4">{{ t('common.loading') }}</p>

      <div v-else-if="adminKeys.length > 0" class="mt-4 overflow-x-auto rounded-xl border border-divider">
        <table class="min-w-full text-sm">
          <thead>
            <tr class="bg-surface text-left text-xs uppercase tracking-[0.08em] text-muted">
              <th class="px-4 py-3">{{ t('apiKeys.adminPlayer') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableKeyName') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableScopes') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableLastUsed') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableStatus') }}</th>
              <th class="px-4 py-3">{{ t('apiKeys.tableActions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in adminKeys" :key="row.key.id" class="border-t border-divider/70 align-top">
              <td class="px-4 py-3 text-body">
                <div class="font-medium">{{ row.playerDisplayName }}</div>
                <div class="text-xs text-muted">{{ row.playerEmail }}</div>
              </td>
              <td class="px-4 py-3 font-medium text-body">{{ row.key.name }}</td>
              <td class="px-4 py-3"><ApiKeyScopeBadges :scopes="row.key.scopes" /></td>
              <td class="px-4 py-3 text-muted">{{ formatDate(row.key.lastUsedAtUtc) }}</td>
              <td class="px-4 py-3 text-muted">
                {{ row.key.revokedAtUtc ? t('apiKeys.statusRevoked') : t('apiKeys.statusActive') }}
              </td>
              <td class="px-4 py-3">
                <div class="flex flex-wrap gap-2">
                  <button
                    class="btn btn-secondary text-bad"
                    :disabled="adminForceRevoking === row.key.id"
                    @click="handleForceRevoke(row.key.id)"
                  >
                    {{ adminForceRevoking === row.key.id ? '…' : t('apiKeys.adminRevokeButton') }}
                  </button>
                  <button
                    class="btn btn-secondary"
                    :disabled="adminBulkRevokingPlayerId === row.playerId"
                    @click="handleRevokeAll(row.playerId)"
                  >
                    {{ adminBulkRevokingPlayerId === row.playerId ? '…' : t('apiKeys.revokeAllButton') }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <ApiKeyAuditTable
        v-if="adminAuditEntries.length > 0"
        class="mt-6"
        :entries="adminAuditEntries"
        :show-player="true"
      />
    </section>

    <Teleport to="body">
      <div
        v-if="showGenerateModal"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
        @click.self="closeGenerateModal"
      >
        <div class="w-full max-w-2xl rounded-2xl border border-divider bg-card p-6 shadow-2xl">
          <h2 class="mb-4 text-lg font-bold text-body">{{ t('apiKeys.generateModalTitle') }}</h2>

          <template v-if="!generatedKey">
            <p v-if="generateError" class="mb-3 rounded-md bg-bad/10 px-3 py-2 text-sm text-bad" role="alert">
              {{ generateError }}
            </p>

            <label for="apiKeyName" class="mb-1 block text-sm font-medium text-muted">
              {{ t('apiKeys.generateModalNameLabel') }}
            </label>
            <input
              id="apiKeyName"
              v-model="newKeyName"
              type="text"
              class="form-input mb-5 w-full"
              :placeholder="t('apiKeys.generateModalNamePlaceholder')"
              maxlength="80"
            />

            <div class="grid gap-4 md:grid-cols-2">
              <label
                v-for="option in scopeOptions"
                :key="option.value"
                class="rounded-xl border border-divider p-4"
              >
                <div class="flex items-start gap-3">
                  <input
                    type="checkbox"
                    class="mt-1"
                    :checked="selectedScopes.includes(option.value)"
                    @change="handleScopeCheckboxChange(option.value, $event)"
                  />
                  <div>
                    <div class="font-medium text-body">{{ option.label }}</div>
                    <p class="mt-1 text-sm text-muted">{{ option.description }}</p>
                  </div>
                </div>
              </label>
            </div>

            <div v-if="hasCompanyBoundScope" class="mt-5 rounded-xl border border-divider p-4">
              <h3 class="font-medium text-body">{{ t('apiKeys.companyScopeTitle') }}</h3>
              <p class="mt-1 text-sm text-muted">{{ t('apiKeys.companyScopeHelp') }}</p>
              <div class="mt-3 grid gap-2 md:grid-cols-2">
                <label
                  v-for="company in companies"
                  :key="company.id"
                  class="flex items-center gap-3 rounded-lg border border-divider px-3 py-2"
                >
                  <input v-model="selectedCompanyIds" type="checkbox" :value="company.id" />
                  <span class="text-sm text-body">{{ company.name }}</span>
                </label>
              </div>
            </div>

            <div class="mt-5 flex justify-end gap-3">
              <button class="btn btn-secondary" @click="closeGenerateModal">{{ t('common.cancel') }}</button>
              <button
                class="btn btn-primary"
                :disabled="generating || !newKeyName.trim() || selectedScopes.length === 0 || (hasCompanyBoundScope && selectedCompanyIds.length === 0)"
                @click="handleGenerate"
              >
                {{ generating ? t('common.loading') : t('apiKeys.generateModalSubmit') }}
              </button>
            </div>
          </template>

          <template v-else>
            <p class="mb-1 font-semibold text-good">{{ t('apiKeys.generatedSuccessTitle') }}</p>
            <p class="mb-3 text-sm text-muted">{{ t('apiKeys.generatedSuccessWarning') }}</p>
            <div class="mb-4 flex items-center gap-2 rounded-lg border border-divider bg-surface px-3 py-2">
              <code class="flex-1 break-all text-xs text-body">{{ generatedKey }}</code>
              <button class="btn btn-secondary text-xs" @click="copyKey">
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
