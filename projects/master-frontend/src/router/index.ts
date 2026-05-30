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
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('@/views/AuthCallbackView.vue'),
    },
    {
      path: '/game-servers',
      name: 'game-servers',
      component: () => import('@/views/GameServersView.vue'),
    },
    {
      path: '/account',
      name: 'account',
      component: () => import('@/views/AccountView.vue'),
    },
    {
      path: '/account/deposit',
      name: 'account-deposit',
      component: () => import('@/views/GoldDepositView.vue'),
    },
    {
      path: '/account/withdraw',
      name: 'account-withdraw',
      component: () => import('@/views/GoldWithdrawView.vue'),
    },
    {
      path: '/settings/game',
      name: 'game-settings',
      component: () => import('@/views/GameSettingsView.vue'),
    },
    {
      path: '/gold-admin',
      name: 'gold-admin',
      component: () => import('@/views/GoldAdminView.vue'),
    },
    {
      path: '/gold-transfers-admin',
      name: 'gold-transfers-admin',
      component: () => import('@/views/GoldTransfersAdminView.vue'),
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
      path: '/support/new',
      name: 'support-new',
      component: () => import('@/views/SupportNewTicketView.vue'),
    },
    {
      path: '/support/tickets',
      name: 'support-tickets',
      component: () => import('@/views/SupportTicketsView.vue'),
    },
    {
      path: '/support/tickets/:ticketId',
      name: 'support-ticket-detail',
      component: () => import('@/views/SupportTicketDetailView.vue'),
    },
    {
      path: '/support/admin',
      name: 'support-admin',
      component: () => import('@/views/SupportAdminView.vue'),
    },
    {
      path: '/game-admin',
      name: 'game-admin',
      component: () => import('@/views/GameAdminDashboardView.vue'),
    },
    {
      path: '/ranking',
      name: 'ranking-dashboard',
      component: () => import('@/views/RankingDashboardView.vue'),
    },
    {
      path: '/ranking/bounties',
      name: 'ranking-bounties',
      component: () => import('@/views/RankingBountiesView.vue'),
    },
    {
      path: '/ranking/bounties/history',
      name: 'ranking-history',
      component: () => import('@/views/RankingBountyHistoryView.vue'),
    },
    {
      path: '/ranking/admin',
      name: 'ranking-admin',
      component: () => import('@/views/RankingAdminView.vue'),
    },
    {
      path: '/api-keys',
      name: 'api-keys',
      component: () => import('@/views/ApiKeyManagementView.vue'),
    },
    {
      path: '/admin/security-board',
      name: 'security-board',
      component: () => import('@/views/SecurityBoardView.vue'),
    },
    {
      path: '/email/unsubscribe',
      name: 'email-unsubscribe',
      component: () => import('@/views/EmailUnsubscribeView.vue'),
    },
    {
      path: '/docs',
      name: 'docs',
      component: () => import('@/views/DocsView.vue'),
    },
    {
      path: '/terms',
      name: 'terms',
      component: () => import('@/views/LegalView.vue'),
    },
    {
      path: '/privacy',
      name: 'privacy',
      component: () => import('@/views/LegalView.vue'),
    },
  ],
  scrollBehavior(to) {
    if (to.hash) {
      return {
        el: to.hash,
        top: 96,
        behavior: 'smooth',
      }
    }

    return { top: 0 }
  },
})

export default router
