<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/layout/LanguageSwitcher.vue'

const { t } = useI18n()
</script>

<template>
  <footer class="bg-card border-t border-divider mt-auto">
    <div class="container flex flex-wrap items-center justify-between gap-2 py-2 text-sm text-muted sm:h-12 sm:flex-nowrap sm:py-0">
      <p class="text-xs sm:text-sm">{{ t('common.allRightsReserved', { year: new Date().getFullYear() }) }}</p>
      <div class="flex items-center gap-4">
        <a
          href="https://github.com/ludovit-scholtz/capitalism/security/policy"
          target="_blank"
          rel="noopener noreferrer"
          class="security-footer-link text-xs text-muted hover:text-primary transition-colors"
        >🔒 {{ t('common.securityLink') }}</a>
        <LanguageSwitcher />
      </div>
    </div>
  </footer>
</template>
