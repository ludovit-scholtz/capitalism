<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import EasyMDE from 'easymde'
import 'easymde/dist/easymde.min.css'

const props = defineProps<{
  modelValue: string
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const textareaRef = ref<HTMLTextAreaElement | null>(null)
let editor: EasyMDE | null = null

onMounted(() => {
  if (!textareaRef.value) return

  editor = new EasyMDE({
    element: textareaRef.value,
    autofocus: false,
    spellChecker: false,
    status: ['lines', 'words', 'cursor'],
    minHeight: '260px',
    previewClass: ['editor-preview', 'support-preview'],
    toolbar: [
      'bold',
      'italic',
      'heading',
      '|',
      'quote',
      'unordered-list',
      'ordered-list',
      '|',
      'link',
      {
        name: 'image-link',
        action: () => {
          if (!editor) return
          const imageUrl = window.prompt('Image URL (http or https):')
          if (!imageUrl) return
          const cm = editor.codemirror
          const selection = cm.getSelection() || 'screenshot'
          cm.replaceSelection(`![${selection}](${imageUrl})`)
        },
        className: 'fa fa-image',
        title: 'Insert image URL',
      },
      '|',
      'preview',
      'side-by-side',
      'fullscreen',
      '|',
      'guide',
    ],
    initialValue: props.modelValue,
  })

  editor.codemirror.on('change', () => {
    if (!editor) return
    emit('update:modelValue', editor.value())
  })

  editor.codemirror.setOption('readOnly', props.disabled ? 'nocursor' : false)
})

watch(
  () => props.modelValue,
  (value) => {
    if (!editor) return
    if (editor.value() !== value) {
      editor.value(value)
    }
  },
)

watch(
  () => props.disabled,
  (disabled) => {
    if (!editor) return
    editor.codemirror.setOption('readOnly', disabled ? 'nocursor' : false)
  },
)

onUnmounted(() => {
  editor?.toTextArea()
  editor = null
})
</script>

<template>
  <div class="ticket-editor-wrap">
    <textarea ref="textareaRef" />
  </div>
</template>

<style scoped>
.ticket-editor-wrap :deep(.EasyMDEContainer .CodeMirror) {
  border-radius: 8px;
  border: 1px solid #2f2f4a;
  background: #0f1020;
  color: #ececff;
}

.ticket-editor-wrap :deep(.EasyMDEContainer .editor-toolbar) {
  border: 1px solid #2f2f4a;
  border-bottom: none;
  border-radius: 8px 8px 0 0;
  background: #16172a;
}

.ticket-editor-wrap :deep(.EasyMDEContainer .editor-toolbar button) {
  color: #d8d8e6;
}

.ticket-editor-wrap :deep(.EasyMDEContainer .editor-toolbar button:hover),
.ticket-editor-wrap :deep(.EasyMDEContainer .editor-toolbar button.active) {
  background: #2b2f4f;
  border-color: #3b4f81;
}

.ticket-editor-wrap :deep(.EasyMDEContainer .CodeMirror-cursor) {
  border-left-color: #f2f2ff;
}
</style>
