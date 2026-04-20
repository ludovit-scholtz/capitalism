import { createRouter, createWebHistory } from 'vue-router';
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
            path: '/gold-admin',
            name: 'gold-admin',
            component: () => import('@/views/GoldAdminView.vue'),
        },
    ],
    scrollBehavior() {
        return { top: 0 };
    },
});
export default router;
