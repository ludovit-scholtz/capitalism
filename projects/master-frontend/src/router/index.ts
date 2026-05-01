import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/HomeView.vue'),
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
    },
    {
      path: '/account',
      name: 'account',
      component: () => import('@/views/AccountView.vue'),
    },
    {
      path: '/gold-admin',
      name: 'gold-admin',
      component: () => import('@/views/GoldAdminView.vue'),
    },
    {
      path: '/referrals/setup',
      name: 'referral-setup',
      component: () => import('@/views/ReferralSetupView.vue'),
    },
    {
      path: '/referrals/become',
      name: 'referral-become',
      component: () => import('@/views/ReferralBecomeView.vue'),
    },
    {
      path: '/referrals/dashboard',
      name: 'referral-dashboard',
      component: () => import('@/views/ReferralDashboardView.vue'),
    },
    {
      path: '/support',
      name: 'support',
      component: () => import('@/views/SupportView.vue'),
    },
    {
      path: '/support/admin',
      name: 'support-admin',
      component: () => import('@/views/SupportAdminView.vue'),
    },
    {
      path: '/ranking',
      name: 'ranking-dashboard',
      component: () => import('@/views/RankingDashboardView.vue'),
    },
    {
      path: '/ranking/bounties',
      name: 'ranking-history',
      component: () => import('@/views/RankingBountyHistoryView.vue'),
    },
    {
      path: '/ranking/admin',
      name: 'ranking-admin',
      component: () => import('@/views/RankingAdminView.vue'),
    },
  ],
  scrollBehavior(to) {
    if (to.hash) {
      return {
        el: to.hash,
        top: 88,
        behavior: 'smooth',
      }
    }

    return { top: 0 }
  },
})

export default router
