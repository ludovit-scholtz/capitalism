<template>
  <div>
    <!-- Unsanitized v-html: no DOMPurify usage in this file -->
    <div class="content" v-html="rawHtml"></div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{ rawHtml: string }>()
</script>
