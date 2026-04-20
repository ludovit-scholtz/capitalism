import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { fetchGameServers } from '@/lib/masterApi';
import { formatHeartbeatDistance } from '@/lib/time';
import { formatProlongLabel, formatRenewalNote, formatStatusLabel, formatTierLabel, } from '@/lib/subscription';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const router = useRouter();
const servers = ref([]);
const loading = ref(true);
const errorMessage = ref('');
const prolongMonths = ref(1);
const prolongLoading = ref(false);
const prolongError = ref('');
const prolongSuccess = ref(false);
const startupPackLoading = ref(false);
const startupPackError = ref('');
const startupPackSuccess = ref(false);
const onlineCount = computed(() => servers.value.filter((server) => server.isOnline).length);
const startupPackClaimedAtLabel = computed(() => {
    const claimedAt = auth.player?.startupPackClaimedAtUtc;
    if (!claimedAt) {
        return '';
    }
    return new Intl.DateTimeFormat(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
    }).format(new Date(claimedAt));
});
function heartbeatLabel(server) {
    return formatHeartbeatDistance(server.lastHeartbeatAtUtc);
}
async function loadServers() {
    loading.value = true;
    errorMessage.value = '';
    try {
        servers.value = await fetchGameServers();
    }
    catch (error) {
        errorMessage.value = error instanceof Error ? error.message : 'Unable to load game servers.';
    }
    finally {
        loading.value = false;
    }
}
async function handleProlong() {
    prolongLoading.value = true;
    prolongError.value = '';
    prolongSuccess.value = false;
    try {
        await auth.prolong(prolongMonths.value);
        prolongSuccess.value = true;
    }
    catch (e) {
        prolongError.value = e instanceof Error ? e.message : 'Failed to prolong subscription.';
    }
    finally {
        prolongLoading.value = false;
    }
}
async function handleStartupPackClaim() {
    startupPackLoading.value = true;
    startupPackError.value = '';
    startupPackSuccess.value = false;
    try {
        await auth.claimStartupPackOffer();
        startupPackSuccess.value = true;
    }
    catch (e) {
        startupPackError.value = e instanceof Error ? e.message : 'Failed to claim startup pack.';
    }
    finally {
        startupPackLoading.value = false;
    }
}
function logout() {
    auth.logout();
    void router.push('/');
}
onMounted(() => {
    void loadServers();
});
const __VLS_ctx = {
    ...{},
    ...{},
};
let __VLS_components;
let __VLS_intrinsics;
let __VLS_directives;
/** @type {__VLS_StyleScopedClasses['nav-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['hero-cta']} */ ;
/** @type {__VLS_StyleScopedClasses['metric-card']} */ ;
/** @type {__VLS_StyleScopedClasses['pitch-card']} */ ;
/** @type {__VLS_StyleScopedClasses['subscription-panel']} */ ;
/** @type {__VLS_StyleScopedClasses['pitch-card']} */ ;
/** @type {__VLS_StyleScopedClasses['pitch-card']} */ ;
/** @type {__VLS_StyleScopedClasses['pitch-cta-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['startup-pack-header']} */ ;
/** @type {__VLS_StyleScopedClasses['startup-pack-benefits']} */ ;
/** @type {__VLS_StyleScopedClasses['startup-pack-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['startup-pack-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['perks-list']} */ ;
/** @type {__VLS_StyleScopedClasses['months-picker']} */ ;
/** @type {__VLS_StyleScopedClasses['months-picker']} */ ;
/** @type {__VLS_StyleScopedClasses['prolong-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['prolong-btn']} */ ;
/** @type {__VLS_StyleScopedClasses['servers-header']} */ ;
/** @type {__VLS_StyleScopedClasses['refresh-button']} */ ;
/** @type {__VLS_StyleScopedClasses['refresh-button']} */ ;
/** @type {__VLS_StyleScopedClasses['launch-link']} */ ;
/** @type {__VLS_StyleScopedClasses['subtle-link']} */ ;
/** @type {__VLS_StyleScopedClasses['server-description']} */ ;
/** @type {__VLS_StyleScopedClasses['server-stats']} */ ;
/** @type {__VLS_StyleScopedClasses['server-stats']} */ ;
/** @type {__VLS_StyleScopedClasses['server-stats']} */ ;
/** @type {__VLS_StyleScopedClasses['launch-link']} */ ;
/** @type {__VLS_StyleScopedClasses['subtle-link']} */ ;
/** @type {__VLS_StyleScopedClasses['hero-panel']} */ ;
/** @type {__VLS_StyleScopedClasses['content-grid']} */ ;
/** @type {__VLS_StyleScopedClasses['server-stats']} */ ;
/** @type {__VLS_StyleScopedClasses['master-shell']} */ ;
/** @type {__VLS_StyleScopedClasses['hero-panel']} */ ;
/** @type {__VLS_StyleScopedClasses['pitch-card']} */ ;
/** @type {__VLS_StyleScopedClasses['servers-panel']} */ ;
/** @type {__VLS_StyleScopedClasses['subscription-panel']} */ ;
/** @type {__VLS_StyleScopedClasses['server-card-header']} */ ;
/** @type {__VLS_StyleScopedClasses['server-links']} */ ;
/** @type {__VLS_StyleScopedClasses['server-stats']} */ ;
/** @type {__VLS_StyleScopedClasses['prolong-controls']} */ ;
/** @type {__VLS_StyleScopedClasses['startup-pack-header']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "hero-video-wrapper" },
});
/** @type {__VLS_StyleScopedClasses['hero-video-wrapper']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "hero-video-overlay" },
});
/** @type {__VLS_StyleScopedClasses['hero-video-overlay']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "hero-video-uplayer" },
});
/** @type {__VLS_StyleScopedClasses['hero-video-uplayer']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.video, __VLS_intrinsics.video)({
    autoplay: true,
    muted: true,
    playsinline: true,
    ...{ class: "hero-video" },
});
/** @type {__VLS_StyleScopedClasses['hero-video']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.source)({
    src: "../assets/hero-video.webm",
    type: "video/webm",
});
__VLS_asFunctionalElement1(__VLS_intrinsics.main, __VLS_intrinsics.main)({
    ...{ class: "master-shell" },
});
/** @type {__VLS_StyleScopedClasses['master-shell']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
    ...{ class: "hero-panel" },
});
/** @type {__VLS_StyleScopedClasses['hero-panel']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "hero-copy" },
});
/** @type {__VLS_StyleScopedClasses['hero-copy']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
    ...{ class: "hero-title" },
});
/** @type {__VLS_StyleScopedClasses['hero-title']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "eyebrow" },
});
/** @type {__VLS_StyleScopedClasses['eyebrow']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "hero-text" },
});
/** @type {__VLS_StyleScopedClasses['hero-text']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.b, __VLS_intrinsics.b)({});
__VLS_asFunctionalElement1(__VLS_intrinsics.nav, __VLS_intrinsics.nav)({
    ...{ class: "site-nav" },
});
/** @type {__VLS_StyleScopedClasses['site-nav']} */ ;
if (__VLS_ctx.auth.isAuthenticated) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
        ...{ class: "nav-player" },
    });
    /** @type {__VLS_StyleScopedClasses['nav-player']} */ ;
    (__VLS_ctx.auth.player?.displayName ?? 'Account');
    __VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
        ...{ class: "nav-btn nav-btn--gold" },
        href: "/gold-admin",
    });
    /** @type {__VLS_StyleScopedClasses['nav-btn']} */ ;
    /** @type {__VLS_StyleScopedClasses['nav-btn--gold']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
        ...{ onClick: (__VLS_ctx.logout) },
        ...{ class: "nav-btn nav-btn--ghost" },
        type: "button",
    });
    /** @type {__VLS_StyleScopedClasses['nav-btn']} */ ;
    /** @type {__VLS_StyleScopedClasses['nav-btn--ghost']} */ ;
}
else {
    __VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
        ...{ class: "hero-cta" },
        href: "/login",
    });
    /** @type {__VLS_StyleScopedClasses['hero-cta']} */ ;
}
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "hero-metrics" },
});
/** @type {__VLS_StyleScopedClasses['hero-metrics']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.article, __VLS_intrinsics.article)({
    ...{ class: "metric-card" },
});
/** @type {__VLS_StyleScopedClasses['metric-card']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
    ...{ class: "metric-label" },
});
/** @type {__VLS_StyleScopedClasses['metric-label']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.strong, __VLS_intrinsics.strong)({});
(__VLS_ctx.onlineCount);
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
__VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
    ...{ class: "content-grid" },
});
/** @type {__VLS_StyleScopedClasses['content-grid']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.aside, __VLS_intrinsics.aside)({});
if (__VLS_ctx.auth.isAuthenticated) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
        ...{ class: "subscription-panel" },
        'aria-label': "Subscription dashboard",
    });
    /** @type {__VLS_StyleScopedClasses['subscription-panel']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "section-kicker" },
    });
    /** @type {__VLS_StyleScopedClasses['section-kicker']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.h2, __VLS_intrinsics.h2)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
        ...{ class: "startup-pack-card" },
        'aria-label': "Startup Pack",
    });
    /** @type {__VLS_StyleScopedClasses['startup-pack-card']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "startup-pack-header" },
    });
    /** @type {__VLS_StyleScopedClasses['startup-pack-header']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "startup-pack-kicker" },
    });
    /** @type {__VLS_StyleScopedClasses['startup-pack-kicker']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.h3, __VLS_intrinsics.h3)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
        ...{ class: ([
                'startup-pack-pill',
                __VLS_ctx.auth.player?.canClaimStartupPack
                    ? 'startup-pack-pill--available'
                    : 'startup-pack-pill--claimed',
            ]) },
    });
    /** @type {__VLS_StyleScopedClasses['startup-pack-pill']} */ ;
    (__VLS_ctx.auth.player?.canClaimStartupPack ? 'Available once' : 'Claimed');
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "startup-pack-copy" },
    });
    /** @type {__VLS_StyleScopedClasses['startup-pack-copy']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.ul, __VLS_intrinsics.ul)({
        ...{ class: "startup-pack-benefits" },
    });
    /** @type {__VLS_StyleScopedClasses['startup-pack-benefits']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
    if (__VLS_ctx.auth.player?.canClaimStartupPack) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "startup-pack-actions" },
        });
        /** @type {__VLS_StyleScopedClasses['startup-pack-actions']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
            ...{ onClick: (__VLS_ctx.handleStartupPackClaim) },
            ...{ class: "startup-pack-btn" },
            type: "button",
            disabled: (__VLS_ctx.startupPackLoading),
        });
        /** @type {__VLS_StyleScopedClasses['startup-pack-btn']} */ ;
        (__VLS_ctx.startupPackLoading ? 'Claiming…' : 'Claim Startup Pack');
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "startup-pack-note" },
        });
        /** @type {__VLS_StyleScopedClasses['startup-pack-note']} */ ;
    }
    else {
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "startup-pack-state" },
        });
        /** @type {__VLS_StyleScopedClasses['startup-pack-state']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "startup-pack-note" },
        });
        /** @type {__VLS_StyleScopedClasses['startup-pack-note']} */ ;
        if (__VLS_ctx.startupPackClaimedAtLabel) {
            (__VLS_ctx.startupPackClaimedAtLabel);
        }
        else {
        }
    }
    if (__VLS_ctx.startupPackError) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "prolong-error" },
            role: "alert",
        });
        /** @type {__VLS_StyleScopedClasses['prolong-error']} */ ;
        (__VLS_ctx.startupPackError);
    }
    if (__VLS_ctx.startupPackSuccess) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "prolong-success" },
            role: "status",
        });
        /** @type {__VLS_StyleScopedClasses['prolong-success']} */ ;
    }
    if (__VLS_ctx.auth.subscription) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "sub-status-card" },
        });
        /** @type {__VLS_StyleScopedClasses['sub-status-card']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "sub-tier-row" },
        });
        /** @type {__VLS_StyleScopedClasses['sub-tier-row']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
            ...{ class: (['tier-badge', __VLS_ctx.auth.subscription.tier === 'PRO' ? 'tier-pro' : 'tier-free']) },
        });
        /** @type {__VLS_StyleScopedClasses['tier-badge']} */ ;
        (__VLS_ctx.formatTierLabel(__VLS_ctx.auth.subscription.tier));
        __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
            ...{ class: ([
                    'status-pill',
                    __VLS_ctx.auth.subscription.isActive ? 'status-online' : 'status-offline',
                ]) },
        });
        /** @type {__VLS_StyleScopedClasses['status-pill']} */ ;
        (__VLS_ctx.formatStatusLabel(__VLS_ctx.auth.subscription));
        if (__VLS_ctx.auth.subscription.isActive) {
            __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
                ...{ class: "renewal-note" },
            });
            /** @type {__VLS_StyleScopedClasses['renewal-note']} */ ;
            (__VLS_ctx.formatRenewalNote(__VLS_ctx.auth.subscription));
        }
        if (__VLS_ctx.auth.subscription.isActive && __VLS_ctx.auth.subscription.tier === 'PRO') {
            __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
                ...{ class: "pro-perks" },
            });
            /** @type {__VLS_StyleScopedClasses['pro-perks']} */ ;
            __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
                ...{ class: "perks-label" },
            });
            /** @type {__VLS_StyleScopedClasses['perks-label']} */ ;
            __VLS_asFunctionalElement1(__VLS_intrinsics.ul, __VLS_intrinsics.ul)({
                ...{ class: "perks-list" },
            });
            /** @type {__VLS_StyleScopedClasses['perks-list']} */ ;
            __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
            __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
            __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
            __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
        }
        else if (!__VLS_ctx.auth.subscription.isActive) {
            __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
                ...{ class: "upgrade-prompt" },
            });
            /** @type {__VLS_StyleScopedClasses['upgrade-prompt']} */ ;
            __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
                ...{ class: "upgrade-text" },
            });
            /** @type {__VLS_StyleScopedClasses['upgrade-text']} */ ;
            __VLS_asFunctionalElement1(__VLS_intrinsics.strong, __VLS_intrinsics.strong)({});
        }
    }
    if (__VLS_ctx.auth.subscription?.canProlong) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "prolong-section" },
        });
        /** @type {__VLS_StyleScopedClasses['prolong-section']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "prolong-label" },
        });
        /** @type {__VLS_StyleScopedClasses['prolong-label']} */ ;
        (__VLS_ctx.auth.subscription ? __VLS_ctx.formatProlongLabel(__VLS_ctx.auth.subscription) : '');
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "prolong-controls" },
        });
        /** @type {__VLS_StyleScopedClasses['prolong-controls']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "months-picker" },
        });
        /** @type {__VLS_StyleScopedClasses['months-picker']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.label, __VLS_intrinsics.label)({
            for: "months-select",
        });
        __VLS_asFunctionalElement1(__VLS_intrinsics.select, __VLS_intrinsics.select)({
            id: "months-select",
            value: (__VLS_ctx.prolongMonths),
        });
        for (const [m] of __VLS_vFor(([1, 3, 6, 12]))) {
            __VLS_asFunctionalElement1(__VLS_intrinsics.option, __VLS_intrinsics.option)({
                key: (m),
                value: (m),
            });
            (m);
            (m > 1 ? 's' : '');
            // @ts-ignore
            [auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, auth, logout, onlineCount, handleStartupPackClaim, startupPackLoading, startupPackLoading, startupPackClaimedAtLabel, startupPackClaimedAtLabel, startupPackError, startupPackError, startupPackSuccess, formatTierLabel, formatStatusLabel, formatRenewalNote, formatProlongLabel, prolongMonths,];
        }
        __VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
            ...{ onClick: (__VLS_ctx.handleProlong) },
            ...{ class: "prolong-btn" },
            type: "button",
            disabled: (__VLS_ctx.prolongLoading),
        });
        /** @type {__VLS_StyleScopedClasses['prolong-btn']} */ ;
        (__VLS_ctx.prolongLoading ? 'Processing…' : 'Confirm');
        if (__VLS_ctx.prolongError) {
            __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
                ...{ class: "prolong-error" },
                role: "alert",
            });
            /** @type {__VLS_StyleScopedClasses['prolong-error']} */ ;
            (__VLS_ctx.prolongError);
        }
        if (__VLS_ctx.prolongSuccess) {
            __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
                ...{ class: "prolong-success" },
                role: "status",
            });
            /** @type {__VLS_StyleScopedClasses['prolong-success']} */ ;
        }
    }
}
else {
    __VLS_asFunctionalElement1(__VLS_intrinsics.article, __VLS_intrinsics.article)({
        ...{ class: "pitch-card" },
    });
    /** @type {__VLS_StyleScopedClasses['pitch-card']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "section-kicker" },
    });
    /** @type {__VLS_StyleScopedClasses['section-kicker']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.h2, __VLS_intrinsics.h2)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.ul, __VLS_intrinsics.ul)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
        href: "https://asa.gold",
        target: "_blank",
        rel: "noreferrer",
    });
    __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({});
    __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
        ...{ class: "pitch-cta-area" },
    });
    /** @type {__VLS_StyleScopedClasses['pitch-cta-area']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "pitch-cta-text" },
    });
    /** @type {__VLS_StyleScopedClasses['pitch-cta-text']} */ ;
    __VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
        ...{ class: "pitch-cta-btn" },
        href: "/login",
    });
    /** @type {__VLS_StyleScopedClasses['pitch-cta-btn']} */ ;
}
__VLS_asFunctionalElement1(__VLS_intrinsics.section, __VLS_intrinsics.section)({
    ...{ class: "servers-panel" },
    'aria-labelledby': "server-list-heading",
});
/** @type {__VLS_StyleScopedClasses['servers-panel']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
    ...{ class: "servers-header" },
});
/** @type {__VLS_StyleScopedClasses['servers-header']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
__VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
    ...{ class: "section-kicker" },
});
/** @type {__VLS_StyleScopedClasses['section-kicker']} */ ;
__VLS_asFunctionalElement1(__VLS_intrinsics.h2, __VLS_intrinsics.h2)({
    id: "server-list-heading",
});
__VLS_asFunctionalElement1(__VLS_intrinsics.button, __VLS_intrinsics.button)({
    ...{ onClick: (__VLS_ctx.loadServers) },
    ...{ class: "refresh-button" },
    type: "button",
});
/** @type {__VLS_StyleScopedClasses['refresh-button']} */ ;
if (__VLS_ctx.loading) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
}
else if (__VLS_ctx.errorMessage) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message state-error" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
    /** @type {__VLS_StyleScopedClasses['state-error']} */ ;
    (__VLS_ctx.errorMessage);
}
else if (__VLS_ctx.servers.length === 0) {
    __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
        ...{ class: "state-message" },
    });
    /** @type {__VLS_StyleScopedClasses['state-message']} */ ;
}
else {
    __VLS_asFunctionalElement1(__VLS_intrinsics.ul, __VLS_intrinsics.ul)({
        ...{ class: "server-list" },
    });
    /** @type {__VLS_StyleScopedClasses['server-list']} */ ;
    for (const [server] of __VLS_vFor((__VLS_ctx.servers))) {
        __VLS_asFunctionalElement1(__VLS_intrinsics.li, __VLS_intrinsics.li)({
            key: (server.id),
            ...{ class: "server-card" },
        });
        /** @type {__VLS_StyleScopedClasses['server-card']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "server-card-header" },
        });
        /** @type {__VLS_StyleScopedClasses['server-card-header']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "server-name" },
        });
        /** @type {__VLS_StyleScopedClasses['server-name']} */ ;
        (server.displayName);
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "server-meta" },
        });
        /** @type {__VLS_StyleScopedClasses['server-meta']} */ ;
        (server.region);
        (server.environment);
        (server.version);
        __VLS_asFunctionalElement1(__VLS_intrinsics.span, __VLS_intrinsics.span)({
            ...{ class: (['status-pill', server.isOnline ? 'status-online' : 'status-offline']) },
        });
        /** @type {__VLS_StyleScopedClasses['status-pill']} */ ;
        (server.isOnline ? 'Online' : 'Offline');
        __VLS_asFunctionalElement1(__VLS_intrinsics.p, __VLS_intrinsics.p)({
            ...{ class: "server-description" },
        });
        /** @type {__VLS_StyleScopedClasses['server-description']} */ ;
        (server.description || 'Economic simulation shard registered with the master node.');
        __VLS_asFunctionalElement1(__VLS_intrinsics.dl, __VLS_intrinsics.dl)({
            ...{ class: "server-stats" },
        });
        /** @type {__VLS_StyleScopedClasses['server-stats']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dt, __VLS_intrinsics.dt)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dd, __VLS_intrinsics.dd)({});
        (server.playerCount);
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dt, __VLS_intrinsics.dt)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dd, __VLS_intrinsics.dd)({});
        (server.companyCount);
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dt, __VLS_intrinsics.dt)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dd, __VLS_intrinsics.dd)({});
        (server.currentTick);
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dt, __VLS_intrinsics.dt)({});
        __VLS_asFunctionalElement1(__VLS_intrinsics.dd, __VLS_intrinsics.dd)({});
        (__VLS_ctx.heartbeatLabel(server));
        __VLS_asFunctionalElement1(__VLS_intrinsics.div, __VLS_intrinsics.div)({
            ...{ class: "server-links" },
        });
        /** @type {__VLS_StyleScopedClasses['server-links']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
            ...{ class: "launch-link" },
            href: (server.frontendUrl),
            target: "_blank",
            rel: "noreferrer",
        });
        /** @type {__VLS_StyleScopedClasses['launch-link']} */ ;
        __VLS_asFunctionalElement1(__VLS_intrinsics.a, __VLS_intrinsics.a)({
            ...{ class: "subtle-link" },
            href: (server.graphqlUrl),
            target: "_blank",
            rel: "noreferrer",
        });
        /** @type {__VLS_StyleScopedClasses['subtle-link']} */ ;
        // @ts-ignore
        [handleProlong, prolongLoading, prolongLoading, prolongError, prolongError, prolongSuccess, loadServers, loading, errorMessage, errorMessage, servers, servers, heartbeatLabel,];
    }
}
// @ts-ignore
[];
const __VLS_export = (await import('vue')).defineComponent({});
export default {};
