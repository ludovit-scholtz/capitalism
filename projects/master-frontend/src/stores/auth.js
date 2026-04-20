import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { claimStartupPack, fetchMe, fetchMySubscription, loginAccount, prolongSubscription, registerAccount, } from '@/lib/masterApi';
const TOKEN_KEY = 'master_auth_token';
const EXPIRES_KEY = 'master_auth_expires';
export const useAuthStore = defineStore('masterAuth', () => {
    const player = ref(null);
    const subscription = ref(null);
    const token = ref(null);
    const loading = ref(false);
    const error = ref(null);
    const isAuthenticated = computed(() => !!token.value);
    function initFromStorage() {
        const stored = localStorage.getItem(TOKEN_KEY);
        const expires = localStorage.getItem(EXPIRES_KEY);
        if (stored && expires && new Date(expires) > new Date()) {
            token.value = stored;
        }
        else {
            localStorage.removeItem(TOKEN_KEY);
            localStorage.removeItem(EXPIRES_KEY);
        }
    }
    function setSession(auth) {
        token.value = auth.token;
        player.value = auth.player;
        subscription.value = null;
        localStorage.setItem(TOKEN_KEY, auth.token);
        localStorage.setItem(EXPIRES_KEY, auth.expiresAtUtc);
    }
    async function register(email, displayName, password) {
        loading.value = true;
        error.value = null;
        try {
            const auth = await registerAccount(email, displayName, password);
            setSession(auth);
        }
        catch (e) {
            error.value = e instanceof Error ? e.message : 'Registration failed';
            throw e;
        }
        finally {
            loading.value = false;
        }
    }
    async function login(email, password) {
        loading.value = true;
        error.value = null;
        try {
            const auth = await loginAccount(email, password);
            setSession(auth);
        }
        catch (e) {
            error.value = e instanceof Error ? e.message : 'Login failed';
            throw e;
        }
        finally {
            loading.value = false;
        }
    }
    async function fetchProfile() {
        if (!token.value)
            return;
        try {
            player.value = await fetchMe(token.value);
        }
        catch {
            // token may have expired
            logout();
        }
    }
    async function fetchSubscription() {
        if (!token.value)
            return;
        try {
            subscription.value = await fetchMySubscription(token.value);
        }
        catch {
            subscription.value = null;
        }
    }
    async function prolong(months) {
        if (!token.value)
            return;
        loading.value = true;
        error.value = null;
        try {
            subscription.value = await prolongSubscription(token.value, months);
        }
        catch (e) {
            error.value = e instanceof Error ? e.message : 'Failed to prolong subscription';
            throw e;
        }
        finally {
            loading.value = false;
        }
    }
    async function claimStartupPackOffer() {
        if (!token.value)
            return;
        loading.value = true;
        error.value = null;
        try {
            subscription.value = await claimStartupPack(token.value);
            await fetchProfile();
        }
        catch (e) {
            error.value = e instanceof Error ? e.message : 'Failed to claim startup pack';
            throw e;
        }
        finally {
            loading.value = false;
        }
    }
    function logout() {
        token.value = null;
        player.value = null;
        subscription.value = null;
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(EXPIRES_KEY);
    }
    return {
        player,
        subscription,
        token,
        loading,
        error,
        isAuthenticated,
        initFromStorage,
        register,
        login,
        fetchProfile,
        fetchSubscription,
        prolong,
        claimStartupPackOffer,
        logout,
    };
});
