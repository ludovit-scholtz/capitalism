import { onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
// Synchronously restore token from localStorage so child views see auth state immediately
auth.initFromStorage();
onMounted(() => {
    if (auth.isAuthenticated) {
        void auth.fetchProfile();
        void auth.fetchSubscription();
    }
});
const __VLS_ctx = {};
let __VLS_components;
let __VLS_intrinsics;
let __VLS_directives;
let __VLS_0;
/** @ts-ignore @type {typeof __VLS_components.RouterView} */
RouterView;
// @ts-ignore
const __VLS_1 = __VLS_asFunctionalComponent1(__VLS_0, new __VLS_0({}));
const __VLS_2 = __VLS_1({}, ...__VLS_functionalComponentArgsRest(__VLS_1));
var __VLS_5 = {};
var __VLS_3;
const __VLS_export = (await import('vue')).defineComponent({});
export default {};
