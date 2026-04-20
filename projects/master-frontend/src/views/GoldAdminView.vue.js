import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { adjustGoldTokenBalance, fetchGoldTokenBalances, fetchGoldTokenTransactions, } from '@/lib/masterApi';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const router = useRouter();
// ── State ──────────────────────────────────────────────────────────────────
const balances = ref([]);
const transactions = ref([]);
const balancesLoading = ref(false);
const txLoading = ref(false);
const balancesError = ref('');
const txError = ref('');
const searchQuery = ref('');
const selectedEmail = ref(null);
const adjustAmount = ref('');
const adjustNote = ref('');
const adjustLoading = ref(false);
const adjustError = ref('');
const adjustSuccess = ref('');
const txFilterEmail = ref('');
// ── Computed ───────────────────────────────────────────────────────────────
const filteredBalances = computed(() => {
    if (!searchQuery.value.trim())
        return balances.value;
    const q = searchQuery.value.trim().toLowerCase();
    return balances.value.filter((b) => b.email.toLowerCase().includes(q) || b.displayName.toLowerCase().includes(q));
});
const adjustAmountNumber = computed(() => {
    const n = parseFloat(adjustAmount.value);
    return isNaN(n) ? null : n;
});
const isDeduction = computed(() => adjustAmountNumber.value !== null && adjustAmountNumber.value < 0);
const selectedBalance = computed(() => selectedEmail.value ? (balances.value.find((b) => b.email === selectedEmail.value) ?? null) : null);
// ── Data loading ───────────────────────────────────────────────────────────
async function loadBalances() {
    if (!auth.token)
        return;
    balancesLoading.value = true;
    balancesError.value = '';
    try {
        balances.value = await fetchGoldTokenBalances(auth.token);
    }
    catch (e) {
        balancesError.value = e instanceof Error ? e.message : 'Failed to load balances.';
    }
    finally {
        balancesLoading.value = false;
    }
}
async function loadTransactions(email) {
    if (!auth.token)
        return;
    txLoading.value = true;
    txError.value = '';
    try {
        transactions.value = await fetchGoldTokenTransactions(auth.token, email, 50);
    }
    catch (e) {
        txError.value = e instanceof Error ? e.message : 'Failed to load transaction history.';
    }
    finally {
        txLoading.value = false;
    }
}
// ── Actions ────────────────────────────────────────────────────────────────
function selectUser(email) {
    selectedEmail.value = email;
    adjustAmount.value = '';
    adjustNote.value = '';
    adjustError.value = '';
    adjustSuccess.value = '';
    void loadTransactions(email);
}
async function handleAdjust() {
    if (!auth.token || !selectedEmail.value)
        return;
    const amount = adjustAmountNumber.value;
    if (amount === null || amount === 0) {
        adjustError.value = 'Amount must be a non-zero number.';
        return;
    }
    adjustLoading.value = true;
    adjustError.value = '';
    adjustSuccess.value = '';
    try {
        const updated = await adjustGoldTokenBalance(auth.token, selectedEmail.value, amount, adjustNote.value.trim() || undefined);
        // Update the local balance display
        const idx = balances.value.findIndex((b) => b.email === selectedEmail.value);
        if (idx !== -1) {
            const existing = balances.value[idx];
            if (existing) {
                balances.value[idx] = { ...existing, goldTokenBalance: updated.goldTokenBalance };
            }
        }
        adjustSuccess.value = `✓ Balance updated to ${formatGold(updated.goldTokenBalance)} g`;
        adjustAmount.value = '';
        adjustNote.value = '';
        // Refresh the transaction log for this user
        await loadTransactions(selectedEmail.value);
    }
    catch (e) {
        adjustError.value = e instanceof Error ? e.message : 'Adjustment failed.';
    }
    finally {
        adjustLoading.value = false;
    }
}
async function handleTxFilter() {
    await loadTransactions(txFilterEmail.value.trim() || undefined);
}
// ── Formatting ─────────────────────────────────────────────────────────────
function formatGold(value) {
    return value.toFixed(4);
}
function formatDateTime(iso) {
    return new Intl.DateTimeFormat(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    }).format(new Date(iso));
}
function formatTxAmount(amount) {
    return amount > 0 ? `+${formatGold(amount)}` : formatGold(amount);
}
// ── Lifecycle ──────────────────────────────────────────────────────────────
onMounted(async () => {
    if (!auth.isAuthenticated) {
        void router.push('/login');
        return;
    }
    await Promise.all([loadBalances(), loadTransactions()]);
});
const __VLS_ctx = {
    ...{},
    ...{},
};
let __VLS_components;
let __VLS_intrinsics;
let __VLS_directives;
/** @type {__VLS_StyleScopedClasses['nav-back-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['search-input']} */ ;
/** @type {__VLS_StyleScopedClasses['refresh-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['refresh-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['balance-table']} */ ;
/** @type {__VLS_StyleScopedClasses['tx-table']} */ ;
/** @type {__VLS_StyleScopedClasses['balance-table']} */ ;
/** @type {__VLS_StyleScopedClasses['tx-table']} */ ;
/** @type {__VLS_StyleScopedClasses['balance-table']} */ ;
/** @type {__VLS_StyleScopedClasses['balance-table']} */ ;
/** @type {__VLS_StyleScopedClasses['balance-table']} */ ;
/** @type {__VLS_StyleScopedClasses['select-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['current-balance-label']} */ ;
/** @type {__VLS_StyleScopedClasses['form-input']} */ ;
/** @type {__VLS_StyleScopedClasses['adjust-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['adjust-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['adjust-btn--deduct']} */ ;
/** @type {__VLS_StyleScopedClasses['cancel-btn']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "gold-admin-shell" },
});
/** @type {__VLS_StyleScopedClasses['gold-admin-shell']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.header, __VLS_intrinsics.header)({
    ...{ class: "gold-admin-header" },
});
/** @type {__VLS_StyleScopedClasses['gold-admin-header']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "gold-admin-header-inner" },
});
/** @type {__VLS_StyleScopedClasses['gold-admin-header-inner']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "section-kicker" },
});
/** @type {__VLS_StyleScopedClasses['section-kicker']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.h1, __VLS_intrinsics.h1)({});
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "gold-admin-subtitle" },
});
/** @type {__VLS_StyleScopedClasses['gold-admin-subtitle']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.nav, __VLS_intrinsics.nav)({
    ...{ class: "gold-admin-nav" },
});
/** @type {__VLS_StyleScopedClasses['gold-admin-nav']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
    href: "/",
    ...{ class: "nav-back-btn" },
});
/** @type {__VLS_StyleScopedClasses['nav-back-btn']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.main, __VLS_intrinsics.main)({
    ...{ class: "gold-admin-main" },
});
/** @type {__VLS_StyleScopedClasses['gold-admin-main']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
    ...{ class: "gold-section" },
    'aria-labelledby': "balances-heading",
});
/** @type {__VLS_StyleScopedClasses['gold-section']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "gold-section-header" },
});
/** @type {__VLS_StyleScopedClasses['gold-section-header']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.h2, __VLS_intrinsics.h2)({
    id: "balances-heading",
});
__VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
    ...{ onClick: (__VLS_ctx.loadBalances) },
    ...{ class: "refresh-btn" },
    type: "button",
    disabled: (__VLS_ctx.balancesLoading),
});
/** @type {__VLS_StyleScopedClasses['refresh-btn']} */ ;
(__VLS_ctx.balancesLoading ? 'Loading…' : 'Refresh');
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "search-bar" },
});
/** @type {__VLS_StyleScopedClasses['search-bar']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.input)({
    type: "search",
    placeholder: "Search by email or name…",
    ...{ class: "search-input" },
    'aria-label': "Search players",
});
(__VLS_ctx.searchQuery);
/** @type {__VLS_StyleScopedClasses['search-input']} */ ;
if (__VLS_ctx.balancesError) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-error" },
        role: "alert",
    });
    /** @type {__VLS_StyleScopedClasses['state-error']} */ ;
    (__VLS_ctx.balancesError);
}
else if (__VLS_ctx.balancesLoading && __VLS_ctx.balances.length === 0) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
}
else if (__VLS_ctx.filteredBalances.length === 0) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
}
else {
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "balance-table-wrap" },
    });
    /** @type {__VLS_StyleScopedClasses['balance-table-wrap']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.table, __VLS_intrinsics.table)({
        ...{ class: "balance-table" },
        'aria-label': "Player gold balances",
    });
    /** @type {__VLS_StyleScopedClasses['balance-table']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.thead, __VLS_intrinsics.thead)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.tr, __VLS_intrinsics.tr)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({
        ...{ class: "col-balance" },
    });
    /** @type {__VLS_StyleScopedClasses['col-balance']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.tbody, __VLS_intrinsics.tbody)({});
    for (const [row] of __VLS_vFor((__VLS_ctx.filteredBalances))) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.tr, __VLS_intrinsics.tr)({
            ...{ onClick: (...[$event]) => {
                    if (!!(__VLS_ctx.balancesError))
                        return;
                    if (!!(__VLS_ctx.balancesLoading && __VLS_ctx.balances.length === 0))
                        return;
                    if (!!(__VLS_ctx.filteredBalances.length === 0))
                        return;
                    __VLS_ctx.selectUser(row.email);
                    // @ts-ignore
                    [loadBalances, balancesLoading, balancesLoading, balancesLoading, searchQuery, balancesError, balancesError, balances, filteredBalances, filteredBalances, selectUser,];
                } },
            key: (row.playerId),
            ...{ class: ({ 'row-selected': __VLS_ctx.selectedEmail === row.email }) },
        });
        /** @type {__VLS_StyleScopedClasses['row-selected']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-email" },
        });
        /** @type {__VLS_StyleScopedClasses['col-email']} */ ;
        (row.email);
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({});
        (row.displayName);
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-balance" },
        });
        /** @type {__VLS_StyleScopedClasses['col-balance']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
            ...{ class: "gold-badge" },
        });
        /** @type {__VLS_StyleScopedClasses['gold-badge']} */ ;
        (__VLS_ctx.formatGold(row.goldTokenBalance));
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
            ...{ onClick: (...[$event]) => {
                    if (!!(__VLS_ctx.balancesError))
                        return;
                    if (!!(__VLS_ctx.balancesLoading && __VLS_ctx.balances.length === 0))
                        return;
                    if (!!(__VLS_ctx.filteredBalances.length === 0))
                        return;
                    __VLS_ctx.selectUser(row.email);
                    // @ts-ignore
                    [selectUser, selectedEmail, formatGold,];
                } },
            ...{ class: "select-btn" },
            type: "button",
        });
        /** @type {__VLS_StyleScopedClasses['select-btn']} */ ;
        // @ts-ignore
        [];
    }
}
if (__VLS_ctx.selectedEmail) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
        ...{ class: "gold-section adjust-panel" },
        'aria-labelledby': "adjust-heading",
    });
    /** @type {__VLS_StyleScopedClasses['gold-section']} */ ;
    /** @type {__VLS_StyleScopedClasses['adjust-panel']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.h2, __VLS_intrinsics.h2)({
        id: "adjust-heading",
    });
    __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
        ...{ class: "adjust-target-email" },
    });
    /** @type {__VLS_StyleScopedClasses['adjust-target-email']} */ ;
    (__VLS_ctx.selectedEmail);
    if (__VLS_ctx.selectedBalance) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "current-balance-label" },
        });
        /** @type {__VLS_StyleScopedClasses['current-balance-label']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.strong, __VLS_intrinsics.strong)({});
        (__VLS_ctx.formatGold(__VLS_ctx.selectedBalance.goldTokenBalance));
    }
    __VLS_asFunctionalElement1(__VLS_intrinsics.form, __VLS_intrinsics.form)({
        ...{ onSubmit: (__VLS_ctx.handleAdjust) },
        ...{ class: "adjust-form" },
    });
    /** @type {__VLS_StyleScopedClasses['adjust-form']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "form-row" },
    });
    /** @type {__VLS_StyleScopedClasses['form-row']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.label, __VLS_intrinsics.label)({
        for: "adjust-amount",
        ...{ class: "form-label" },
    });
    /** @type {__VLS_StyleScopedClasses['form-label']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.input)({
        id: "adjust-amount",
        type: "number",
        step: "0.0001",
        placeholder: "e.g. 10.5 or -5.0",
        ...{ class: "form-input" },
        ...{ class: ({ 'input-deduction': __VLS_ctx.isDeduction }) },
        required: true,
    });
    (__VLS_ctx.adjustAmount);
    /** @type {__VLS_StyleScopedClasses['form-input']} */ ;
    /** @type {__VLS_StyleScopedClasses['input-deduction']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "form-row" },
    });
    /** @type {__VLS_StyleScopedClasses['form-row']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.label, __VLS_intrinsics.label)({
        for: "adjust-note",
        ...{ class: "form-label" },
    });
    /** @type {__VLS_StyleScopedClasses['form-label']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.input)({
        id: "adjust-note",
        value: (__VLS_ctx.adjustNote),
        type: "text",
        maxlength: "500",
        placeholder: "Reason for adjustment…",
        ...{ class: "form-input" },
    });
    /** @type {__VLS_StyleScopedClasses['form-input']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "form-actions" },
    });
    /** @type {__VLS_StyleScopedClasses['form-actions']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
        type: "submit",
        ...{ class: "adjust-btn" },
        ...{ class: ({ 'adjust-btn--deduct': __VLS_ctx.isDeduction }) },
        disabled: (__VLS_ctx.adjustLoading || !__VLS_ctx.adjustAmount),
    });
    /** @type {__VLS_StyleScopedClasses['adjust-btn']} */ ;
    /** @type {__VLS_StyleScopedClasses['adjust-btn--deduct']} */ ;
    if (__VLS_ctx.adjustLoading) {
    }
    else if (__VLS_ctx.isDeduction) {
    }
    else {
    }
    __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
        ...{ onClick: (...[$event]) => {
                if (!(__VLS_ctx.selectedEmail))
                    return;
                __VLS_ctx.selectedEmail = null;
                // @ts-ignore
                [selectedEmail, selectedEmail, selectedEmail, formatGold, selectedBalance, selectedBalance, handleAdjust, isDeduction, isDeduction, isDeduction, adjustAmount, adjustAmount, adjustNote, adjustLoading, adjustLoading,];
            } },
        type: "button",
        ...{ class: "cancel-btn" },
    });
    /** @type {__VLS_StyleScopedClasses['cancel-btn']} */ ;
    if (__VLS_ctx.adjustError) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "form-error" },
            role: "alert",
        });
        /** @type {__VLS_StyleScopedClasses['form-error']} */ ;
        (__VLS_ctx.adjustError);
    }
    if (__VLS_ctx.adjustSuccess) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "form-success" },
            role: "status",
        });
        /** @type {__VLS_StyleScopedClasses['form-success']} */ ;
        (__VLS_ctx.adjustSuccess);
    }
}
__VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
    ...{ class: "gold-section" },
    'aria-labelledby': "tx-heading",
});
/** @type {__VLS_StyleScopedClasses['gold-section']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "gold-section-header" },
});
/** @type {__VLS_StyleScopedClasses['gold-section-header']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.h2, __VLS_intrinsics.h2)({
    id: "tx-heading",
});
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "tx-filter-bar" },
});
/** @type {__VLS_StyleScopedClasses['tx-filter-bar']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.input)({
    ...{ onKeyup: (__VLS_ctx.handleTxFilter) },
    type: "email",
    placeholder: "Filter by email…",
    ...{ class: "search-input" },
    'aria-label': "Filter transactions by email",
});
(__VLS_ctx.txFilterEmail);
/** @type {__VLS_StyleScopedClasses['search-input']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
    ...{ onClick: (__VLS_ctx.handleTxFilter) },
    type: "button",
    ...{ class: "refresh-btn" },
});
/** @type {__VLS_StyleScopedClasses['refresh-btn']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
    ...{ onClick: (() => { __VLS_ctx.txFilterEmail = ''; void __VLS_ctx.loadTransactions(); }) },
    type: "button",
    ...{ class: "refresh-btn refresh-btn--ghost" },
});
/** @type {__VLS_StyleScopedClasses['refresh-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['refresh-btn--ghost']} */ ;
if (__VLS_ctx.txError) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-error" },
        role: "alert",
    });
    /** @type {__VLS_StyleScopedClasses['state-error']} */ ;
    (__VLS_ctx.txError);
}
else if (__VLS_ctx.txLoading && __VLS_ctx.transactions.length === 0) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
}
else if (__VLS_ctx.transactions.length === 0) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
}
else {
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "tx-table-wrap" },
    });
    /** @type {__VLS_StyleScopedClasses['tx-table-wrap']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.table, __VLS_intrinsics.table)({
        ...{ class: "tx-table" },
        'aria-label': "Gold token transaction log",
    });
    /** @type {__VLS_StyleScopedClasses['tx-table']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.thead, __VLS_intrinsics.thead)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.tr, __VLS_intrinsics.tr)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({
        ...{ class: "col-amount" },
    });
    /** @type {__VLS_StyleScopedClasses['col-amount']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.th, __VLS_intrinsics.th)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.tbody, __VLS_intrinsics.tbody)({});
    for (const [tx] of __VLS_vFor((__VLS_ctx.transactions))) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.tr, __VLS_intrinsics.tr)({
            key: (tx.id),
        });
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-date" },
        });
        /** @type {__VLS_StyleScopedClasses['col-date']} */ ;
        (__VLS_ctx.formatDateTime(tx.createdAtUtc));
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-email" },
        });
        /** @type {__VLS_StyleScopedClasses['col-email']} */ ;
        (tx.playerEmail);
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-amount" },
            ...{ class: (tx.amount > 0 ? 'amount-positive' : 'amount-negative') },
        });
        /** @type {__VLS_StyleScopedClasses['col-amount']} */ ;
        (__VLS_ctx.formatTxAmount(tx.amount));
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({});
        (__VLS_ctx.formatGold(tx.balanceBefore));
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({});
        (__VLS_ctx.formatGold(tx.balanceAfter));
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-email" },
        });
        /** @type {__VLS_StyleScopedClasses['col-email']} */ ;
        (tx.adminEmail);
        __VLS_asFunctionalElement1(__VLS_intrinsics.td, __VLS_intrinsics.td)({
            ...{ class: "col-note" },
        });
        /** @type {__VLS_StyleScopedClasses['col-note']} */ ;
        (tx.note ?? '—');
        // @ts-ignore
        [formatGold, formatGold, adjustError, adjustError, adjustSuccess, adjustSuccess, handleTxFilter, handleTxFilter, txFilterEmail, txFilterEmail, loadTransactions, txError, txError, txLoading, transactions, transactions, transactions, formatDateTime, formatTxAmount,];
    }
}
// @ts-ignore
[];
const __VLS_export = (await import('vue')).defineComponent({});
export default {};
