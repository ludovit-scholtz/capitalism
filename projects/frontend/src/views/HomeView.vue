<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { formatDiscoveryChipLabel } from '@/lib/discoveryLabels'
import { useDiscoveryAnalytics } from '@/composables/useDiscoveryAnalytics'
import { usePwa } from '@/composables/usePwa'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const { trackSearch, trackFilterChange, trackFilterClear } = useDiscoveryAnalytics()
const { isOffline } = usePwa()

const syncingFromRoute = ref(false)
// Prevents the route watcher from triggering syncFromRoute when the URL was just
// updated by the store watcher.  Avoids double-fetch and double-analytics.
const skipNextRouteSync = ref(false)

/** Guard against CSS injection: only allow valid 3- or 6-digit hex colors. */
function safeHexColor(value: string | null | undefined): string | null {
  if (!value) return null
  return /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/.test(value.trim()) ? value.trim() : null
}

function mapUrl(lat: number, lng: number): string {
  const safeLat = Math.min(90, Math.max(-90, lat))
  const safeLng = Math.min(180, Math.max(-180, lng))
  return `https://www.openstreetmap.org/export/embed.html?bbox=${safeLng - 0.02},${safeLat - 0.02},${safeLng + 0.02},${safeLat + 0.02}&layer=mapnik&marker=${safeLat},${safeLng}`
}

watch(
  () => route.query,
  async () => {
    if (skipNextRouteSync.value) {
      // The URL was just updated by the store watcher.  Skip this one invocation
      // to avoid a second syncFromRoute call (and double-fetch) for the same change.
      skipNextRouteSync.value = false
      return
    }
  },
  { immediate: true },
)

</script>

<template>
  <div class="home-view">
    <section
      class="subdomain-header"
    >
     Capitalism V
    </section>
  </div>
</template>

