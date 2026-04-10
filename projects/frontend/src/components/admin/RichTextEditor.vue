<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  modelValue: string
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const { t } = useI18n()
const editor = ref<HTMLDivElement | null>(null)

function syncEditorHtml(nextValue: string) {
  if (!editor.value || editor.value.innerHTML === nextValue) {
    return
  }

  editor.value.innerHTML = nextValue
}

function emitEditorHtml() {
  emit('update:modelValue', editor.value?.innerHTML ?? '')
}

function runCommand(command: string, value?: string) {
  editor.value?.focus()

  if (command === 'createLink') {
    const url = window.prompt(t('admin.editorLinkPrompt'))
    if (!url) {
      return
    }

    document.execCommand(command, false, url)
    emitEditorHtml()
    return
  }

  document.execCommand(command, false, value ?? '')
  emitEditorHtml()
}

onMounted(() => {
  syncEditorHtml(props.modelValue)
})

watch(
  () => props.modelValue,
  (nextValue) => {
    syncEditorHtml(nextValue)
  },
)
</script>

<template>
  <div class="editor-shell">
    <div class="editor-toolbar">
      <button type="button" class="editor-btn" :title="t('admin.editorBold')" @click="runCommand('bold')">B</button>
      <button type="button" class="editor-btn" :title="t('admin.editorItalic')" @click="runCommand('italic')">I</button>
      <button type="button" class="editor-btn" :title="t('admin.editorHeading')" @click="runCommand('formatBlock', 'h2')">H2</button>
      <button type="button" class="editor-btn" :title="t('admin.editorParagraph')" @click="runCommand('formatBlock', 'p')">P</button>
      <button type="button" class="editor-btn" :title="t('admin.editorList')" @click="runCommand('insertUnorderedList')">• List</button>
      <button type="button" class="editor-btn" :title="t('admin.editorLink')" @click="runCommand('createLink')">Link</button>
    </div>
    <div
      ref="editor"
      class="editor-surface"
      contenteditable="true"
      :data-placeholder="placeholder ?? t('admin.editorPlaceholder')"
      @input="emitEditorHtml"
    ></div>
  </div>
</template>

<style scoped>
.editor-shell {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
  background: rgba(255, 255, 255, 0.02);
}

.editor-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 0.75rem;
  border-bottom: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.03);
}

.editor-btn {
  padding: 0.4rem 0.7rem;
  border-radius: 999px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 0.82rem;
  font-weight: 700;
}

.editor-btn:hover {
  background: var(--color-surface-raised);
}

.editor-surface {
  min-height: 15rem;
  padding: 1rem;
  color: var(--color-text);
  outline: none;
  line-height: 1.65;
}

.editor-surface:empty::before {
  content: attr(data-placeholder);
  color: var(--color-text-secondary);
}
</style>