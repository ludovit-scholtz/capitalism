<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/layout/LanguageSwitcher.vue'

const { t } = useI18n()
</script>

<template>
  <footer class="mt-auto border-t border-divider bg-card">
    <div class="container flex h-12 items-center justify-between text-sm text-muted">
      <p>{{ t('common.allRightsReserved', { year: new Date().getFullYear() }) }}</p>
      <LanguageSwitcher />
    </div>
  </footer>
</template>
