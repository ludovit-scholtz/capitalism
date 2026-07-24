/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_GRAPHQL_URL?: string
  readonly VITE_MASTER_GRAPHQL_URL?: string
  readonly VITE_MASTER_WEB_URL?: string
  readonly VITE_BIATEC_OIDC_AUTHORIZE_URL?: string
  readonly VITE_BIATEC_OIDC_TOKEN_URL?: string
  readonly VITE_BIATEC_OIDC_CLIENT_ID?: string
  readonly VITE_BIATEC_OIDC_REDIRECT_URI?: string
  readonly VITE_BIATEC_OIDC_SCOPE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
