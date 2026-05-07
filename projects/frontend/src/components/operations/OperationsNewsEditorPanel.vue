<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import RichTextEditor from '@/components/admin/RichTextEditor.vue'
import { NEWS_EDITOR_LOCALES, pickGamesLocalization } from '@/lib/news'
import type { GamesEntry, GamesLocalization } from '@/types'

type EntryDraft = {
  entryId: string | null
  entryType: GamesEntry['entryType']
  status: GamesEntry['status']
  localizations: GamesLocalization[]
}

const props = defineProps<{
  draft: EntryDraft
  activeLocale: (typeof NEWS_EDITOR_LOCALES)[number]
  saving: boolean
  canSave: boolean
  selectedEntry: GamesEntry | null
}>()

const emit = defineEmits<{
  (event: 'update:activeLocale', value: (typeof NEWS_EDITOR_LOCALES)[number]): void
  (event: 'update:draft', value: EntryDraft): void
  (event: 'save'): void
  (event: 'cancel'): void
}>()

const { t } = useI18n()

const activeLocalization = computed(() =>
  props.draft.localizations.find((localization) => localization.locale === props.activeLocale),
)

const previewLocalization = computed(() => pickGamesLocalization(props.draft.localizations, props.activeLocale))

function patchDraft(patch: Partial<EntryDraft>) {
  emit('update:draft', { ...props.draft, ...patch })
}

function patchLocalization(patch: Partial<GamesLocalization>) {
  const localizations = props.draft.localizations.map((localization) =>
    localization.locale === props.activeLocale ? { ...localization, ...patch } : localization,
  )
  emit('update:draft', { ...props.draft, localizations })
}
</script>

<template>
  <section class="card ops-editor-panel">
    <div class="ops-editor-header">
      <div>
        <h3>{{ selectedEntry ? t('operations.news.editEntry') : t('operations.news.compose') }}</h3>
        <p>{{ t('operations.news.editorHelp') }}</p>
      </div>
      <button type="button" class="btn btn-ghost btn-sm" @click="emit('cancel')">
        {{ t('common.cancel') }}
      </button>
    </div>

    <div class="ops-editor-fields">
      <label class="form-label">
        {{ t('admin.entryType') }}
        <select
          class="form-select"
          :value="draft.entryType"
          @change="patchDraft({ entryType: ($event.target as HTMLSelectElement).value as GamesEntry['entryType'] })"
        >
          <option value="NEWS">{{ t('operations.news.filterNews') }}</option>
          <option value="CHANGELOG">{{ t('operations.news.filterChangelog') }}</option>
        </select>
      </label>
      <label class="form-label">
        {{ t('admin.entryStatus') }}
        <select
          class="form-select"
          :value="draft.status"
          @change="patchDraft({ status: ($event.target as HTMLSelectElement).value as GamesEntry['status'] })"
        >
          <option value="DRAFT">{{ t('admin.statusDraft') }}</option>
          <option value="PUBLISHED">{{ t('admin.statusPublished') }}</option>
        </select>
      </label>
    </div>

    <div class="ops-locale-tabs">
      <button
        v-for="editorLocale in NEWS_EDITOR_LOCALES"
        :key="editorLocale"
        type="button"
        class="ops-locale-tab"
        :class="{ active: activeLocale === editorLocale }"
        @click="emit('update:activeLocale', editorLocale)"
      >
        {{ editorLocale.toUpperCase() }}
      </button>
    </div>

    <label class="form-label">
      {{ t('admin.entryTitle') }}
      <input
        class="form-input"
        :value="activeLocalization?.title ?? ''"
        @input="patchLocalization({ title: ($event.target as HTMLInputElement).value ?? '' })"
      />
    </label>

    <label class="form-label">
      {{ t('admin.entrySummary') }}
      <textarea
        class="form-textarea"
        :value="activeLocalization?.summary ?? ''"
        @input="patchLocalization({ summary: ($event.target as HTMLTextAreaElement).value ?? '' })"
      ></textarea>
    </label>

    <label class="form-label">
      {{ t('admin.entryContent') }}
      <RichTextEditor
        :model-value="activeLocalization?.htmlContent ?? ''"
        @update:model-value="patchLocalization({ htmlContent: $event })"
      />
    </label>

    <div class="ops-editor-preview">
      <h4>{{ t('operations.news.previewTitle') }}</h4>
      <strong>{{ previewLocalization?.title || t('news.untitled') }}</strong>
      <p>{{ previewLocalization?.summary || t('operations.news.previewEmpty') }}</p>
    </div>

    <div class="ops-editor-actions">
      <button type="button" class="btn btn-primary" :disabled="!canSave || saving" @click="emit('save')">
        {{ saving ? t('common.saving') : t('admin.saveEntry') }}
      </button>
      <button type="button" class="btn btn-ghost" @click="emit('cancel')">{{ t('common.cancel') }}</button>
    </div>
  </section>
</template>

<style scoped>
.ops-editor-panel {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.ops-editor-header,
.ops-editor-fields,
.ops-editor-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.75rem;
}

.ops-editor-header p,
.ops-editor-preview p {
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}

.ops-editor-fields > label {
  flex: 1;
  min-width: 180px;
}

.ops-locale-tabs {
  display: flex;
  gap: 0.35rem;
}

.ops-locale-tab {
  padding: 0.45rem 0.75rem;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.ops-locale-tab.active {
  color: var(--color-text);
  background: rgba(255, 255, 255, 0.06);
}

.ops-editor-preview {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px dashed var(--color-border);
}
</style>
