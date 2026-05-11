<template>
  <div>
    <!-- Sanitized v-html: DOMPurify is imported and used in the same file -->
    <div class="content" v-html="safeHtml"></div>
  </div>
</template>

<script setup lang="ts">
import DOMPurify from 'dompurify'
import { computed } from 'vue'

const props = defineProps<{ rawHtml: string }>()
const safeHtml = computed(() => DOMPurify.sanitize(props.rawHtml))
</script>
