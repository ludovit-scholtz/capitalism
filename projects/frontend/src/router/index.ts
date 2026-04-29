import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue') },
    { path: '/onboarding', name: 'onboarding', component: () => import('@/views/OnboardingView.vue') },
    { path: '/dashboard', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
    { path: '/news', name: 'news', component: () => import('@/views/NewsView.vue') },
    { path: '/admin', name: 'admin-dashboard', component: () => import('@/views/GameAdminDashboardView.vue') },
    { path: '/leaderboard', name: 'leaderboard', component: () => import('@/views/LeaderboardView.vue') },
    { path: '/encyclopedia', name: 'encyclopedia', component: () => import('@/views/ManufacturingEncyclopediaView.vue') },
    {
      path: '/encyclopedia/:topicSlug(onboarding-help|factory-layout-help|sales-shop-help|stock-exchange-help|resources-definition)',
      name: 'encyclopedia-topic',
      component: () => import('@/views/ManufacturingEncyclopediaView.vue'),
    },
    { path: '/exchange', name: 'exchange', component: () => import('@/views/GlobalExchangeView.vue') },
    { path: '/stocks', name: 'stocks', component: () => import('@/views/StockExchangeView.vue') },
    { path: '/forex', name: 'forex', component: () => import('@/views/ForexExchangeView.vue') },
    { path: '/encyclopedia/resources/:slug', name: 'encyclopedia-detail', component: () => import('@/views/ResourceDetailView.vue') },
    { path: '/buy-building/:companyId', name: 'buy-building', component: () => import('@/views/BuyBuildingView.vue') },
    { path: '/building/:id', name: 'building-detail', component: () => import('@/views/BuildingDetailView.vue') },
    { path: '/city/:id', name: 'city-map', component: () => import('@/views/CityMapView.vue') },
    { path: '/ledger/:companyId', name: 'ledger', component: () => import('@/views/LedgerView.vue') },
    { path: '/company/:companyId/settings', name: 'company-settings', component: () => import('@/views/CompanySettingsView.vue') },
    { path: '/banking', name: 'loan-marketplace', alias: '/loans', component: () => import('@/views/LoanMarketplaceView.vue') },
    { path: '/bank/:buildingId', name: 'bank-management', component: () => import('@/views/BankManagementView.vue') },
    { path: '/bank/:buildingId/request-loan', name: 'bank-loan-request', component: () => import('@/views/BankLoanRequestView.vue') },
    { path: '/personal-ledger', name: 'personal-ledger', component: () => import('@/views/PersonalLedgerView.vue') },
    {
      path: '/marketing-analytics',
      name: 'marketing-analytics',
      component: () => import('@/views/MarketingAnalyticsView.vue'),
    },
    {
      path: '/bank-statement/:companyId',
      name: 'bank-statement',
      component: () => import('@/views/BankStatementView.vue'),
    },
    {
      path: '/bank-statement',
      name: 'bank-statement-default',
      component: () => import('@/views/BankStatementView.vue'),
    },
  ],
  scrollBehavior() {
    return { top: 0 }
  },
})

export default router
