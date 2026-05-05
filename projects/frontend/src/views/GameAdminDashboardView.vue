<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'

import AdminDashboardContent from '@/components/admin/AdminDashboardContent.vue'
import { useAuthStore } from '@/stores/auth'
import { useGameAdminStore } from '@/stores/gameAdmin'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const adminStore = useGameAdminStore()

const canAccessDashboard = computed(() => adminStore.session?.canAccessAdminDashboard ?? false)

async function loadDashboard() {
  try {
    const session = await adminStore.fetchSession()
    if (!session.canAccessAdminDashboard) {
      return
    }
    await adminStore.fetchDashboard()
  } catch {
    // errors will surface in the child component
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.replace('/login')
    return
  }

  await loadDashboard()
})
</script>

<template>
  <div class="admin-view container">
    <div class="page-header admin-header">
      <div>
        <p class="admin-eyebrow">{{ t('admin.eyebrow') }}</p>
        <h1>{{ t('admin.title') }}</h1>
        <p>{{ t('admin.subtitle') }}</p>
      </div>
    </div>

    <div v-if="!canAccessDashboard && !adminStore.loadingSession" class="admin-locked card">
      <h2>{{ t('admin.accessDeniedTitle') }}</h2>
      <p>{{ t('admin.accessDeniedBody') }}</p>
    </div>

    <AdminDashboardContent v-else />
  </div>
</template>

<style scoped>
.admin-view {
  padding-top: 2rem;
  padding-bottom: 4rem;
}

.admin-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1.5rem;
}

.admin-eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.16em;
  font-size: 0.72rem;
  color: #ffc07a;
  margin-bottom: 0.35rem;
}

.admin-locked {
  padding: 1.5rem;
}
</style>
