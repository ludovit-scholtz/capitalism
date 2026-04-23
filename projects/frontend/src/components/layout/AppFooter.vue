<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/layout/LanguageSwitcher.vue'

const { t } = useI18n()
</script>

<template>
  <footer class="bg-card border-t border-divider mt-auto">
    <div class="container flex items-center justify-between h-12 text-sm text-muted">
      <p>{{ t('common.allRightsReserved', { year: new Date().getFullYear() }) }}</p>
      <LanguageSwitcher />
    </div>
  </footer>
</template>
