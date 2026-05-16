#!/bin/sh
set -eu

cat > /usr/share/nginx/html/runtime-config.js <<EOF
window.__capitalismRuntimeConfig__ = {
  graphqlUrl: "${APP_GRAPHQL_URL:-}",
  biatecOidcAuthorizeUrl: "${APP_BIATEC_OIDC_AUTHORIZE_URL:-}",
  biatecOidcEndSessionUrl: "${APP_BIATEC_OIDC_END_SESSION_URL:-}",
  biatecOidcClientId: "${APP_BIATEC_OIDC_CLIENT_ID:-}",
  biatecOidcRedirectUri: "${APP_BIATEC_OIDC_REDIRECT_URI:-}",
  biatecOidcScope: "${APP_BIATEC_OIDC_SCOPE:-}",
  biatecOidcAudience: "${APP_BIATEC_OIDC_AUDIENCE:-}",
  biatecOidcAllowedIssuers: "${APP_BIATEC_OIDC_ALLOWED_ISSUERS:-}",
}
EOF