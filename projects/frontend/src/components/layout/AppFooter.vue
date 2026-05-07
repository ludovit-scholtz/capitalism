<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/layout/LanguageSwitcher.vue'

const { t } = useI18n()
</script>

<template>
  <footer class="bg-card border-t border-divider mt-auto">
    <div class="container flex flex-wrap items-center justify-between gap-2 py-2 text-sm text-muted sm:h-12 sm:flex-nowrap sm:py-0">
      <p class="text-xs sm:text-sm">{{ t('common.allRightsReserved', { year: new Date().getFullYear() }) }}</p>
      <div class="w-full sm:w-auto flex justify-end">
        <LanguageSwitcher />
      </div>
    </div>
  </footer>
</template>