<style scoped>
.hero {
  background: linear-gradient(160deg, #0d0f14 0%, rgba(19, 127, 236, 0.12) 100%);
  border-bottom: 1px solid var(--color-border);
  padding: 4rem 0 3rem;
  margin-bottom: 0;
  position: relative;
}

.subdomain-header {
  --subdomain-color: var(--color-primary);
  --subdomain-accent: var(--color-primary);
  background: linear-gradient(160deg, #0d0f14 0%, rgba(19, 127, 236, 0.08) 100%);
  border-bottom: 3px solid var(--subdomain-color);
  padding: 2rem 0 1.75rem;
  margin-bottom: 0;
}

.subdomain-header-content {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 2rem;
}

.subdomain-hero-main {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  flex: 1;
  min-width: 0;
}

.subdomain-logo-wrap {
  flex-shrink: 0;
}

.subdomain-logo {
  height: 48px;
  width: auto;
  object-fit: contain;
  border-radius: var(--radius-sm);
}

.subdomain-hero-text {
  min-width: 0;
}

.subdomain-header h1 {
  font-size: 1.75rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  margin-bottom: 0.25rem;
  color: var(--color-text);
}

.subdomain-description {
  color: var(--color-text-secondary);
  font-size: 0.9375rem;
  margin-bottom: 0.5rem;
  max-width: 560px;
  line-height: 1.6;
}

.subdomain-curator-credit {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
}

.subdomain-curator-icon {
  color: var(--subdomain-color);
  font-size: 0.9375rem;
}

.subdomain-overview-snippet {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  line-height: 1.65;
  max-width: 540px;
  margin-bottom: 0.75rem;
}

.subdomain-links {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  margin-top: 0.5rem;
}

.subdomain-hub-link {
  font-size: 0.875rem;
}

.subdomain-banner-wrap {
  flex-shrink: 0;
}

.subdomain-banner {
  width: 220px;
  height: 130px;
  object-fit: cover;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
}

.main-site-link {
  flex-shrink: 0;
  color: var(--color-primary);
  font-size: 0.9375rem;
  font-weight: 600;
}

.main-site-link:hover {
  text-decoration: underline;
}

.hero-content {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.hero-video-wrapper {
  position: absolute;
  inset: 0;
  overflow: hidden;
  pointer-events: none;
}

.hero-text h1 {
  font-size: 2.75rem;
  font-weight: 700;
  line-height: 1.15;
  letter-spacing: -0.03em;
  margin-bottom: 0.875rem;
  color: var(--color-text);
}

.hero-accent {
  color: var(--color-primary);
}

.hero-text p {
  font-size: 1.0625rem;
  color: var(--color-text-secondary);
  max-width: 560px;
  margin-bottom: 1.5rem;
}

.hero-stat-row {
  display: flex;
  gap: 2rem;
}

.hero-stat {
  display: flex;
  flex-direction: column;
}

.hero-stat-num {
  font-size: 2rem;
  font-weight: 700;
  color: var(--color-primary);
  line-height: 1;
}

.hero-stat-label {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}

.catalog-section {
  padding-top: 2rem;
  padding-bottom: 3rem;
}

.catalog-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 340px;
  gap: 1.5rem;
}

.results-column {
  display: flex;
  flex-direction: column;
}

.results-summary {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.75rem;
}

/* Low-signal notice shown when only a few results are present */
.low-signal-notice {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.625rem 0.875rem;
  margin-bottom: 0.75rem;
  background: var(--color-surface-raised);
  color: var(--color-text-secondary);
  border: 1px solid var(--color-border);
  border-radius: 0.5rem;
  font-size: 0.8125rem;
}

.low-signal-icon {
  flex-shrink: 0;
  line-height: 1.4;
}

.events-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1rem;
}

/* Offline cached-content notice shown above the events grid */
.cached-results-notice {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  margin-bottom: 0.75rem;
  background: var(--color-surface-raised);
  color: var(--color-warning);
  border: 1px solid var(--color-border);
  border-radius: 0.5rem;
  font-size: 0.8125rem;
  font-weight: 500;
}

.results-state {
  padding: 2rem;
  display: grid;
  gap: 1rem;
}

.loading-state,
.error-state {
  grid-template-columns: auto 1fr;
  align-items: center;
}

.loading-spinner {
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 999px;
  border: 3px solid rgba(19, 127, 236, 0.15);
  border-top-color: var(--color-primary);
  animation: spin 0.9s linear infinite;
}

.state-icon {
  font-size: 1.75rem;
}

.state-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.map-panel {
  height: fit-content;
  padding: 1.125rem;
  position: sticky;
  top: 80px;
}

.map-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9375rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
  color: var(--color-text);
}

.map-title svg {
  width: 16px;
  height: 16px;
  color: var(--color-primary);
  flex-shrink: 0;
}

.map-caption {
  color: var(--color-text-secondary);
  font-size: 0.8125rem;
  margin-bottom: 0.75rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.map-panel iframe {
  width: 100%;
  height: 360px;
  border: 0;
  border-radius: var(--radius-sm);
}

.map-actions {
  margin-top: 0.75rem;
}

.map-link {
  font-size: 0.8125rem;
  color: var(--color-primary);
}

.map-empty,
.empty-state p,
.results-state p,
.results-state h2 {
  color: var(--color-text-secondary);
}

.empty-state {
  text-align: center;
  justify-items: center;
}

.empty-icon {
  font-size: 2.5rem;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 1024px) {
  .catalog-layout {
    grid-template-columns: 1fr;
  }

  .map-panel {
    position: static;
    order: -1;
  }

  .map-panel iframe {
    height: 240px;
  }
}

@media (max-width: 640px) {
  .subdomain-header-content {
    flex-direction: column;
    align-items: flex-start;
  }

  .subdomain-banner-wrap {
    display: none;
  }

  .subdomain-hero-main {
    flex-direction: column;
  }

  .subdomain-links {
    flex-direction: column;
    align-items: flex-start;
  }

  .hero-text h1 {
    font-size: 2rem;
  }

  .loading-state,
  .error-state {
    grid-template-columns: 1fr;
  }
}
.hero-video {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  z-index: -2;
}

.hero-video-uplayer {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(52, 55, 220, 0.2);
  z-index: -1;
}
</style>
