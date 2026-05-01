<script setup lang="ts">
interface NavLinkItem {
  label: string
  to: string
}

const props = withDefaults(
  defineProps<{
    kicker?: string
    title: string
    subtitle?: string
    variant?: 'default' | 'home' | 'servers' | 'ranking' | 'support' | 'referral' | 'gold' | 'admin'
    navLinks?: NavLinkItem[]
  }>(),
  {
    kicker: '',
    subtitle: '',
    variant: 'default',
    navLinks: () => [],
  },
)

const variantClass = `variant-${props.variant}`
</script>

<template>
  <section class="jumbotron" :class="variantClass">
    <div class="jumbotron-overlay"></div>
    <div class="container relative z-[1] py-8 lg:py-10">
      <p v-if="kicker" class="jumbotron-kicker">{{ kicker }}</p>
      <h1 class="jumbotron-title">{{ title }}</h1>
      <p v-if="subtitle" class="jumbotron-subtitle">{{ subtitle }}</p>

      <nav v-if="navLinks.length" class="jumbotron-nav" aria-label="View navigation">
        <RouterLink
          v-for="link in navLinks"
          :key="`${link.to}:${link.label}`"
          class="jumbotron-nav-link"
          :to="link.to"
        >
          {{ link.label }}
        </RouterLink>
      </nav>

      <slot name="actions"></slot>
    </div>
  </section>
</template>

<style scoped>
.jumbotron {
  position: relative;
  overflow: hidden;
  border-bottom: 1px solid var(--color-border);
  background: linear-gradient(140deg, rgba(19, 25, 36, 0.92), rgba(9, 12, 18, 0.88));
}

.jumbotron-overlay {
  position: absolute;
  inset: 0;
  opacity: 0.85;
  background:
    radial-gradient(circle at 12% 18%, rgba(0, 71, 255, 0.24), transparent 42%),
    radial-gradient(circle at 88% 12%, rgba(255, 128, 0, 0.18), transparent 36%);
}

.jumbotron-kicker {
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.13em;
  text-transform: uppercase;
  color: var(--color-text-secondary);
}

.jumbotron-title {
  margin-top: 0.45rem;
  font-size: clamp(1.8rem, 3vw, 2.7rem);
  font-weight: 700;
  line-height: 1.1;
  color: var(--color-text);
}

.jumbotron-subtitle {
  margin-top: 0.85rem;
  max-width: 70ch;
  color: var(--color-text-secondary);
  font-size: 0.96rem;
}

.jumbotron-nav {
  margin-top: 1.2rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
}

.jumbotron-nav-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  padding: 0.44rem 0.9rem;
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-text);
  background: rgba(0, 0, 0, 0.2);
  transition: all 0.15s ease;
}

.jumbotron-nav-link:hover,
.jumbotron-nav-link.router-link-active {
  border-color: color-mix(in srgb, var(--color-primary) 70%, var(--color-border));
  background: color-mix(in srgb, var(--color-primary-light) 65%, transparent);
  color: var(--color-text);
}

.variant-home .jumbotron-overlay {
  background:
    radial-gradient(circle at 14% 18%, rgba(0, 71, 255, 0.27), transparent 44%),
    radial-gradient(circle at 84% 16%, rgba(255, 170, 0, 0.18), transparent 38%);
}

.variant-servers .jumbotron-overlay {
  background:
    radial-gradient(circle at 18% 22%, rgba(34, 197, 94, 0.2), transparent 40%),
    radial-gradient(circle at 80% 12%, rgba(0, 71, 255, 0.2), transparent 40%);
}

.variant-ranking .jumbotron-overlay {
  background:
    radial-gradient(circle at 14% 22%, rgba(249, 115, 22, 0.18), transparent 42%),
    radial-gradient(circle at 84% 20%, rgba(250, 204, 21, 0.18), transparent 42%);
}

.variant-support .jumbotron-overlay {
  background:
    radial-gradient(circle at 16% 20%, rgba(14, 165, 233, 0.2), transparent 40%),
    radial-gradient(circle at 86% 20%, rgba(59, 130, 246, 0.2), transparent 38%);
}

.variant-referral .jumbotron-overlay {
  background:
    radial-gradient(circle at 14% 18%, rgba(139, 92, 246, 0.2), transparent 42%),
    radial-gradient(circle at 84% 20%, rgba(236, 72, 153, 0.18), transparent 40%);
}

.variant-gold .jumbotron-overlay {
  background:
    radial-gradient(circle at 16% 18%, rgba(250, 204, 21, 0.24), transparent 40%),
    radial-gradient(circle at 84% 18%, rgba(251, 191, 36, 0.16), transparent 38%);
}

.variant-admin .jumbotron-overlay {
  background:
    radial-gradient(circle at 14% 16%, rgba(239, 68, 68, 0.18), transparent 42%),
    radial-gradient(circle at 84% 18%, rgba(59, 130, 246, 0.18), transparent 40%);
}

[data-theme='light'] .jumbotron {
  background: linear-gradient(140deg, rgba(241, 245, 249, 0.92), rgba(255, 255, 255, 0.9));
}
</style>
