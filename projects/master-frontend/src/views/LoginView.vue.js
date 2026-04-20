import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const router = useRouter();
const mode = ref('login');
const email = ref('');
const displayName = ref('');
const password = ref('');
const formError = ref('');
async function submit() {
    formError.value = '';
    try {
        if (mode.value === 'register') {
            await auth.register(email.value, displayName.value, password.value);
        }
        else {
            await auth.login(email.value, password.value);
        }
        await auth.fetchSubscription();
        await router.push('/');
    }
    catch (e) {
        formError.value = e instanceof Error ? e.message : 'Something went wrong. Please try again.';
    }
}
const __VLS_ctx = {
    ...{},
    ...{},
};
let __VLS_components;
let __VLS_intrinsics;
let __VLS_directives;
/** @type {__VLS_StyleScopedClasses['login-brand']} */ ;
/** @type {__VLS_StyleScopedClasses['field-group']} */ ;
/** @type {__VLS_StyleScopedClasses['field-group']} */ ;
/** @type {__VLS_StyleScopedClasses['field-group']} */ ;
/** @type {__VLS_StyleScopedClasses['submit-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['submit-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['back-link']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.main, __VLS_intrinsics.main)({
    ...{ class: "login-shell" },
});
/** @type {__VLS_StyleScopedClasses['login-shell']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "login-card" },
});
/** @type {__VLS_StyleScopedClasses['login-card']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "login-brand" },
});
/** @type {__VLS_StyleScopedClasses['login-brand']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "eyebrow" },
});
/** @type {__VLS_StyleScopedClasses['eyebrow']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.h1, __VLS_intrinsics.h1)({});
(__VLS_ctx.mode === 'login' ? 'Sign in' : 'Create account');
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "login-sub" },
});
/** @type {__VLS_StyleScopedClasses['login-sub']} */ ;
(__VLS_ctx.mode === 'login'
    ? 'Access your Pro subscription and server directory.'
    : 'Join the Capitalism Network to track your subscription.');
__VLS_asFunctionalElement1(__VLS_intrinsics.form, __VLS_intrinsics.form)({
    ...{ onSubmit: (__VLS_ctx.submit) },
    ...{ class: "login-form" },
});
/** @type {__VLS_StyleScopedClasses['login-form']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "field-group" },
});
/** @type {__VLS_StyleScopedClasses['field-group']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.label, __VLS_intrinsics.label)({
    for: "email",
});
__VLS_asFunctionalElement1(__VLS_intrinsics.input)({
    id: "email",
    type: "email",
    autocomplete: "email",
    placeholder: "you@example.com",
    required: true,
});
(__VLS_ctx.email);
if (__VLS_ctx.mode === 'register') {
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "field-group" },
    });
    /** @type {__VLS_StyleScopedClasses['field-group']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.label, __VLS_intrinsics.label)({
        for: "displayName",
    });
    __VLS_asFunctionalElement1(__VLS_intrinsics.input)({
        id: "displayName",
        value: (__VLS_ctx.displayName),
        type: "text",
        autocomplete: "name",
        placeholder: "Your name in the simulation",
        required: true,
    });
}
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "field-group" },
});
/** @type {__VLS_StyleScopedClasses['field-group']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.label, __VLS_intrinsics.label)({
    for: "password",
});
__VLS_asFunctionalElement1(__VLS_intrinsics.input)({
    id: "password",
    type: "password",
    autocomplete: "current-password",
    placeholder: "••••••••",
    required: true,
});
(__VLS_ctx.password);
if (__VLS_ctx.formError) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "form-error" },
        role: "alert",
    });
    /** @type {__VLS_StyleScopedClasses['form-error']} */ ;
    (__VLS_ctx.formError);
}
__VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
    ...{ class: "submit-btn" },
    type: "submit",
    disabled: (__VLS_ctx.auth.loading),
});
/** @type {__VLS_StyleScopedClasses['submit-btn']} */ ;
(__VLS_ctx.auth.loading ? 'Please wait…' : __VLS_ctx.mode === 'login' ? 'Sign in' : 'Create account');
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "toggle-mode" },
});
/** @type {__VLS_StyleScopedClasses['toggle-mode']} */ ;
if (__VLS_ctx.mode === 'login') {
    __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
        ...{ onClick: (...[$event]) => {
                if (!(__VLS_ctx.mode === 'login'))
                    return;
                __VLS_ctx.mode = 'register';
                // @ts-ignore
                [mode, mode, mode, mode, mode, mode, submit, email, displayName, password, formError, formError, auth, auth,];
            } },
        ...{ class: "link-btn" },
        type: "button",
    });
    /** @type {__VLS_StyleScopedClasses['link-btn']} */ ;
}
else {
    __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
        ...{ onClick: (...[$event]) => {
                if (!!(__VLS_ctx.mode === 'login'))
                    return;
                __VLS_ctx.mode = 'login';
                // @ts-ignore
                [mode,];
            } },
        ...{ class: "link-btn" },
        type: "button",
    });
    /** @type {__VLS_StyleScopedClasses['link-btn']} */ ;
}
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "back-link" },
});
/** @type {__VLS_StyleScopedClasses['back-link']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
    href: "/",
});
// @ts-ignore
[];
const __VLS_export = (await import('vue')).defineComponent({});
export default {};
