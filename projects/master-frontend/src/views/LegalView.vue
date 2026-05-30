<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { fetchLegalDocuments, type LegalDocument, type LegalDocumentKind } from '@/lib/masterApi'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const documents = ref<LegalDocument[]>([])
const loading = ref(true)
const errorMessage = ref('')

const activeKind = computed<LegalDocumentKind>(() =>
  route.name === 'privacy' ? 'PRIVACY' : 'TERMS',
)

const activeDocument = computed(() =>
  documents.value.find((document) => document.kind === activeKind.value),
)

const navItems = computed(() => [
  { label: t('nav.home'), to: '/' },
  { label: t('legal.terms'), to: '/terms' },
  { label: t('legal.privacy'), to: '/privacy' },
])

async function loadDocuments() {
  loading.value = true
  errorMessage.value = ''

  try {
    documents.value = await fetchLegalDocuments(locale.value)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('legal.loadError')
  } finally {
    loading.value = false
  }
}

function selectKind(kind: LegalDocumentKind) {
  void router.push(kind === 'PRIVACY' ? '/privacy' : '/terms')
}

onMounted(loadDocuments)
watch(locale, loadDocuments)
</script>

<template>
  <div>
    <ViewJumbotron
      :kicker="t('legal.kicker')"
      :title="t('legal.title')"
      :subtitle="t('legal.subtitle')"
      variant="default"
    />
    <ViewSubnav :items="navItems" aria-label="Legal navigation" />

    <main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
      <div class="grid gap-6 lg:grid-cols-[240px_minmax(0,1fr)]">
        <!-- Sidebar navigation -->
        <nav
          class="sticky top-20 self-start rounded-xl border border-divider bg-card p-4"
          aria-label="Legal documents"
        >
          <p class="mb-3 text-xs font-semibold uppercase tracking-[0.12em] text-muted">
            {{ t('legal.documentsLabel') }}
          </p>
          <ul class="flex flex-col gap-1">
            <li>
              <button
                type="button"
                class="w-full rounded-lg px-3 py-2 text-left text-sm transition-colors"
                :class="
                  activeKind === 'TERMS'
                    ? 'bg-accent/15 font-semibold text-accent'
                    : 'text-body hover:bg-overlay/40'
                "
                @click="selectKind('TERMS')"
              >
                {{ t('legal.terms') }}
              </button>
            </li>
            <li>
              <button
                type="button"
                class="w-full rounded-lg px-3 py-2 text-left text-sm transition-colors"
                :class="
                  activeKind === 'PRIVACY'
                    ? 'bg-accent/15 font-semibold text-accent'
                    : 'text-body hover:bg-overlay/40'
                "
                @click="selectKind('PRIVACY')"
              >
                {{ t('legal.privacy') }}
              </button>
            </li>
          </ul>
          <p class="mt-4 text-xs leading-relaxed text-muted">
            {{ t('legal.tokenizationNote') }}
            <a
              href="https://asa.gold/terms/latest"
              target="_blank"
              rel="noopener noreferrer"
              class="text-accent underline"
            >
              asa.gold/terms/latest
            </a>
          </p>
        </nav>

        <!-- Content area -->
        <article class="card prose-doc p-6 lg:p-8">
          <p v-if="loading" class="text-sm text-muted">{{ t('legal.loading') }}</p>
          <p v-else-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
          <template v-else-if="activeDocument">
            <h1 class="mb-1 text-2xl font-bold text-body">{{ activeDocument.title }}</h1>
            <p class="mb-4 text-xs text-muted">
              {{ t('legal.version') }} {{ activeDocument.version }} ·
              {{ t('legal.effectiveDate') }} {{ activeDocument.effectiveDate }}
            </p>
            <p class="legal-intro">{{ activeDocument.intro }}</p>

            <section
              v-for="(section, index) in activeDocument.sections"
              :key="index"
              class="doc-section"
            >
              <h2>{{ section.heading }}</h2>
              <p v-for="(paragraph, pIndex) in section.paragraphs" :key="pIndex">
                {{ paragraph }}
              </p>
            </section>
          </template>
          <p v-else class="text-sm text-muted">{{ t('legal.loadError') }}</p>
        </article>
      </div>
    </main>
  </div>
</template>

<style scoped>
.legal-intro {
  font-size: 0.9375rem;
  line-height: 1.7;
  color: var(--color-muted);
}

.doc-section {
  margin-top: 1.75rem;
}

.doc-section h2 {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--color-body);
  margin-bottom: 0.625rem;
}

.doc-section p {
  font-size: 0.9375rem;
  line-height: 1.7;
  color: var(--color-muted);
  margin-bottom: 0.625rem;
}
</style>
