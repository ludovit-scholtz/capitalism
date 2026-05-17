<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useNotificationsStore } from '@/stores/notifications'
import { resolveNotificationCopy } from '@/lib/notificationText'
import type { PlayerNotificationItem } from '@/types'

type NotificationListItem = PlayerNotificationItem & {
  displayTitle: string
  displayMessage: string
}

const { t, te } = useI18n()
const router = useRouter()
const notificationsStore = useNotificationsStore()
const { inbox, loading } = storeToRefs(notificationsStore)
const loadError = ref<string | null>(null)

function groupNotifications(items: NotificationListItem[]) {
  const groups = new Map<string, NotificationListItem[]>()
  for (const item of items) {
    const day = new Date(item.createdAtUtc).toLocaleDateString()
    const existing = groups.get(day) ?? []
    existing.push(item)
    groups.set(day, existing)
  }
  return Array.from(groups.entries()).map(([day, dayItems]) => ({ day, items: dayItems }))
}

const groupedNotifications = computed(() => {
  const items = (inbox.value?.items ?? []).map((item) => {
    const localized = resolveNotificationCopy(item, t, te)
    return {
      ...item,
      displayTitle: localized.title,
      displayMessage: localized.message,
    }
  })
  return groupNotifications(items)
})

function formatNotificationAge(createdAtUtc: string) {
  const createdAt = new Date(createdAtUtc).getTime()
  const diffMs = Math.max(0, Date.now() - createdAt)
  const diffMinutes = Math.floor(diffMs / 60000)
  if (diffMinutes < 1) return t('notifications.justNow')
  if (diffMinutes < 60) return t('notifications.minutesAgo', { count: diffMinutes })
  const diffHours = Math.floor(diffMinutes / 60)
  if (diffHours < 24) return t('notifications.hoursAgo', { count: diffHours })
  const diffDays = Math.floor(diffHours / 24)
  return t('notifications.daysAgo', { count: diffDays })
}

function getNotificationIcon(type: string) {
  if (type === 'SHIPMENT_ARRIVED') return { symbol: '✅', className: 'notification-icon-shipment' }
  if (type === 'LOGISTICS_MARGIN_EROSION') return { symbol: '⚠️', className: 'notification-icon-margin' }
  return { symbol: '🔔', className: 'notification-icon-default' }
}

function getNotificationSeverityClass(severity: string) {
  if (severity === 'CRITICAL') return 'notification-severity-critical'
  if (severity === 'WARNING') return 'notification-severity-warning'
  return 'notification-severity-info'
}

async function loadNotifications() {
  loadError.value = null
  try {
    await notificationsStore.fetchInbox(50)
    const unreadIds = inbox.value?.items.filter((item) => !item.isRead).map((item) => item.id) ?? []
    if (unreadIds.length > 0) {
      await notificationsStore.markRead(unreadIds)
    }
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : t('common.unknownError')
  }
}

async function markAllRead() {
  await notificationsStore.markAllRead()
}

async function handleNotificationClick(item: PlayerNotificationItem) {
  if (!item.isRead) {
    await notificationsStore.markRead([item.id])
  }

  if (item.buildingId) {
    await router.push(`/building/${item.buildingId}`)
  } else if (item.type === 'SHIPMENT_ARRIVED' || item.type === 'LOGISTICS_MARGIN_EROSION') {
    await router.push('/trade-routes')
  } else if (item.type === 'LOAN_REPAYMENT_DUE_SOON' || item.type === 'LOAN_PAYMENT_DUE' || item.type === 'LOAN_DEFAULT' || item.loanId) {
    await router.push('/banking')
  } else if (item.type === 'BANK_ACCOUNT_LOW_BALANCE' || item.bankAccountId) {
    await router.push('/bank-statement')
  } else if (item.type === 'TAKEOVER_ALERT' || item.companyId) {
    await router.push('/stocks')
  } else {
    await router.push('/dashboard')
  }
}

onMounted(() => {
  void loadNotifications()
})
</script>

<template>
  <div class="notifications-page container mx-auto px-4 pt-8 pb-16">
    <header class="notifications-header">
      <h1>{{ t('notifications.title') }}</h1>
      <p>{{ t('notifications.subtitle') }}</p>
      <button class="btn btn-ghost btn-sm" :disabled="(inbox?.items.length ?? 0) === 0" @click="markAllRead">
        {{ t('notifications.markAllRead') }}
      </button>
    </header>

    <div v-if="loading" class="notification-page-state">{{ t('common.loading') }}</div>
    <div v-else-if="loadError" class="notification-page-state notification-page-state-error">
      <p>{{ loadError }}</p>
      <button class="btn btn-secondary btn-sm" @click="loadNotifications">{{ t('common.tryAgain') }}</button>
    </div>
    <div v-else-if="!inbox || inbox.items.length === 0" class="notification-page-state">{{ t('notifications.empty') }}</div>
    <div v-else class="notification-groups">
      <section v-for="group in groupedNotifications" :key="group.day" class="notification-group">
        <h2 class="notification-group-day">{{ group.day }}</h2>
        <ul class="notification-list">
          <li v-for="item in group.items" :key="item.id" class="notification-item" :class="{ 'notification-item-unread': !item.isRead }">
            <button class="notification-item-btn" @click="handleNotificationClick(item)">
              <div class="notification-item-top">
                <span class="notification-item-icon" :class="[getNotificationIcon(item.type).className, getNotificationSeverityClass(item.severity)]">
                  {{ getNotificationIcon(item.type).symbol }}
                </span>
                <span class="notification-item-title">{{ item.displayTitle }}</span>
              </div>
              <span class="notification-item-message">{{ item.displayMessage }}</span>
              <span class="notification-item-time">{{ formatNotificationAge(item.createdAtUtc) }}</span>
            </button>
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>

<style scoped>
.notifications-page {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.notifications-header {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.notifications-header h1 {
  margin: 0;
}

.notifications-header p {
  margin: 0;
  color: var(--color-text-secondary);
}

.notification-page-state {
  border: 1px dashed var(--color-divider);
  border-radius: 0.75rem;
  padding: 1rem;
  color: var(--color-text-secondary);
  text-align: center;
}

.notification-page-state-error {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  align-items: center;
}

.notification-groups {
  display: grid;
  gap: 0.7rem;
}

.notification-group-day {
  font-size: 0.72rem;
  font-weight: 700;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.03em;
  margin: 0;
}

.notification-list {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
}

.notification-item {
  border: 1px solid var(--color-divider);
  border-radius: 0.75rem;
  overflow: hidden;
}

.notification-item-unread {
  border-color: color-mix(in srgb, var(--color-primary) 40%, var(--color-divider));
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
}

.notification-item-btn {
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  padding: 0.7rem 0.8rem;
  display: grid;
  gap: 0.18rem;
}

.notification-item-top {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.notification-item-icon {
  font-size: 0.95rem;
  line-height: 1;
}

.notification-icon-shipment {
  color: #16a34a;
}

.notification-icon-margin {
  color: #f59e0b;
}

.notification-icon-default {
  color: var(--color-text-secondary);
}

.notification-severity-critical {
  color: #ef4444;
}

.notification-severity-warning {
  color: #f59e0b;
}

.notification-severity-info {
  color: #3b82f6;
}

.notification-item-title {
  font-size: 0.86rem;
  font-weight: 700;
  color: var(--color-text);
}

.notification-item-message {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  line-height: 1.35;
}

.notification-item-time {
  font-size: 0.7rem;
  color: var(--color-text-muted);
}
</style>
