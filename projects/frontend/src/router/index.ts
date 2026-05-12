import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue') },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/views/ForgotPasswordView.vue'),
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
    },
    { path: '/auth/callback', name: 'auth-callback', component: () => import('@/views/AuthCallbackView.vue') },
    { path: '/onboarding', name: 'onboarding', component: () => import('@/views/OnboardingView.vue') },
    { path: '/dashboard', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
    { path: '/news', name: 'news', component: () => import('@/views/NewsView.vue') },
    { path: '/admin', redirect: '/operations/statistics' },
    {
      path: '/leaderboard',
      name: 'leaderboard',
      alias: '/ranking',
      component: () => import('@/views/LeaderboardView.vue'),
    },
    { path: '/player/:id', name: 'player-profile', component: () => import('@/views/PlayerProfileView.vue') },
    { path: '/cities', name: 'cities', component: () => import('@/views/CitiesView.vue') },
    { path: '/map', name: 'world-map', component: () => import('@/views/WorldMapView.vue') },
    { path: '/buildings/market', name: 'building-market', component: () => import('@/views/BuildingMarketView.vue') },
    { path: '/encyclopedia', name: 'encyclopedia', component: () => import('@/views/ManufacturingEncyclopediaView.vue') },
    {
      path: '/encyclopedia/:topicSlug(onboarding-help|factory-layout-help|sales-shop-help|forex-trading-help|stock-exchange-help|resources-definition)',
      name: 'encyclopedia-topic',
      component: () => import('@/views/ManufacturingEncyclopediaView.vue'),
    },
    { path: '/exchange', name: 'exchange', component: () => import('@/views/GlobalExchangeView.vue') },
    { path: '/stocks', name: 'stocks', alias: '/stock-exchange', component: () => import('@/views/StockExchangeView.vue') },
    { path: '/forex', name: 'forex', component: () => import('@/views/ForexExchangeView.vue') },
    { path: '/encyclopedia/resources/:slug', name: 'encyclopedia-detail', component: () => import('@/views/ResourceDetailView.vue') },
    { path: '/buy-building/:companyId', name: 'buy-building', component: () => import('@/views/BuyBuildingView.vue') },
    { path: '/building/:id', name: 'building-detail', component: () => import('@/views/BuildingDetailView.vue') },
    { path: '/building/:id/sell', name: 'sell-building', component: () => import('@/views/SellBuildingView.vue') },
    { path: '/city/:id', name: 'city-map', component: () => import('@/views/CityMapView.vue') },
    { path: '/ledger/:companyId', name: 'ledger', alias: '/company/:companyId/ledger', component: () => import('@/views/LedgerView.vue') },
    { path: '/company/:companyId/settings', name: 'company-settings', component: () => import('@/views/CompanySettingsView.vue') },
    { path: '/banking', name: 'loan-marketplace', alias: '/loans', component: () => import('@/views/LoanMarketplaceView.vue') },
    { path: '/bank/:buildingId', name: 'bank-management', component: () => import('@/views/BankManagementView.vue') },
    { path: '/bank/:buildingId/request-loan', name: 'bank-loan-request', component: () => import('@/views/BankLoanRequestView.vue') },
    {
      path: '/personal-ledger',
      name: 'personal-ledger',
      alias: '/portfolio',
      component: () => import('@/views/PersonalLedgerView.vue'),
    },
    {
      path: '/market-intelligence',
      name: 'market-intelligence',
      component: () => import('@/views/MarketIntelligenceView.vue'),
    },
    {
      path: '/market',
      name: 'market-dashboard',
      component: () => import('@/views/MarketDashboardView.vue'),
    },
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
    {
      path: '/trade-routes',
      name: 'trade-routes',
      component: () => import('@/views/TradeRoutesView.vue'),
    },
    { path: '/tutorial', name: 'tutorial', component: () => import('@/views/TutorialView.vue') },
    { path: '/operations', redirect: '/operations/statistics' },
    {
      path: '/operations/statistics',
      component: () => import('@/views/OperationsDashboardView.vue'),
      children: [
        {
          path: '',
          name: 'operations-overview',
          component: () => import('@/views/OperationsOverviewView.vue'),
        },
        {
          path: 'money-flow',
          name: 'operations-money-flow',
          component: () => import('@/views/OperationsStatisticsView.vue'),
        },
        {
          path: 'product-analytics',
          name: 'operations-product-analytics',
          component: () => import('@/views/OperationsAnalyticsView.vue'),
        },
        {
          path: 'news',
          name: 'operations-news',
          component: () => import('@/views/OperationsNewsView.vue'),
        },
        {
          path: 'players',
          name: 'operations-players',
          component: () => import('@/views/OperationsPlayersView.vue'),
        },
        {
          path: 'players/:id',
          name: 'operations-player-detail',
          component: () => import('@/views/OperationsPlayerDetailView.vue'),
        },
      ],
    },
  ],
  scrollBehavior() {
    return { top: 0 }
  },
})

export default router
