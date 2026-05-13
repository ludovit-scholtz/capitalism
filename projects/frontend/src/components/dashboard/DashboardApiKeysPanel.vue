<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'

type ApiKeyInfo = {
  id: string
  name: string
  createdAtUtc: string
  lastUsedAtUtc: string | null
  revokedAtUtc: string | null
}

const { t, locale } = useI18n()

const loading = ref(false)
const loadError = ref<string | null>(null)
const keys = ref<ApiKeyInfo[]>([])

const generating = ref(false)
const newKeyName = ref('')
const generatedKey = ref<string | null>(null)
const copied = ref(false)
const actionError = ref<string | null>(null)

function formatDate(iso: string | null): string {
  if (!iso) return t('dashboard.apiKeysNeverUsed')
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(iso))
}

function maskKeyPrefix(key: ApiKeyInfo): string {
  return `sk_live_${key.id.replace(/-/g, '').slice(0, 8)}...`
}

async function loadKeys() {
  loading.value = true
  loadError.value = null
  try {
    const data = await gqlRequest<{ myApiKeys: ApiKeyInfo[] }>(`query GetMyApiKeys {
      myApiKeys {
        id
        name
        createdAtUtc
        lastUsedAtUtc
        revokedAtUtc
      }
    }`)
    keys.value = data.myApiKeys
  } catch (error: unknown) {
    loadError.value = error instanceof Error ? error.message : t('dashboard.apiKeysLoadError')
  } finally {
    loading.value = false
  }
}

async function generateKey() {
  const trimmedName = newKeyName.value.trim()
  if (!trimmedName || generating.value) return
  if (!window.confirm(t('dashboard.apiKeysGenerateConfirm'))) return

  generating.value = true
  actionError.value = null
  generatedKey.value = null
  copied.value = false

  try {
    const data = await gqlRequest<{
      generateApiKey: {
        plaintextKey: string
      }
    }>(
      `mutation GenerateApiKey($input: GenerateApiKeyInput!) {
        generateApiKey(input: $input) {
          plaintextKey
        }
      }`,
      {
        input: {
          name: trimmedName,
          // Until dedicated scope-selection UI is added, generate read + bot + trading scopes by default.
          scopes: ['read-only', 'bot-only', 'trading-only'],
        },
      },
    )
    generatedKey.value = data.generateApiKey.plaintextKey
    newKeyName.value = ''
    await loadKeys()
  } catch (error: unknown) {
    actionError.value = error instanceof Error ? error.message : t('dashboard.apiKeysGenerateError')
  } finally {
    generating.value = false
  }
}

async function copyGeneratedKey() {
  if (!generatedKey.value) return
  try {
    await navigator.clipboard.writeText(generatedKey.value)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 1800)
  } catch {
    // Ignore unsupported clipboard environment.
  }
}

async function revokeKey(keyId: string) {
  if (!window.confirm(t('dashboard.apiKeysRevokeConfirm'))) return

  actionError.value = null
  try {
    await gqlRequest<{ revokeApiKey: boolean }>(
      `mutation RevokeApiKey($input: RevokeApiKeyInput!) {
        revokeApiKey(input: $input)
      }`,
      { input: { keyId } },
    )
    await loadKeys()
  } catch (error: unknown) {
    actionError.value = error instanceof Error ? error.message : t('dashboard.apiKeysRevokeError')
  }
}

onMounted(() => {
  void loadKeys()
})
</script>

<template>
  <section class="api-keys-panel mt-6 border-t border-divider pt-5">
    <div class="mb-4 flex flex-col gap-1.5">
      <h3 class="text-[0.9375rem] font-bold">{{ t('dashboard.apiKeysTitle') }}</h3>
      <p class="m-0 text-sm text-muted">{{ t('dashboard.apiKeysBody') }}</p>
    </div>

    <form class="flex flex-col gap-3 md:flex-row md:items-end" @submit.prevent="generateKey">
      <label class="flex flex-1 flex-col gap-1.5">
        <span class="text-sm font-semibold">{{ t('dashboard.apiKeysNameLabel') }}</span>
        <input
          v-model="newKeyName"
          type="text"
          maxlength="80"
          :placeholder="t('dashboard.apiKeysNamePlaceholder')"
          class="rounded border border-divider bg-page px-3.5 py-3 text-body transition-colors focus:border-brand focus:outline-none"
        />
      </label>
      <button class="btn btn-primary self-start md:self-auto" type="submit" :disabled="generating || !newKeyName.trim()">
        {{ generating ? t('common.loading') : t('dashboard.apiKeysGenerateCta') }}
      </button>
    </form>

    <div
      v-if="generatedKey"
      class="api-key-generated-card mt-4 flex flex-col gap-2 rounded-lg border border-amber-400/35 bg-amber-400/10 p-3"
      role="status"
    >
      <strong class="text-sm text-amber-200">{{ t('dashboard.apiKeysShownOnceTitle') }}</strong>
      <p class="m-0 text-sm text-amber-100">{{ t('dashboard.apiKeysShownOnceBody') }}</p>
      <code aria-label="Generated API key" class="rounded border border-amber-400/30 bg-black/20 px-2 py-1 text-xs break-all">{{ generatedKey }}</code>
      <button class="btn btn-secondary w-fit" type="button" @click="copyGeneratedKey">
        {{ copied ? t('dashboard.apiKeysCopied') : t('dashboard.apiKeysCopy') }}
      </button>
    </div>

    <p v-if="actionError" class="mt-3 m-0 rounded-lg bg-[rgba(248,113,113,0.12)] px-3 py-3 text-sm text-bad" role="alert">
      {{ actionError }}
    </p>
    <p v-if="loadError" class="mt-3 m-0 rounded-lg bg-[rgba(248,113,113,0.12)] px-3 py-3 text-sm text-bad" role="alert">
      {{ loadError }}
    </p>
    <p v-if="loading" class="mt-4 m-0 text-sm text-muted">{{ t('common.loading') }}</p>

    <div v-else-if="keys.length === 0" class="api-keys-empty-state mt-4 rounded-lg border border-divider bg-card-raised p-4">
      <p class="m-0 text-sm text-muted">{{ t('dashboard.apiKeysEmptyState') }}</p>
    </div>

    <div v-else class="mt-4 overflow-x-auto">
      <table class="w-full min-w-[36rem] border-collapse text-left text-sm">
        <thead>
          <tr class="border-b border-divider text-xs uppercase tracking-wide text-muted">
            <th class="px-3 py-2">{{ t('dashboard.apiKeysPrefixColumn') }}</th>
            <th class="px-3 py-2">{{ t('dashboard.apiKeysNameColumn') }}</th>
            <th class="px-3 py-2">{{ t('dashboard.apiKeysCreatedColumn') }}</th>
            <th class="px-3 py-2">{{ t('dashboard.apiKeysLastUsedColumn') }}</th>
            <th class="px-3 py-2">{{ t('dashboard.apiKeysActionColumn') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="key in keys" :key="key.id" class="border-b border-divider/70 align-top">
            <td class="px-3 py-2 font-mono text-xs text-body">{{ maskKeyPrefix(key) }}</td>
            <td class="px-3 py-2 text-body">{{ key.name }}</td>
            <td class="px-3 py-2 text-muted">{{ formatDate(key.createdAtUtc) }}</td>
            <td class="px-3 py-2 text-muted">{{ formatDate(key.lastUsedAtUtc) }}</td>
            <td class="px-3 py-2">
              <button class="btn btn-danger btn-sm" type="button" @click="revokeKey(key.id)">
                {{ t('dashboard.apiKeysRevoke') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
