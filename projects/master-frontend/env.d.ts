/// <reference types="vite/client" />

interface ImportMetaEnv {
	readonly VITE_BIATEC_OIDC_AUTHORIZE_URL?: string
	readonly VITE_BIATEC_OIDC_CLIENT_ID?: string
	readonly VITE_BIATEC_OIDC_REDIRECT_URI?: string
	readonly VITE_BIATEC_OIDC_SCOPE?: string
	readonly VITE_BIATEC_OIDC_AUDIENCE?: string
	readonly VITE_BIATEC_OIDC_ALLOWED_ISSUERS?: string
}

interface ImportMeta {
	readonly env: ImportMetaEnv
}