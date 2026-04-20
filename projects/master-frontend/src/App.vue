<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

// Synchronously restore token from localStorage so child views see auth state immediately
auth.initFromStorage()

onMounted(() => {
  if (auth.isAuthenticated) {
    void auth.fetchProfile()
    void auth.fetchSubscription()
  }
})
</script>

<template>
  <RouterView />
</template>

<style scoped></style>