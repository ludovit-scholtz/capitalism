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
  <div class="editor-shell overflow-hidden rounded-md border border-divider bg-white/5">
    <div class="editor-toolbar flex flex-wrap gap-2 border-b border-divider bg-white/8 p-3">
      <button
        type="button"
        class="editor-btn rounded-full border border-divider bg-card px-3 py-1.5 text-[0.82rem] font-bold text-body transition-colors hover:bg-card-raised"
        :title="t('admin.editorBold')"
        @click="runCommand('bold')"
      >
        B
      </button>
      <button
        type="button"
        class="editor-btn rounded-full border border-divider bg-card px-3 py-1.5 text-[0.82rem] font-bold text-body transition-colors hover:bg-card-raised"
        :title="t('admin.editorItalic')"
        @click="runCommand('italic')"
      >
        I
      </button>
      <button
        type="button"
        class="editor-btn rounded-full border border-divider bg-card px-3 py-1.5 text-[0.82rem] font-bold text-body transition-colors hover:bg-card-raised"
        :title="t('admin.editorHeading')"
        @click="runCommand('formatBlock', 'h2')"
      >
        H2
      </button>
      <button
        type="button"
        class="editor-btn rounded-full border border-divider bg-card px-3 py-1.5 text-[0.82rem] font-bold text-body transition-colors hover:bg-card-raised"
        :title="t('admin.editorParagraph')"
        @click="runCommand('formatBlock', 'p')"
      >
        P
      </button>
      <button
        type="button"
        class="editor-btn rounded-full border border-divider bg-card px-3 py-1.5 text-[0.82rem] font-bold text-body transition-colors hover:bg-card-raised"
        :title="t('admin.editorList')"
        @click="runCommand('insertUnorderedList')"
      >
        • List
      </button>
      <button
        type="button"
        class="editor-btn rounded-full border border-divider bg-card px-3 py-1.5 text-[0.82rem] font-bold text-body transition-colors hover:bg-card-raised"
        :title="t('admin.editorLink')"
        @click="runCommand('createLink')"
      >
        Link
      </button>
    </div>
    <div
      ref="editor"
      class="editor-surface min-h-60 p-4 leading-[1.65] text-body outline-none"
      contenteditable="true"
      :data-placeholder="placeholder ?? t('admin.editorPlaceholder')"
      @input="emitEditorHtml"
    ></div>
  </div>
</template>

<style scoped>
.editor-surface:empty::before {
  content: attr(data-placeholder);
  color: var(--color-text-secondary);
}
</style>
