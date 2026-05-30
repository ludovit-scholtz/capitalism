<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/layout/LanguageSwitcher.vue'
import { resolveMasterWebUrl } from '@/lib/runtimeGraphqlUrl'

const { t } = useI18n()

const masterPortalUrl = resolveMasterWebUrl(import.meta.env.VITE_MASTER_WEB_URL)
const termsUrl = `${masterPortalUrl}/terms`
const privacyUrl = `${masterPortalUrl}/privacy`
</script>

<template>
  <footer class="bg-card border-t border-divider mt-auto">
    <div class="container flex flex-wrap items-center justify-between gap-2 py-2 text-sm text-muted sm:h-12 sm:flex-nowrap sm:py-0">
      <p class="text-xs sm:text-sm">{{ t('common.allRightsReserved', { year: new Date().getFullYear() }) }}</p>
      <div class="flex w-full flex-wrap items-center justify-end gap-x-4 gap-y-1 sm:w-auto sm:flex-nowrap sm:gap-4">
        <a :href="termsUrl" target="_blank" rel="noopener noreferrer" class="hover:text-body">
          {{ t('legal.terms') }}
        </a>
        <a :href="privacyUrl" target="_blank" rel="noopener noreferrer" class="hover:text-body">
          {{ t('legal.privacy') }}
        </a>
        <LanguageSwitcher />
      </div>
    </div>
  </footer>
</template>
